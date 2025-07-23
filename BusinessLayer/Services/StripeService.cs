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
    private readonly IUserSubscriptionService _userSubscriptionService;

    public StripeService(IConfiguration configuration,
        CoachingDbContext context,
        ILogService logService,
        IHelperService helperService,
        ISessionService sessionService,
        ISessionPackService sessionPackService,
        ICurrencyConversionService currencyConversionService,
        IUserSubscriptionService userSubscriptionService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        _helperService = helperService;
        _sessionService = sessionService;
        _sessionPackService = sessionPackService;
        _currencyConversionService = currencyConversionService;
        _userSubscriptionService = userSubscriptionService;
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

            // Add a small delay to ensure any previous Stripe operations have completed
            await Task.Delay(500);

            await CleanupStalePendingSessionsAsync(session.Email, bookingType, planId ?? string.Empty, session.SessionCategory.ToString(), session.PreferredDateTime);
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

                string stripePriceId = packPrice.StripePriceId;
                if (string.IsNullOrEmpty(stripePriceId))
                {
                    stripePriceId = await CreateOrUpdateSessionPackPriceAsync(
                        packPrice.Name,
                        priceValue,
                        packPrice.SessionType.ToString(),
                        packPrice.TotalSessions,
                        userCurrency
                    );
                    packPrice.StripePriceId = stripePriceId;
                    _context.SessionPackPrices.Update(packPrice);
                    await _context.SaveChangesAsync();
                }

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
                    Mode = "payment",
                    SuccessUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled?sessionId={session.Id}",
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

                // For subscriptions, planId is the Stripe Price ID, not the database ID
                SubscriptionPrice subscriptionPrice;

                if (planId.StartsWith("price_")) // It's a Stripe Price ID
                {
                    subscriptionPrice = await _context.SubscriptionPrices
                        .FirstOrDefaultAsync(sp => sp.StripePriceId == planId && sp.SessionType == session.SessionCategory);
                }
                else if (int.TryParse(planId, out var priceId)) // It's a database ID
                {
                    subscriptionPrice = await _context.SubscriptionPrices
                        .FirstOrDefaultAsync(sp => sp.Id == priceId && sp.SessionType == session.SessionCategory);
                }
                else
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"Invalid PlanId format: {planId}");
                    throw new ArgumentException("Invalid PlanId format.");
                }

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

                string stripePriceId = subscriptionPrice.StripePriceId;
                if (string.IsNullOrEmpty(stripePriceId))
                {
                    stripePriceId = await CreateOrUpdateSubscriptionPriceAsync(
                        subscriptionPrice.Name,
                        priceInUserCurrency,
                        subscriptionPrice.SessionType.ToString(),
                        userCurrency
                    );
                    subscriptionPrice.StripePriceId = stripePriceId;
                    _context.SubscriptionPrices.Update(subscriptionPrice);
                    await _context.SaveChangesAsync();
                }

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
                    CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled?sessionId={session.Id}",
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
                                        Name = $"Single Session - {session.SessionCategory.GetDisplayName()}",
                                        Description = "A one-time personalized coaching session tailored to your goals."
                                    },
                                    UnitAmountDecimal = (long)(priceInUserCurrency * 100),
                                },
                                Quantity = 1,
                            },
                        },
                        Mode = "payment",
                        SuccessUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                        CancelUrl = $"{_configuration["AppSettings:BaseUrl"]}/payment-cancelled?sessionId={session.Id}",
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

    public async Task<bool> ConfirmPaymentAsync(string sessionId)
    {
        try
        {
            await _logService.LogInfo("ConfirmPaymentAsync", $"Starting payment confirmation for sessionId: {sessionId}");

            var existingSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.StripeSessionId == sessionId);

            if (existingSession != null && existingSession.IsPaid && !existingSession.IsPending)
            {
                await _logService.LogInfo("ConfirmPaymentAsync", $"Session {sessionId} already processed successfully");
                return true;
            }

            var service = new SessionService();
            var stripeSession = await service.GetAsync(sessionId);

            if (stripeSession.PaymentStatus != "paid")
            {
                await _logService.LogError("ConfirmPaymentAsync", $"Payment not completed. Status: {stripeSession.PaymentStatus}");
                return false;
            }

            if (!stripeSession.Metadata.TryGetValue("SessionId", out var dbSessionId) ||
                !int.TryParse(dbSessionId, out var sessionIdInt))
            {
                await _logService.LogError("ConfirmPaymentAsync", "Invalid or missing SessionId in metadata");
                return false;
            }

            var dbSession = await _context.Sessions.FindAsync(sessionIdInt);
            if (dbSession == null)
            {
                await _logService.LogError("ConfirmPaymentAsync", $"Session not found in database: {sessionIdInt}");
                return false;
            }

            dbSession.IsPaid = true;
            dbSession.PaidAt = DateTime.UtcNow;
            dbSession.IsPending = false;
            dbSession.StripeSessionId = sessionId;

            var bookingType = Enum.Parse<BookingType>(stripeSession.Metadata["BookingType"]);
            var planId = stripeSession.Metadata.GetValueOrDefault("PlanId");

            await _logService.LogInfo("ConfirmPaymentAsync", $"Processing {bookingType} payment for PlanId: {planId}");

            if (bookingType == BookingType.Subscription && !string.IsNullOrEmpty(planId))
            {
                SubscriptionPrice subscriptionPrice;

                if (planId.StartsWith("price_"))
                {
                    subscriptionPrice = await _context.SubscriptionPrices
                        .FirstOrDefaultAsync(sp => sp.StripePriceId == planId);
                }
                else if (int.TryParse(planId, out var priceId))
                {
                    subscriptionPrice = await _context.SubscriptionPrices
                        .FirstOrDefaultAsync(sp => sp.Id == priceId);
                }
                else
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Invalid PlanId format: {planId}");
                    return false;
                }

                if (subscriptionPrice == null)
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Subscription price not found for PlanId: {planId}");
                    return false;
                }

                var stripeSubscriptionId = stripeSession.SubscriptionId;
                if (string.IsNullOrEmpty(stripeSubscriptionId))
                {
                    await _logService.LogError("ConfirmPaymentAsync", "No subscription ID found in Stripe session");
                    return false;
                }

                var existingSubscription = await _context.UserSubscriptions
                    .FirstOrDefaultAsync(s => s.UserId == dbSession.Email &&
                                        s.PriceId == subscriptionPrice.Id &&
                                        s.IsActive);

                if (existingSubscription == null)
                {
                    var newSubscription = new UserSubscription
                    {
                        UserId = dbSession.Email,
                        PriceId = subscriptionPrice.Id,
                        StripeSubscriptionId = stripeSubscriptionId,
                        IsActive = true,
                        SessionsUsedThisMonth = 1,
                        CurrentPeriodStart = DateTime.UtcNow,
                        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                        StartedAt = DateTime.UtcNow
                    };

                    _context.UserSubscriptions.Add(newSubscription);
                    await _logService.LogInfo("ConfirmPaymentAsync", $"Created new subscription: {stripeSubscriptionId} with first session consumed");
                }
                else
                {
                    var usageRegistered = await _userSubscriptionService.RegisterMonthlyUsage(dbSession.Email, stripeSubscriptionId);
                    if (usageRegistered)
                    {
                        await _logService.LogInfo("ConfirmPaymentAsync", $"Registered monthly usage for existing subscription: {existingSubscription.StripeSubscriptionId}");
                    }
                    else
                    {
                        await _logService.LogWarning("ConfirmPaymentAsync", $"Failed to register monthly usage for existing subscription: {existingSubscription.StripeSubscriptionId}");
                    }
                }

                dbSession.PackId = stripeSubscriptionId;
            }
            else if (bookingType == BookingType.SessionPack && !string.IsNullOrEmpty(planId))
            {
                if (int.TryParse(planId, out var packId))
                {
                    var packPrice = await _context.SessionPackPrices.FindAsync(packId);
                    if (packPrice != null)
                    {
                        await _logService.LogInfo("ConfirmPaymentAsync", $"Processing pack: {packPrice.Name}");

                        var userSessionPack = new SessionPack
                        {
                            UserId = dbSession.Email,
                            PriceId = packPrice.Id,
                            SessionsRemaining = packPrice.TotalSessions - 1,
                            PurchasedAt = DateTime.UtcNow,
                        };

                        _context.SessionPacks.Add(userSessionPack);
                        await _logService.LogInfo("ConfirmPaymentAsync", $"Created session pack for user {dbSession.Email}: {packPrice.Name} with {packPrice.TotalSessions} sessions, {userSessionPack.SessionsRemaining} remaining after consuming one for scheduled session");

                        dbSession.PackId = userSessionPack.Id.ToString();
                    }
                    else
                    {
                        await _logService.LogError("ConfirmPaymentAsync", $"Session pack price not found for PlanId: {planId}");
                        return false;
                    }
                }
                else
                {
                    await _logService.LogError("ConfirmPaymentAsync", $"Invalid pack PlanId format: {planId}");
                    return false;
                }
            }

            await _context.SaveChangesAsync();
            await _logService.LogInfo("ConfirmPaymentAsync", $"Payment confirmation completed successfully for session: {sessionIdInt}");

            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogError("ConfirmPaymentAsync", $"Error: {ex.Message}, StackTrace: {ex.StackTrace}");
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
                        if (checkoutSession.Metadata != null &&
                            checkoutSession.Metadata.TryGetValue("SessionId", out var sessionIdStr) &&
                            int.TryParse(sessionIdStr, out var sessionId))
                        {
                            dbSession = await _context.Sessions.FindAsync(sessionId);
                            if (dbSession != null)
                            {
                                dbSession.StripeSessionId = checkoutSession.Id;
                                await _logService.LogInfo("HandleWebhookAsync", $"Updated session {sessionId} with StripeSessionId: {checkoutSession.Id}");
                            }
                        }
                    }

                    if (dbSession == null)
                    {
                        await _logService.LogError("HandleWebhookAsync", $"Session not found for StripeSessionId: {checkoutSession.Id}");
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

            var productService = new ProductService();
            var productListOptions = new ProductListOptions { Limit = 100, Active = true };
            var products = await productService.ListAsync(productListOptions);
            var existingProduct = products.Data.FirstOrDefault(p =>
                p.Metadata.TryGetValue("SessionType", out var st) && st == sessionType &&
                p.Metadata.TryGetValue("Currency", out var curr) && curr.ToUpper() == currency.ToUpper());

            string productId;
            if (existingProduct != null)
            {
                productId = existingProduct.Id;
                await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                    $"Reusing existing Product ID: {productId} for SessionType: {sessionType}, Currency: {currency}");
            }
            else
            {
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
                productId = product?.Id ?? throw new InvalidOperationException("Failed to create Stripe product.");
                await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                    $"Created new Product ID: {productId} for SessionType: {sessionType}, Currency: {currency}");
            }

            var priceService = new PriceService();
            var priceListOptions = new PriceListOptions { Product = productId, Active = true, Limit = 100 };
            var prices = await priceService.ListAsync(priceListOptions);
            var existingPrice = prices.Data.FirstOrDefault(p =>
                p.UnitAmountDecimal == (long)(amount * 100) && p.Currency.ToUpper() == currency.ToUpper());

            string stripePriceId;
            if (existingPrice != null)
            {
                stripePriceId = existingPrice.Id;
                await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                    $"Using existing active Price ID: {stripePriceId}");
            }
            else
            {
                var priceOptions = new PriceCreateOptions
                {
                    UnitAmount = (long)(amount * 100),
                    Currency = currency.ToLower(),
                    Recurring = new PriceRecurringOptions { Interval = "month" },
                    Product = productId,
                    Metadata = new Dictionary<string, string>
                        {
                            { "SessionType", sessionType },
                            { "Currency", currency }
                        }
                };
                var price = await priceService.CreateAsync(priceOptions);
                stripePriceId = price?.Id ?? throw new InvalidOperationException("Failed to create Stripe price.");
                await _logService.LogInfo("CreateOrUpdateSubscriptionPriceAsync",
                    $"Created Price ID: {stripePriceId} for Product: {productName}, Amount: {amount} {currency}");
            }

            return stripePriceId;
        }
        catch (StripeException ex)
        {
            await _logService.LogError("CreateOrUpdateSubscriptionPriceAsync",
                $"Stripe error for {productName}: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateOrUpdateSubscriptionPriceAsync",
                $"Failed to create or update price for {productName}. Error: {ex.Message}, StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<string> CreateOrUpdateSessionPackPriceAsync(string productName, decimal amount, string sessionType, int totalSessions, string currency = "GBP")
    {
        try
        {
            if (string.IsNullOrEmpty(StripeConfiguration.ApiKey))
            {
                throw new InvalidOperationException("Stripe API key is not configured.");
            }

            await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                $"Processing price for {productName}, Amount: {amount}, Currency: {currency}, SessionType: {sessionType}, TotalSessions: {totalSessions}");

            SessionPackPrice? existingPrice = await _context.SessionPackPrices
                .FirstOrDefaultAsync(sp => sp.SessionType.ToString() == sessionType && sp.PriceGBP == amount && sp.TotalSessions == totalSessions);

            string stripePriceId = existingPrice?.StripePriceId;
            if (existingPrice != null && !string.IsNullOrEmpty(stripePriceId))
            {
                var existsPriceService = new PriceService();
                var stripePrice = await existsPriceService.GetAsync(stripePriceId);
                if (stripePrice != null)
                {
                    if (stripePrice.Active && stripePrice.UnitAmountDecimal == (long)(amount * 100) &&
                        stripePrice.Currency.ToUpper() == currency.ToUpper() && stripePrice.Product.Name == productName &&
                        stripePrice.Metadata.TryGetValue("TotalSessions", out var totalSessionsMetadata) &&
                        int.TryParse(totalSessionsMetadata, out var parsedTotalSessions) && parsedTotalSessions == totalSessions)
                    {
                        await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                            $"Using existing active Price ID: {stripePriceId}");
                        return stripePriceId;
                    }
                    await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                        $"Existing price mismatch or inactive for ID: {stripePriceId}, creating new.");
                }
                else
                {
                    await _logService.LogWarning("CreateOrUpdateSessionPackPriceAsync",
                        $"Failed to retrieve Stripe price for ID: {stripePriceId}, creating new.");
                }
            }

            var productService = new ProductService();
            var productListOptions = new ProductListOptions { Limit = 100, Active = true };
            var products = await productService.ListAsync(productListOptions);
            var existingProduct = products.Data.FirstOrDefault(p =>
                p.Metadata.TryGetValue("SessionType", out var st) && st == sessionType &&
                p.Metadata.TryGetValue("Currency", out var curr) && curr.ToUpper() == currency.ToUpper() &&
                p.Name == productName);

            string productId;
            if (existingProduct != null)
            {
                productId = existingProduct.Id;
                await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                    $"Reusing existing Product ID: {productId} for SessionType: {sessionType}, Currency: {currency}");
            }
            else
            {
                var productOptions = new ProductCreateOptions
                {
                    Name = productName,
                    Description = $"Session Pack for {sessionType} ({totalSessions} sessions) in {currency}",
                    Metadata = new Dictionary<string, string>
                    {
                        { "SessionType", sessionType },
                        { "Currency", currency },
                        { "TotalSessions", totalSessions.ToString() }
                    }
                };
                var product = await productService.CreateAsync(productOptions);
                productId = product?.Id ?? throw new InvalidOperationException("Failed to create Stripe product.");
                await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                    $"Created new Product ID: {productId} for SessionType: {sessionType}, Currency: {currency}, TotalSessions: {totalSessions}");
            }

            var priceService = new PriceService();
            var priceOptions = new PriceCreateOptions
            {
                UnitAmount = (long)(amount * 100),
                Currency = currency.ToLower(),
                Product = productId,
                Metadata = new Dictionary<string, string>
                {
                    { "SessionType", sessionType },
                    { "Currency", currency },
                    { "TotalSessions", totalSessions.ToString() }
                }
            };
            var price = await priceService.CreateAsync(priceOptions);
            if (price == null) throw new InvalidOperationException("Failed to create Stripe price.");

            if (existingPrice != null)
            {
                existingPrice.StripePriceId = price.Id;
                _context.SessionPackPrices.Update(existingPrice);
            }
            else
            {
                var sessionPackPrice = new SessionPackPrice
                {
                    SessionType = (SessionType)Enum.Parse(typeof(SessionType), sessionType),
                    Name = productName,
                    PriceGBP = amount,
                    StripePriceId = price.Id,
                    TotalSessions = totalSessions
                };
                _context.SessionPackPrices.Add(sessionPackPrice);
            }

            await _context.SaveChangesAsync();
            await _logService.LogInfo("CreateOrUpdateSessionPackPriceAsync",
                $"Created Price ID: {price.Id} for Product: {productName}, Amount: {amount} {currency}, TotalSessions: {totalSessions}");
            return price.Id;
        }
        catch (StripeException ex)
        {
            await _logService.LogError("CreateOrUpdateSessionPackPriceAsync",
                $"Stripe error for {productName}: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateOrUpdateSessionPackPriceAsync",
                $"Failed to create or update price for {productName}. Error: {ex.Message}, StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task CleanupStalePendingSessionsAsync(string userEmail, BookingType bookingType, string planId, string sessionCategory, DateTime preferredDateTime)
    {
        try
        {
            var staleThreshold = DateTime.UtcNow.AddHours(-12);

            // Use a more specific query to avoid conflicts
            var staleSessions = await _context.Sessions
                .Where(s => s.Email == userEmail &&
                            s.IsPending &&
                            s.CreatedAt < staleThreshold &&
                            (bookingType == BookingType.SingleSession || s.PackId == planId))
                .ToListAsync();

            if (staleSessions.Any())
            {
                await _logService.LogInfo("CleanupStalePendingSessionsAsync",
                    $"Found {staleSessions.Count} stale sessions for cleanup for User: {userEmail}");

                foreach (var session in staleSessions)
                {
                    try
                    {
                        session.IsPending = false;
                        _context.Sessions.Update(session);
                        await _logService.LogInfo("CleanupStalePendingSessionsAsync",
                            $"Marked stale session as not pending - Id: {session.Id}, StripeSessionId: {session.StripeSessionId ?? "null"}");
                    }
                    catch (Exception sessionEx)
                    {
                        await _logService.LogError("CleanupStalePendingSessionsAsync",
                            $"Failed to update individual session {session.Id}: {sessionEx.Message}");
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                    await _logService.LogInfo("CleanupStalePendingSessionsAsync",
                        $"Successfully cleaned up {staleSessions.Count} stale sessions for User: {userEmail}");
                }
                catch (Exception saveEx)
                {
                    await _logService.LogError("CleanupStalePendingSessionsAsync",
                        $"Failed to save cleanup changes for User: {userEmail}: {saveEx.Message}");
                    throw;
                }
            }
            else
            {
                await _logService.LogInfo("CleanupStalePendingSessionsAsync",
                    $"No stale sessions found for User: {userEmail}, BookingType: {bookingType}");
            }
        }
        catch (Exception ex)
        {
            await _logService.LogError("CleanupStalePendingSessionsAsync Error",
                $"Failed to cleanup stale sessions for User: {userEmail}, BookingType: {bookingType}, PlanId: {planId}. Error: {ex.Message}");
            throw;
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