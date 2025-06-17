using ModelLayer.Models;
using ModelLayer.Models.Enums;

namespace BusinessLayer.Services.Interfaces{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(CheckoutSessionRequest session);
        Task<bool> ConfirmPaymentAsync(string stripeSessionId);
        Task HandleWebhookAsync(string json, string stripeSignature);
        Task<string> CreateOrUpdateSubscriptionPriceAsync(string productName, decimal amount, string sessionType, string currency = "GBP");
        Task CleanupStalePendingSessionsAsync(string userEmail, BookingType bookingType, string planId);
    }
}

