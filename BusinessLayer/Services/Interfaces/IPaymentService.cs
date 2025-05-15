using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest session);
        Task<bool> ConfirmPaymentAsync(string stripeSessionId);
        Task HandleWebhookAsync(string json, string stripeSignature);
        Task<string> CreateOrUpdateSubscriptionPriceAsync(string productName, decimal amount, string sessionType);
    }
}

