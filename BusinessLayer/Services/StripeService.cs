using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;
using ModelLayer.Models;

public class StripeService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly CoachingDbContext _context;
    private readonly ILogService _logService;
    private readonly IHelperService _helperService;

    public StripeService(IConfiguration configuration, CoachingDbContext context, ILogService logService, IHelperService helperService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        _helperService = helperService;
        StripeConfiguration.ApiKey = _helperService.GetConfigValue("Stripe:SecretKey");
    }

    public async Task<string> CreateCheckoutSessionAsync(ModelLayer.Models.Session session)
    {
        try
        {
            // Determine if this is a subscription or one-time payment
            var subscriptionPrice = await _context.SubscriptionPrices
                .FirstOrDefaultAsync(sp => sp.SessionType == session.SessionCategory);

            if (subscriptionPrice != null && !string.IsNullOrEmpty(subscriptionPrice.StripePriceId))
            {
                // Subscription checkout
                var options = new SessionCreateOptions
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
                        { "SessionId", session.Id.ToString() }
                    }
                };

                var stripeService = new SessionService();
                var stripeSession = await stripeService.CreateAsync(options);

                session.StripeSessionId = stripeSession.Id;
                session.IsPaid = false;
                session.CreatedAt = DateTime.UtcNow;

                _context.Sessions.Add(session);
                await _context.SaveChangesAsync();

                return stripeSession.Url;
            }
            else
            {
                // One-time payment (existing logic)
                var servicePrice = await _context.SessionPrices
                    .FirstOrDefaultAsync(sp => sp.SessionType == session.SessionCategory);

                if (servicePrice == null)
                {
                    await _logService.LogError("CreateCheckoutSessionAsync", $"No price found for session type {session.SessionCategory}");
                    throw new Exception("Service price not found.");
                }

                var options = new SessionCreateOptions
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
                        { "SessionId", session.Id.ToString() }
                    }
                };

                var stripeService = new SessionService();
                var stripeSession = await stripeService.CreateAsync(options);

                session.StripeSessionId = stripeSession.Id;
                session.IsPaid = false;
                session.CreatedAt = DateTime.UtcNow;

                _context.Sessions.Add(session);
                await _context.SaveChangesAsync();

                return stripeSession.Url;
            }
        }
        catch (StripeException ex)
        {
            await _logService.LogError("CreateCheckoutSessionAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            throw new Exception("Failed to create Stripe checkout session.");
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
            var stripeService = new SessionService();
            var session = await stripeService.GetAsync(stripeSessionId);

            if (session.PaymentStatus != "paid" && session.Status != "active")
            {
                await _logService.LogInfo("ConfirmPaymentAsync", $"Payment not completed for StripeSessionId: {stripeSessionId}, Status: {session.PaymentStatus}");
                return false;
            }

            var dbSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.StripeSessionId == stripeSessionId);

            if (dbSession == null)
            {
                await _logService.LogError("ConfirmPaymentAsync", $"Session not found for StripeSessionId: {stripeSessionId}");
                return false;
            }

            if (dbSession.IsPaid)
            {
                await _logService.LogInfo("ConfirmPaymentAsync", $"Session already paid for StripeSessionId: {stripeSessionId}");
                return true;
            }

            dbSession.IsPaid = true;
            dbSession.PaidAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }
        catch (StripeException ex)
        {
            await _logService.LogError("ConfirmPaymentAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
            return false;
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

                var videoSession = await _context.VideoSessions
                    .FirstOrDefaultAsync(vs => vs.SessionRefId == dbSession.Id);
                if (videoSession == null)
                {
                    videoSession = new VideoSession
                    {
                        UserId = dbSession.Email,
                        SessionId = Guid.NewGuid().ToString(),
                        ScheduledAt = dbSession.PreferredDateTime,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        Session = dbSession,
                        SessionRefId = dbSession.Id
                    };
                    _context.VideoSessions.Add(videoSession);
                    await _context.SaveChangesAsync();
                }

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