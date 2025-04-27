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

    public StripeService(IConfiguration configuration, CoachingDbContext context, ILogService logService)
    {
        _configuration = configuration;
        _context = context;
        _logService = logService;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateCheckoutSessionAsync(ModelLayer.Models.Session session)
    {
        try
        {
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
                SuccessUrl = $"{_configuration["App:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_configuration["App:BaseUrl"]}/payment-cancelled",
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
        catch (StripeException ex)
        {
            await _logService.LogError("CreateChkoutSessionAsync Stripe Error", $"Error: {ex.Message}, StripeError: {ex.StripeError?.Message}");
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

            if (session.PaymentStatus != "paid")
            {
                await _logService.LogInfo($"ConfirmPaymentAsync -> Payment not completed for StripeSessionId: {stripeSessionId}, Status: {session.PaymentStatus}");
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
                await _logService.LogInfo($"ConfirmPaymentAsync -> Session already paid for StripeSessionId: {stripeSessionId}");
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
            var webhookSecret = _configuration["Stripe:WebhookSecret"];
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

                // Create VideoSession if not already created
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

                    // SessionService.CreateSessionAsync already sends confirmation email
                    // but i can trigger it here if needed 
                }

                await _logService.LogInfo($"HandleWebhookAsync-> Successfully processed checkout.session.completed for StripeSessionId: {checkoutSession.Id}");
            }
            else
            {
                await _logService.LogInfo($"HandleWebhookAsync -> Unhandled event type: {stripeEvent.Type}");
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
}