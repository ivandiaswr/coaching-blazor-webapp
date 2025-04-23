using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSessionAsync(Session session);
        Task<bool> ConfirmPaymentAsync(string stripeSessionId);
    }
}

