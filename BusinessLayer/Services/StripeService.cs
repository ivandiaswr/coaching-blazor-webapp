using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Stripe.Checkout;

public class StripeService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly CoachingDbContext _context;

    public StripeService(IConfiguration configuration, CoachingDbContext context)
    {
        this._configuration = configuration;
        this._context = context;
        // stipe key
    }

    public async Task<string> CreateCheckoutSessionAsync(ModelLayer.Models.Session session)
    {
        var servicePrice = await _context.SessionPrices.FirstOrDefaultAsync(sp => sp.SessionType == session.SessionCategory);

        if(servicePrice is null)
            throw new Exception("Service price not found.");

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
                            Name = "Coaching Session: " + session.Id,
                        },
                        UnitAmountDecimal = (long)(servicePrice.PriceEUR * 100),
                    },
                    Quantity = 1,
                },
            },
            Mode = "payment",
            SuccessUrl = $"{_configuration["App:BaseUrl"]}/payment-success?sessionId={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_configuration["App:BaseUrl"]}/payment-cancelled"
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

    public async Task<bool> ConfirmPaymentAsync(string stripeSessionId)
    {
        var stripeService = new SessionService();
        var session = await stripeService.GetAsync(stripeSessionId);

        if (session.PaymentStatus == "paid")
        {
            var dbSession = await _context.Sessions.FirstOrDefaultAsync(s => s.StripeSessionId == stripeSessionId);

            if (dbSession != null)
            {
                dbSession.IsPaid = true;
                dbSession.PaidAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return true;
            }
        } 
        
        return false;
    }
}