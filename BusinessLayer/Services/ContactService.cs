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
    private readonly ILogger<ContactService> _logger;
    private readonly IGoogleService _googleService;
    private readonly IEmailSubscriptionService _emailSubscriptionService;
    private readonly ILogService _logService;

    public ContactService(CoachingDbContext context, 
        IHelperService helperService,
        ILogger<ContactService> logger,
        IGoogleService googleService,
        IEmailSubscriptionService emailSubscriptionService,
        ILogService logService)
    {
        this._context = context;
        this._helperService = helperService;
        this._logger = logger;
        this._googleService = googleService;
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
            _logger.LogError(ex, "Error during GetAllContacts");
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

            var CreateEventAdminAsyncGoogleMeetLink = await _googleService.CreateEventAdminAsync(contact);

            if(string.IsNullOrEmpty(CreateEventAdminAsyncGoogleMeetLink))
            {
                _logger.LogError("Failed to create admins event.");
                _logService.LogError("ContactSubmitAsync", "CreateEventAdminAsyncResult");
                return false;
            }

            var CreateEventIntervalAsyncResult = await _googleService.CreateEventIntervalAsync(contact);

            if(!CreateEventIntervalAsyncResult)
            {
                _logger.LogError("Failed to create users event.");
                 _logService.LogError("ContactSubmitAsync", "CreateEventIntervalAsyncResult");
                return false;
            }

            // if(!contact.Email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            // {
            //     await SendNonGmailEmailSync(contact, CreateEventAdminAsyncGoogleMeetLink); // send email to the user
            // } 

            await SendEmailAsync(contact); // send email to the admin

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ContactSubmitAsync");
            _logService.LogError("ContactSubmitAsync", ex.Message);
            return false;
        }  

        return true;
    }

    public async Task SendNonGmailEmailSync(Contact contact, string googleMeetLink)
    {
        string subject = $"Meeting Invitation with {contact.FullName}";
        string message = $@"            
            Thank you for scheduling a meeting with us. Below are the meeting details:
            
            Date and Time: {contact.PreferredDateTime.ToString("f")} (UTC)
            Google Meet Link: {googleMeetLink}
            
            Best regards,
            Ítala Veloso";

        await _emailSubscriptionService.SendCustomEmailAsync(new List<string> { contact.Email }, subject, message);
    }

    public async Task SendEmailAsync(Contact contact)
    {
        var smtpServer = _helperService.GetConfigValue("SmtpSettings:Server");
        var smtpPort = int.Parse(_helperService.GetConfigValue("SmtpSettings:Port") ?? "");
        var smtpUsername = _helperService.GetConfigValue("SmtpSettings:Username");
        var smtpPassword = _helperService.GetConfigValue("SmtpSettings:Password");

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
        {
            _logService.LogError("SendEmailAsync", $"SMTP settings are not properly configured. Server: {smtpServer}, Username: {smtpUsername}, Password: {(smtpPassword != null ? smtpPassword : null)}");
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
            _logger.LogError(ex, "Error during subscription");
            _logService.LogError("SendCustomEmailAsync", ex.Message);
            throw;
        }
    }
}
