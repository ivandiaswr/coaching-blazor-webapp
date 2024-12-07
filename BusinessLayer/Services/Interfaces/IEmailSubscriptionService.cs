using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface IEmailSubscriptionService
{
    List<EmailSubscription> GetAllEmailSubscriptions();
    Task<bool> SubscriptionAsync(EmailSubscription emailSubscription);
    Task<bool> UnsubscribeAsync(string email);
    Task<bool> SubscriptionGiftAsync(EmailSubscription emailSubscription);
    Task<bool> SendCustomEmailAsync(List<string> recipientEmails, string subject, string body);
}
