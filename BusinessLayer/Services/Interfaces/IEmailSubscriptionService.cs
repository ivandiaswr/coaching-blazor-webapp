using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces
{
    public interface IEmailSubscriptionService
    {
        List<EmailSubscription> GetAllEmailSubscriptions();
        Task<bool> SubscriptionAsync(EmailSubscription emailSubscription);
        Task<bool> UnsubscribeAsync(string email);
        Task<bool> SubscriptionGiftAsync(EmailSubscription emailSubscription);
        Task<bool> SendCustomEmailAsync(List<string> recipients, string subject, string body, List<(string Name, Stream Content, string ContentType)>? attachments = null);
        Task<bool> SendSimpleEmailAsync(string email, string subject, string htmlBody);
        Task<bool> DeleteEmailSubscriptionAsync(int id);
    }
}