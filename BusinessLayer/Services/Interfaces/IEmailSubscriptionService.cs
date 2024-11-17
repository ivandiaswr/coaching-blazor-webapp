namespace BusinessLayer.Services.Interfaces;

public interface IEmailSubscriptionService
{
    Task<bool> SubscriptionAsync(string email);

    Task<bool> SubscriptionGiftAsync(string email, GiftCategory giftCategory);
}
