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
    private readonly ICurrencyConversionService _currencyConversionService;

    public StripeService(IConfiguration configuration,
        CoachingDbContext context,
        ILogService logService,
        IHelperService helperService,
        ISessionService sessionService,
        ISessionPackService sessionPackService,
        ICurrencyConversionService currencyConversionService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        _helperService = helperService;
        _sessionService = sessionService;
        _sessionPackService = sessionPackService;
        _currencyConversionService = currencyConversionService;
        StripeConfiguration.ApiKey = _helperService.GetConfigValue("Stripe:SecretKey");
    }

    public async Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest request)
    {
        try
        {
            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Starting checkout for Request: {request?.Session?.Id}, Email: {request?.Session?.Email}, BookingType: {request?.BookingType}, Currency: {request?.Currency}");

            var session = request.Session;
            var bookingType = request.BookingType;
            var planId = request.PlanId;
            var userCurrency = request.Currency?.ToUpper() ?? "GBP";

            if (!IsStripeSupportedCurrency(userCurrency))
            {
                await _logService.LogWarning("CreateCheckoutSessionAsync", $"Unsupported currency: {userCurrency}. Falling back to GBP.");
                userCurrency = "GBP";
            }

            if (session == null)
            {
                await _logService.LogError("CreateCheckoutSessionAsync", "Session object is null.");
                throw new ArgumentNullException(nameof(request.Session));
            }

            if (bookingType == BookingType.SessionPack || bookingType == BookingType.Subscription)
            {
                if (string.IsNullOrEmpty(planId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", "PlanId is required for SessionPack or Subscription bookings.");
                    throw new ArgumentException("The PlanId field is required.");
                }
                if (bookingType == BookingType.SessionPack && string.IsNullOrEmpty(session.PackId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", "PackId is required for SessionPack bookings.");
                    throw new ArgumentException("The PackId field is required.");
                }
            }
            else if (bookingType == BookingType.SingleSession)
            {
                if (!string.IsNullOrEmpty(planId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", "PlanId should be null for SingleSession bookings.");
                    throw new ArgumentException("The PlanId field should be null for SingleSession bookings.");
                }
                if (!string.IsNullOrEmpty(session.PackId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", "PackId should be null for SingleSession bookings.");
                    throw new ArgumentException("The PackId field should be null for SingleSession bookings.");
                }
            }

            await _logService.LogInfo("CreateCheckoutSessionAsync",
                $"Received session with Id: {session.Id}, Email: {session.Email}, StripeSessionId: {session.StripeSessionId}, BookingType: {bookingType}, PlanId: {planId}, Currency: {userCurrency}, SessionCategory: {session.SessionCategory}, PreferredDateTime: {session.PreferredDateTime}");

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Validating session Id: {session.Id}");
            var existingSession = await _sessionService.GetSessionByIdAsync(session.Id);
            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Session validation result: Id: {existingSession?.Id}, IsPending: {existingSession?.IsPending}");
            if (existingSession == null || !existingSession.IsPending)
            {
                await _logService.LogError("CreateCheckoutSessionAsync",
                    $"Session not found or not pending. SessionId: {session.Id}, Email: {session.Email}, Found: {existingSession != null}, IsPending: {existingSession?.IsPending}");
                throw new InvalidOperationException("Session not found or not pending.");
            }

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Cleaning up stale sessions for Email: {session.Email}");
            await CleanupStalePendingSessionsAsync(session.Email, bookingType, planId, session.SessionCategory.ToString(), session.PreferredDateTime);
            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Completed cleanup for Email: {session.Email}");

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Starting transaction for session Id: {session.Id}");

            SessionCreateOptions options;
            if (bookingType == BookingType.SessionPack && !string.IsNullOrEmpty(planId))
            {
                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Processing SessionPack with PlanId: {planId}");
                if (!int.TryParse(planId, out var priceId))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid PlanId format: {planId}");
                    throw new ArgumentException("Invalid PlanId format.");
                }

                var packPrice = await _context.SessionPackPrices.FirstOrDefaultAsync(sp => sp.Id == priceId);
                if (packPrice == null)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"No session pack price found for PlanId: {planId}");
                    throw new Exception("Session pack price not found.");
                }

                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Converting price for PlanId: {planId}, PriceGBP: {packPrice.PriceGBP}");
                var (priceValue, priceError) = await _currencyConversionService.ConvertPrice(packPrice.PriceGBP, userCurrency);
                if (!string.IsNullOrEmpty(priceError))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Currency conversion error: {priceError}");
                    throw new Exception($"Currency conversion error: {priceError}");
                }

                options = new SessionCreateOptions
                {
                    CustomerEmail = session.Email,
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = userCurrency.ToLower(),
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Session Pack: {packPrice.Name}",
                                },
                                UnitAmountDecimal = (long)(priceValue * 100),
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
                        { "PlanId", planId },
                        { "Currency", userCurrency }
                    }
                };
            }
            else if (bookingType == BookingType.Subscription && !string.IsNullOrEmpty(planId))
            {
                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Processing Subscription with PlanId: {planId}");
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

                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Converting price for PlanId: {planId}, PriceGBP: {subscriptionPrice.PriceGBP}");
                var (priceInUserCurrency, priceError) = await _currencyConversionService.ConvertPrice(subscriptionPrice.PriceGBP, userCurrency);
                if (!string.IsNullOrEmpty(priceError))
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Currency conversion error: {priceError}");
                    throw new Exception($"Currency conversion error: {priceError}");
                }

                await _logService.LogInfo("CreateCheckoutSessionAsync", $"Creating or updating Stripe price for PlanId: {planId}");
                var stripePriceId = await CreateOrUpdateSubscriptionPriceAsync(
                    $"Subscription: {subscriptionPrice.Name}",
                    priceInUserCurrency,
                    subscriptionPrice.SessionType.ToString(),
                    userCurrency
                );

                options = new SessionCreateOptions
                {
                    CustomerEmail = session.Email,
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Price = stripePriceId,
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
                        { "PlanId", planId },
                        { "Currency", userCurrency }
                    }
                };
            }
            else if (bookingType == BookingType.SingleSession)
            {
                await _logService.LogInfo("CreateCheckoutSessionAsync",
                    $"Processing SingleSession for SessionCategory: {session.SessionCategory}, Currency: {userCurrency}");

                try
                {
                    await _logService.LogInfo("CreateCheckoutSessionAsync",
                        $"Retrieving price for SessionCategory: {session.SessionCategory}");
                    var servicePrice = await _context.SessionPrices
                        .FirstOrDefaultAsync(sp => sp.SessionType == session.SessionCategory);
                    if (servicePrice == null)
                    {
                        await _logService.LogError("CreateCheckoutSessionAsync",
                            $"No price found for session category: {session.SessionCategory}");
                        throw new InvalidOperationException($"No price found for session category: {session.SessionCategory}");
                    }
                    await _logService.LogInfo("CreateCheckoutSessionAsync",
                        $"Found price for SessionCategory: {session.SessionCategory}, PriceGBP: {servicePrice.PriceGBP}");

                    await _logService.LogInfo("CreateCheckoutSessionAsync",
                        $"Converting price {servicePrice.PriceGBP} GBP to {userCurrency}");
                    (decimal priceInUserCurrency, string priceError) = await _currencyConversionService.ConvertPrice(servicePrice.PriceGBP, userCurrency);
                    if (!string.IsNullOrEmpty(priceError))
                    {
                        await _logService.LogError("CreateCheckoutSessionAsync",
                            $"Currency conversion error for {userCurrency}: {priceError}");
                        throw new InvalidOperationException($"Currency conversion error: {priceError}");
                    }
                    await _logService.LogInfo("CreateCheckoutSessionAsync",
                        $"Converted price: {priceInUserCurrency} {userCurrency}");

                    options = new SessionCreateOptions
                    {
                        CustomerEmail = session.Email,
                        PaymentMethodTypes = new List<string> { "card" },
                        LineItems = new List<SessionLineItemOptions>
                        {
                            new SessionLineItemOptions
                            {
                                PriceData = new SessionLineItemPriceDataOptions
                                {
                                    Currency = userCurrency.ToLower(),
                                    ProductData = new SessionLineItemPriceDataProductDataOptions
                                    {
                                        Name = $"Coaching Session: {session.SessionCategory}",
                                    },
                                    UnitAmountDecimal = (long)(priceInUserCurrency * 100),
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
                            { "Currency", userCurrency }
                        }
                    };
                    await _logService.LogInfo("CreateCheckoutSessionAsync",
                        $"Created SessionCreateOptions for SessionId: {session.Id}, Currency: {userCurrency}");
                }
                catch (Exception ex)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync SingleSession Error",
                        $"Failed to process SingleSession for SessionId: {session.Id}, SessionCategory: {session.SessionCategory}. Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                    throw;
                }
            }
            else
            {
                await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid BookingType: {bookingType}");
                throw new ArgumentException("Invalid booking type.");
            }

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Creating Stripe checkout session with IdempotencyKey: {request.IdempotencyKey}");
            var requestOptions = new RequestOptions
            {
                IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString()
            };

            var stripeService = new SessionService();
            var stripeSession = await stripeService.CreateAsync(options, requestOptions);

            await _logService.LogInfo("CreateCheckoutSessionAsync", $"Updating session Id: {session.Id} with StripeSessionId: {stripeSession.Id}");
            session.StripeSessionId = stripeSession.Id;
            await _sessionService.CreatePendingSessionAsync(session);


            try
            {
                await _context.SaveChangesAsync();
            }
            catch (SqliteException ex)
            {
                await _logService.LogError("CreateCheckoutSessionAsync SQLite Error",
                    $"Failed to save changes for SessionId: {session.Id}, Error: {ex.Message}, SqliteErrorCode: {ex.SqliteErrorCode}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
            catch (DbUpdateException ex)
            {
                await _logService.LogError("CreateCheckoutSessionAsync DbUpdate Error",
                    $"Failed to save changes for SessionId: {session.Id}, Error: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                throw;
            }

            await _logService.LogInfo("CreateCheckoutSessionAsync",
                $"Completed checkout for session Id: {session.Id}, StripeSessionId: {stripeSession.Id}, Currency: {userCurrency}, PreferredDateTime: {session.PreferredDateTime}");

            return stripeSession.Url;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateCheckoutSessionAsync Error",
                $"Failed for SessionId: {request?.Session?.Id}, Email: {request?.Session?.Email}, Error: {ex.Message}, StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<bool> ConfirmPaymentAsync(string stripeSessionId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
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
                .Include(s => s.VideoSession)
                .FirstOrDefaultAsync(s => s.StripeSessionId == stripeSessionId);

            if (dbSession == null)
            {
                await _logService.LogError("ConfirmPaymentAsync", $"No session found for StripeSessionId: {stripeSessionId}");
                return false;
            }

            if (!dbSession.IsPending && dbSession.IsPaid && dbSession.VideoSession != null)
            {
                await _logService.LogInfo("ConfirmPaymentAsync", $"Session Id: {dbSession.Id} already confirmed with VideoSession for StripeSessionId: {stripeSessionId}");
                await transaction.CommitAsync();
                return true;
            }

            if (dbSession.IsPending && dbSession.VideoSession != null)
            {
                await _logService.LogWarning("ConfirmPaymentAsync", $"Pending session Id: {dbSession.Id} already has VideoSession for StripeSessionId: {stripeSessionId}");
                await transaction.CommitAsync();
                return true;
            }

            var bookingTypeStr = stripeSession.Metadata.TryGetValue("BookingType", out var bt) ? bt : null;
            var planId = stripeSession.Metadata.TryGetValue("PlanId", out var pid) ? pid : null;
            var currency = stripeSession.Metadata.TryGetValue("Currency", out var curr) ? curr : "GBP";
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
                await _logService.LogInfo("ConfirmPaymentAsync",
                    $"Created SessionPack for UserId: {sessionPack.UserId}, PackId: {sessionPack.Id}, SessionsRemaining: {sessionPack.SessionsRemaining}, Currency: {currency}");

                var success = await _sessionPackService.ConsumeSession(dbSession.Email, packId);
                if (!success)
                {
                    await _logService.LogError("ConfirmPaymentAsync",
                        $"Failed to deduct session from pack for UserId: {dbSession.Email}, PackId: {packId}");
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

                if (!string.IsNullOrEmpty(subscriptionPrice.StripePriceId))
                {
                    var priceService = new PriceService();
                    var stripePrice = await priceService.GetAsync(subscriptionPrice.StripePriceId);
                    if (!stripePrice.Active)
                    {
                        await _logService.LogError("ConfirmPaymentAsync",
                            $"Stripe price ID: {subscriptionPrice.StripePriceId} is inactive for PlanId: {planId}");
                        return false;
                    }
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
                await _logService.LogInfo("ConfirmPaymentAsync",
                    $"Created UserSubscription for UserId: {userSubscription.UserId}, SubscriptionId: {userSubscription.StripeSubscriptionId}");
            }

            dbSession.IsPaid = true;
            dbSession.PaidAt = DateTime.UtcNow;
            dbSession.PackId = packId;
            dbSession.IsPending = false;

            if (dbSession.VideoSession == null)
            {
                var videoSession = new VideoSession
                {
                    UserId = dbSession.Email,
                    ScheduledAt = dbSession.PreferredDateTime,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Session = dbSession,
                    SessionRefId = dbSession.Id
                };
                _context.VideoSessions.Add(videoSession);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            await _logService.LogInfo("ConfirmPaymentAsync",
                $"Confirmed session Id: {dbSession.Id}, UserId: {dbSession.Email}, StripeSessionId: {stripeSessionId}, PackId: {dbSession.PackId}, Currency: {currency}");

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await _logService.LogError("ConfirmPaymentAsync Error",
                $"Failed to confirm payment for StripeSessionId: {stripeSessionId}. Error: {ex.Message}");
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

                try
                {
                    var dbSession = await _context.Sessions.FirstOrDefaultAsync(s => s.StripeSessionId == checkoutSession.Id);
                    if (dbSession == null)
                    {
                        await _logService.LogError("HandleWebhookAsync", $"Session not found for StripeSessionId: {checkoutSession.Id}");
                        return;
                    }

                    if (!dbSession.IsPending && dbSession.IsPaid)
                    {
                        await _logService.LogInfo("HandleWebhookAsync", $"Session already confirmed for StripeSessionId: {checkoutSession.Id}");
                        return;
                    }

                    if (!dbSession.IsPending || dbSession.IsPaid)
                    {
                        await _logService.LogInfo("HandleWebhookAsync", $"Session Id: {dbSession.Id} already processed for StripeSessionId: {checkoutSession.Id}");
                        return;
                    }

                    var paymentConfirmed = await ConfirmPaymentAsync(checkoutSession.Id);
                    if (!paymentConfirmed)
                    {
                        await _logService.LogError("HandleWebhookAsync", $"Failed to confirm payment for StripeSessionId: {checkoutSession.Id}");
                        return;
                    }

                    await _sessionService.CreateSessionAsync(dbSession);
                    await _context.SaveChangesAsync();
                    await _logService.LogInfo("HandleWebhookAsync", $"Successfully processed checkout.session.completed for StripeSessionId: {checkoutSession.Id}");
                }
                catch (Exception ex)
                {
                    await _logService.LogError("HandleWebhookAsync", $"Failed to process webhook for StripeSessionId: {checkoutSession.Id}. Error: {ex.Message}");
                    throw;
                }
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

    public async Task<string> CreateOrUpdateSubscriptionPriceAsync(string productName, decimal amount, string sessionType, string currency = "GBP")
    {
        try
        {
            await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                $"Processing price for {productName}, Amount: {amount}, Currency: {currency}, SessionType: {sessionType}");

            SubscriptionPrice? existingPrice = null;

            if (Enum.TryParse<SessionType>(sessionType, out var parsedSessionType))
            {
                existingPrice = await _context.SubscriptionPrices
                    .FirstOrDefaultAsync(sp => sp.SessionType == parsedSessionType && sp.Currency == currency && !string.IsNullOrEmpty(sp.StripePriceId));
            }
            else
            {
                throw new ArgumentException($"Invalid session type: {sessionType}");
            }

            if (existingPrice != null)
            {
                var priceService = new PriceService();
                var stripePrice = await priceService.GetAsync(existingPrice.StripePriceId);
                if (stripePrice.Active && stripePrice.UnitAmountDecimal == (long)(amount * 100) && stripePrice.Currency.ToUpper() == currency.ToUpper())
                {
                    await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                        $"Using existing active Price ID: {existingPrice.StripePriceId}");
                    return existingPrice.StripePriceId;
                }
                else
                {
                    await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                        $"Existing price ID: {existingPrice.StripePriceId} is inactive or mismatched. Creating new Price.");
                }
            }

            var productService = new ProductService();
            var productOptions = new ProductCreateOptions
            {
                Name = productName,
                Description = $"Subscription for {sessionType} in {currency}",
                Metadata = new Dictionary<string, string>
                {
                    { "SessionType", sessionType },
                    { "Currency", currency }
                }
            };
            var product = await productService.CreateAsync(productOptions);

            var priceServiceCreate = new PriceService();
            var priceOptions = new PriceCreateOptions
            {
                UnitAmount = (long)(amount * 100),
                Currency = currency.ToLower(),
                Recurring = new PriceRecurringOptions
                {
                    Interval = "month"
                },
                Product = product.Id,
                Metadata = new Dictionary<string, string>
                {
                    { "SessionType", sessionType },
                    { "Currency", currency }
                }
            };
            var price = await priceServiceCreate.CreateAsync(priceOptions);

            var subscriptionPrice = new SubscriptionPrice
            {
                SessionType = parsedSessionType,
                Name = productName,
                PriceGBP = amount,
                Currency = currency,
                StripePriceId = price.Id,
                MonthlyLimit = existingPrice?.MonthlyLimit ?? 1
            };
            _context.SubscriptionPrices.Add(subscriptionPrice);
            await _context.SaveChangesAsync();

            await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                $"Created Price ID: {price.Id} for Product: {productName}, Amount: {amount} {currency}");
            return price.Id;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateOrUpdateSubscriptionPriceAsync",
                $"Failed to create or update price for {productName}. Error: {ex.Message}");
            throw;
        }
    }

    public async Task CleanupStalePendingSessionsAsync(string userEmail, BookingType bookingType, string planId, string sessionCategory, DateTime preferredDateTime)
    {
        try
        {
            var staleThreshold = DateTime.UtcNow.AddHours(-12);
            var staleSessions = await _context.Sessions
                .Where(s => s.Email == userEmail &&
                            s.IsPending &&
                            s.CreatedAt < staleThreshold &&
                            (bookingType == BookingType.SingleSession || s.PackId == planId))
                .ToListAsync();

            foreach (var session in staleSessions)
            {
                session.IsPending = false;
                _context.Sessions.Update(session);
                await _logService.LogInfo("CleanupStalePendingSessionsAsync",
                    $"Canceled stale or duplicate pending session Id: {session.Id} for User: {userEmail}, BookingType: {bookingType}, PlanId: {planId}, StripeSessionId: {session.StripeSessionId ?? "null"}, SessionCategory: {session.SessionCategory}, PreferredDateTime: {session.PreferredDateTime}");
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _logService.LogError("CleanupStalePendingSessionsAsync Error",
                $"Failed to cleanup stale sessions for User: {userEmail}, BookingType: {bookingType}, PlanId: {planId}. Error: {ex.Message}");
        }
    }

    private bool IsStripeSupportedCurrency(string currency)
    {
        var supportedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT", "BGN",
            "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD", "CDF", "CHF",
            "CLP", "CNY", "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP", "DZD", "EGP", "ERN", "ETB",
            "EUR", "FJD", "FKP", "FOK", "GBP", "GEL", "GGP", "GHS", "GIP", "GMD", "GNF", "GTQ", "GYD", "HKD",
            "HNL", "HRK", "HTG", "HUF", "IDR", "ILS", "IMP", "INR", "IQD", "IRR", "ISK", "JEP", "JMD", "JOD",
            "JPY", "KES", "KGS", "KHR", "KID", "KMF", "KRW", "KWD", "KYD", "KZT", "LAK", "LBP", "LKR", "LRD",
            "LSL", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN",
            "MYR", "MZN", "NAD", "NGN", "NIO", "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR",
            "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP",
            "SLE", "SOS", "SRD", "SSP", "STN", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP", "TRY", "TTD",
            "TVD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VES", "VND", "VUV", "WST", "XAF", "XCD",
            "XOF", "XPF", "YER", "ZAR", "ZMW"
        };
        return supportedCurrencies.Contains(currency.ToUpper());
    }
}