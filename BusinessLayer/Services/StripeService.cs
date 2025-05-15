using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using ModelLayer.Models;
using ModelLayer.Models.Enums;
using Microsoft.Data.Sqlite;

public class StripeService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly CoachingDbContext _context;
    private readonly ILogService _logService;
    private readonly IHelperService _helperService;
    private readonly ISessionService _sessionService;
    private readonly ISessionPackService _sessionPackService;

    public StripeService(IConfiguration configuration, 
        CoachingDbContext context, 
        ILogService logService, 
        IHelperService helperService, 
        ISessionService sessionService,
        ISessionPackService sessionPackService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        _helperService = helperService;
        _sessionService = sessionService;
        _sessionPackService = sessionPackService;
        StripeConfiguration.ApiKey = _helperService.GetConfigValue("Stripe:SecretKey");
    }

    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request)
    {
        try
        {
            var session = request.Session;
            var bookingType = request.BookingType;
            var planId = request.PlanId;

            if (session == null)
            {
                await _logService.LogError("CreateCheckoutSessionAsync", "Session object is null.");
                throw new ArgumentNullException(nameof(request.Session));
            }

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Received session with Id: {session.Id}, StripeSessionId: {session.StripeSessionId}, BookingType: {bookingType}, PlanId: {planId}");

            if (bookingType == BookingType.SessionPack)
            {
                var existingPendingSession = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.Email == session.Email && s.PackId == planId && s.IsPending);
                if (existingPendingSession != null)
                {
                    await _logService.LogWarning("CreateCheckoutSessionAsync", $"Pending session already exists for Email: {session.Email}, PackId: {planId}, SessionId: {existingPendingSession.Id}");
                    throw new InvalidOperationException($"A pending session already exists (ID: {existingPendingSession.Id}). Please complete or cancel the existing payment.");
                }
            }
            
            if (session.Id == 0)
            {
                session.IsPending = true;
                await _sessionService.CreatePendingSessionAsync(session);
                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Created new pending session with Id: {session.Id}");
            }
            else
            {
                var existingSession = await _context.Sessions.FirstOrDefaultAsync(s => s.Id == session.Id);
                if (existingSession == null)
                {
                    await _logService.LogWarning("CreateCheckoutSessionAsync", $"Session has non-zero Id: {session.Id} but does not exist in database. Creating new pending session.");
                    session.Id = 0;
                    session.IsPending = true;
                    await _sessionService.CreatePendingSessionAsync(session);
                }
                else
                {
                    await _logService.LogInfo("CreateCheckoutSessionAsync", $"Session already exists with Id: {existingSession.Id}. Using existing pending session.");
                    session = existingSession;
                }
            }

            SessionCreateOptions options;
            if (bookingType == BookingType.SessionPack && !string.IsNullOrEmpty(planId))
            {
                if (!int.TryParse(planId, out var priceId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid PlanId format: {planId}");
                    throw new ArgumentException("Invalid PlanId format.");
                }

                var packPrice = await _context.SessionPackPrices
                    .FirstOrDefaultAsync(sp => sp.Id == priceId);

                if (packPrice == null)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"No session pack price found for PlanId: {planId}");
                    throw new Exception("Session pack price not found.");
                }

                options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "eur",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Session Pack: {packPrice.Name}",
                                },
                                UnitAmountDecimal = (long)(packPrice.PriceEUR * 100),
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    SuccessUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled",
                    Metadata = new Dictionary<string, string>
                    {
                        { "SessionId", session.Id.ToString() },
                        { "BookingType", bookingType.ToString() },
                        { "PlanId", planId }
                    }
                };
            }
            else if (bookingType == BookingType.Subscription && !string.IsNullOrEmpty(planId))
            {
                if (!int.TryParse(planId, out var priceId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid PlanId format: {planId}");
                    throw new ArgumentException("Invalid PlanId format.");
                }

                var subscriptionPrice = await _context.SubscriptionPrices
                    .FirstOrDefaultAsync(sp => sp.Id == priceId && sp.SessionType == session.SessionCategory);

                if (subscriptionPrice == null)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"No subscription price found for PlanId: {planId}");
                    throw new Exception("Subscription price not found.");
                }

                options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Price = subscriptionPrice.StripePriceId,
                            Quantity = 1,
                        },
                    },
                    Mode = "subscription",
                    SuccessUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled",
                    Metadata = new Dictionary<string, string>
                    {
                        { "SessionId", session.Id.ToString() },
                        { "BookingType", bookingType.ToString() },
                        { "PlanId", planId }
                    }
                };
            }
            else if (bookingType == BookingType.SingleSession)
            {
                var servicePrice = await _context.SessionPrices
                    .FirstOrDefaultAsync(sp => sp.SessionType == session.SessionCategory);

                if (servicePrice == null)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"No price found for session type {session.SessionCategory}");
                    throw new Exception("Service price not found.");
                }

                options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "eur",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Coaching Session: {session.SessionCategory}",
                                },
                                UnitAmountDecimal = (long)(servicePrice.PriceEUR * 100),
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    SuccessUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled",
                    Metadata = new Dictionary<string, string>
                    {
                        { "SessionId", session.Id.ToString() },
                        { "BookingType", bookingType.ToString() }
                    }
                };
            }
            else
            {
                await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid BookingType: {bookingType}");
                throw new ArgumentException("Invalid booking type.");
            }

            var stripeService = new SessionService();
            var stripeSession = await stripeService.CreateAsync(options);

            try
            {
                session.StripeSessionId = stripeSession.Id;
                _context.Sessions.Update(session);
                await _context.SaveChangesAsync();
                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Updated session Id: {session.Id} with StripeSessionId: {stripeSession.Id}");
            }
            catch (DbUpdateException ex)
            {
                await _logService.LogError("CreateCheckoutSessionAsync", $"Failed to update session Id: {session.Id} with StripeSessionId: {stripeSession.Id}. Error: {ex.Message}");
                throw new Exception("Failed to save StripeSessionId to database.", ex);
            }

            return stripeSession.Url;
        }
        catch (StripeException ex)
        {
            await _logService.LogError("CreateCheckoutSessionAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw new Exception("Failed to create Stripe checkout session.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
        {
            await _logService.LogError("CreateCheckoutSessionAsync", $"UNIQUE constraint failed: {sqliteEx.Message}, Session Id: {request.Session?.Id}");
            throw new Exception("Failed to create session due to duplicate Id.");
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateCheckoutSessionAsync Error", ex.Message);
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAsync(string stripeSessionId)
    {
        try
        {
            var stripeSessionService = new SessionService();
            var stripeSession = await stripeSessionService.GetAsync(stripeSessionId);

            if (stripeSession.PaymentStatus != "paid" && stripeSession.Status != "complete")
            {
                await _logService.LogError("ConfirmPaymentAsync", $"Payment not completed for StripeSessionId: {stripeSessionId}");
                return false;
            }

            var dbSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.StripeSessionId == stripeSessionId && s.IsPending);

            if (dbSession == null)
            {
                await _logService.LogError("ConfirmPaymentAsync", $"No pending session found for StripeSessionId: {stripeSessionId}");
                return false;
            }

            var bookingTypeStr = stripeSession.Metadata.TryGetValue("BookingType", out var bt) ? bt : null;
            var planId = stripeSession.Metadata.TryGetValue("PlanId", out var pid) ? pid : null;
            var bookingType = Enum.TryParse<BookingType>(bookingTypeStr, out var parsedType) ? parsedType : BookingType.SingleSession;

            string packId = null;

            if (bookingType == BookingType.SessionPack && !string.IsNullOrEmpty(planId))
            {
                if (!int.TryParse(planId, out var priceId))
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Invalid PlanId format: {planId}");
                    return false;
                }

                var packPrice = await _context.SessionPackPrices
                    .FirstOrDefaultAsync(sp => sp.Id == priceId);

                if (packPrice == null)
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Session pack price not found for PlanId: {planId}");
                    return false;
                }

                var sessionPack = new SessionPack
                {
                    UserId = dbSession.Email,
                    PriceId = packPrice.Id,
                    SessionsRemaining = packPrice.TotalSessions,
                    PurchasedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMonths(3)
                };

                await _sessionPackService.CreateAsync(sessionPack);
                packId = sessionPack.Id.ToString();
                await _logService.LogInfo("ConfirmPaymentAsync", $"Created SessionPack for UserId: {sessionPack.UserId}, PackId: {sessionPack.Id}, SessionsRemaining: {sessionPack.SessionsRemaining}");

                var success = await _sessionPackService.ConsumeSession(dbSession.Email, packId);
                if (!success)
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Failed to deduct session from pack for UserId: {dbSession.Email}, PackId: {packId}");
                    return false;
                }
            }
            else if (bookingType == BookingType.Subscription && !string.IsNullOrEmpty(planId))
            {
                if (!int.TryParse(planId, out var priceId))
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Invalid PlanId format: {planId}");
                    return false;
                }

                var subscriptionPrice = await _context.SubscriptionPrices
                    .FirstOrDefaultAsync(sp => sp.Id == priceId);

                if (subscriptionPrice == null)
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Subscription price not found for PlanId: {planId}");
                    return false;
                }

                var periodStart = stripeSession.Created;
                var periodEnd = periodStart.AddMonths(1);

                var userSubscription = new UserSubscription
                {
                    UserId = dbSession.Email,
                    PriceId = priceId,
                    StripeSubscriptionId = stripeSession.SubscriptionId,
                    IsActive = true,
                    SessionsUsedThisMonth = 1,
                    CurrentPeriodStart = periodStart,
                    CurrentPeriodEnd = periodEnd
                };

                _context.UserSubscriptions.Add(userSubscription);
                await _context.SaveChangesAsync();
                await _logService.LogInfo("ConfirmPaymentAsync", $"Created UserSubscription for UserId: {userSubscription.UserId}, SubscriptionId: {userSubscription.StripeSubscriptionId}, SessionsUsedThisMonth: {userSubscription.SessionsUsedThisMonth}");
            }

            dbSession.IsPaid = true;
            dbSession.PaidAt = DateTime.UtcNow;
            dbSession.PackId = packId; //
            await _sessionService.CreateSessionAsync(dbSession);
            await _logService.LogInfo("ConfirmPaymentAsync", $"Confirmed session Id: {dbSession.Id}, UserId: {dbSession.Email}, StripeSessionId: {stripeSessionId}, PackId: {dbSession.PackId}");

            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogError("ConfirmPaymentAsync Error", ex.Message);
            return false;
        }
    }

    public async Task HandleWebhookAsync(string json, string stripeSignature)
    {
        try
        {
            var webhookSecret = _helperService.GetConfigValue("Stripe:WebhookSecret");
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

            if (stripeEvent.Type == "checkout.session.completed")
            {
                var checkoutSession = stripeEvent.Data.Object as Stripe.Checkout.Session;
                if (checkoutSession == null || string.IsNullOrEmpty(checkoutSession.Id))
                {
                    await _logService.LogError("HandleWebhookAsync", "Invalid checkout session data");
                    return;
                }

                var paymentConfirmed = await ConfirmPaymentAsync(checkoutSession.Id);
                if (!paymentConfirmed)
                {
                    await _logService.LogError("HandleWebhookAsync", $"Failed to confirm payment for StripeSessionId: {checkoutSession.Id}");
                    return;
                }

                var dbSession = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.StripeSessionId == checkoutSession.Id);
                if (dbSession == null)
                {
                    await _logService.LogError("HandleWebhookAsync", $"Session not found for StripeSessionId: {checkoutSession.Id}");
                    return;
                }

                await _sessionService.CreateSessionAsync(dbSession);
                await _logService.LogInfo("HandleWebhookAsync", $"Successfully processed checkout.session.completed for StripeSessionId: {checkoutSession.Id}");
            }
            else
            {
                await _logService.LogInfo("HandleWebhookAsync", $"Unhandled event type: {stripeEvent.Type}");
            }
        }
        catch (StripeException ex)
        {
            await _logService.LogError("HandleWebhookAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw;
        }
        catch (Exception ex)
        {
            await _logService.LogError("HandleWebhookAsync Error", ex.Message);
            throw;
        }
    }

    public async Task<string> CreateOrUpdateSubscriptionPriceAsync(string productName, decimal amount, string sessionType)
    {
        try
        {
            await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync", $"Processing price for {productName}, €{amount}, SessionType: {sessionType}");

            SubscriptionPrice existingPrice = null;

            // Check if a Price already exists for this SessionType
            if (Enum.TryParse<SessionType>(sessionType, out var parsedSessionType))
            {
                existingPrice = await _context.SubscriptionPrices
                    .FirstOrDefaultAsync(sp => sp.SessionType == parsedSessionType && !string.IsNullOrEmpty(sp.StripePriceId));
            }
            else
            {
                throw new ArgumentException($"Invalid session type: {sessionType}");
            }

            if (existingPrice != null)
            {
                var priceService = new PriceService();
                var stripePrice = await priceService.GetAsync(existingPrice.StripePriceId);
                if (stripePrice.UnitAmountDecimal == (long)(amount * 100))
                {
                    await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync", $"Existing Price ID: {existingPrice.StripePriceId} matches amount €{amount}");
                    return existingPrice.StripePriceId;
                }
                else
                {
                    await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync", $"Amount changed from €{stripePrice.UnitAmountDecimal / 100} to €{amount}. Creating new Price.");
                }
            }

            // Create new Product
            var productService = new ProductService();
            var productOptions = new ProductCreateOptions
            {
                Name = productName,
                Description = $"Subscription for {sessionType}",
                Metadata = new Dictionary<string, string> { { "SessionType", sessionType } }
            };
            var product = await productService.CreateAsync(productOptions);

            // Create new Price
            var priceServiceCreate = new PriceService();
            var priceOptions = new PriceCreateOptions
            {
                UnitAmount = (long)(amount * 100),
                Currency = "eur",
                Recurring = new PriceRecurringOptions
                {
                    Interval = "month"
                },
                Product = product.Id,
                Metadata = new Dictionary<string, string> { { "SessionType", sessionType } }
            };
            var price = await priceServiceCreate.CreateAsync(priceOptions);

            await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync", $"Created Price ID: {price.Id} for Product: {productName}, Amount: €{amount}");
            return price.Id;
        }
        catch (StripeException ex)
        {
            await _logService.LogError("CreateOrUpdateSubscriptionPriceAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw new Exception("Failed to create or update Stripe subscription price.");
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateOrUpdateSubscriptionPriceAsync Error", ex.Message);
            throw;
        }
    }
}