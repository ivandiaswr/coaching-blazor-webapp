using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using ModelLayer.Models;
namespace BusinessLayer.Services;

public class ContactService : IContactService
{
    private readonly CoachingDbContext _context;
    private readonly IHelperService _helperService;
    private readonly IEmailSubscriptionService _emailSubscriptionService;
    private readonly ILogService _logService;

    public ContactService(CoachingDbContext context, 
        IHelperService helperService,
        IEmailSubscriptionService emailSubscriptionService,
        ILogService logService)
    {
        this._context = context;
        this._helperService = helperService;
        this._emailSubscriptionService = emailSubscriptionService;
        this._logService = logService;
    }

    public List<Contact> GetAllContacts()
    {
        try
        {
            var contacts = _context.Contacts.ToList();

            return contacts;
        } 
        catch(Exception ex)
        {
            _logService.LogError("GetAllContacts", ex.Message);
             throw;
        }
    }

    public async Task<bool> ContactSubmitAsync(Contact contact)
    {
        if(contact is null)
            return false;

        try
        {
            contact.CreatedAt = DateTime.UtcNow;
            contact.UpdateFullName();

            _context.Contacts.Add(contact);

            await SendEmailAsync(contact); // send email to the admin

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await _logService.LogError("Error during ContactSubmitAsync", ex.Message);

            await _emailSubscriptionService.SendCustomEmailAsync(
                new List<string>
                {
                    _helperService.GetConfigValue("AdminEmail:Primary"),
                    _helperService.GetConfigValue("AdminEmail:Secondary")
                },
                "Schedule Error",
                @$"Exception: {ex}<br>Message: {ex.Message}"
            );

            return false;
        }  

        return true;
    }

    public async Task SendEmailAsync(Contact contact)
    {
        var smtpServer = _helperService.GetConfigValue("SmtpSettings:Server");
        var smtpPort = int.Parse(_helperService.GetConfigValue("SmtpSettings:Port") ?? "");
        var smtpUsername = _helperService.GetConfigValue("SmtpSettings:Username");
        var smtpPassword = _helperService.GetConfigValue("SmtpSettings:Password");

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
        {
            await _logService.LogError("SendEmailAsync", $"SMTP settings are not properly configured. Server: {smtpServer}, Username: {smtpUsername}, Password: {(smtpPassword != null ? smtpPassword : null)}");
            throw new InvalidOperationException($"SMTP settings are not properly configured. Server: {smtpServer}, Username: {smtpUsername}, Password: {(smtpPassword != null ? "****" : null)}");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Meeting Scheduled", smtpUsername));
        message.To.Add(new MailboxAddress("", smtpUsername));
        message.Subject = $"Meeting Scheduled - {contact.FullName}";
        message.Body = new TextPart("plain")
        {
            Text = $"Name: {contact.FullName}\n" + 
                   "Email: " + contact.Email + "\n" +
                   "Session Category: " + contact.SessionCategory + "\n" +  
                   "Message: " + contact.Message + "\n" +
                   "Preferred Meeting Date: " + contact.PreferredDateTime
        };

       using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUsername, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            await _logService.LogError("SendCustomEmailAsync", ex.Message);
            throw;
        }
    }
}
