using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface IEmailSubscriptionService
{
    List<EmailSubscription> GetAllEmailSubscriptions();
    Task<bool> SubscriptionAsync(string email);

    Task<bool> SubscriptionGiftAsync(string email, GiftCategory giftCategory);
    Task<bool> SendCustomEmailAsync(List<string> recipientEmails, string subject, string body);
}
