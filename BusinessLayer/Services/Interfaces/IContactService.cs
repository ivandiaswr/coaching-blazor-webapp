using ModelLayer.Models;

namespace BusinessLayer.Services.Interfaces;

public interface IContactService
{
    List<Contact> GetAllContacts();
    Task<bool> ContactSubmitAsync(Contact contact);
    Task SendEmailAsync(Contact contact);
}
