using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using ModelLayer.Models;
namespace BusinessLayer.Services;

public class ContactService : IContactService
{
    private readonly CoachingDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContactService> _logger;
    private readonly IGoogleService _googleService;

    public ContactService(CoachingDbContext context, IConfiguration configuration, ILogger<ContactService> logger, IGoogleService googleService)
    {
        this._context = context;
        this._configuration = configuration;
        this._logger = logger;
        this._googleService = googleService;
    }

    public async Task<bool> ContactSubmitAsync(Contact contact, string userAccessToken)
    {
        if(contact is null)
            return false;

        try
        {
            _context.Contacts.Add(contact);

            //await SendEmailAsync(contact);

            await _googleService.CreateEventAdminAsync(
                $"Session with {contact.Name}",
                contact.Message,
                contact.PreferredDateTime,
                contact.PreferredDateTime.AddMinutes(30),
                "UTC",
                contact.Email
            );

            await _googleService.CreateEventUserAsync(
                userAccessToken,
                $"Session with {contact.Name}",
                contact.Message,
                contact.PreferredDateTime,
                contact.PreferredDateTime.AddMinutes(30),
                "UTC",
                contact.Email
            );

            await _context.SaveChangesAsync();
        } 
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error during ContactSubmitAsync");
             return false;
        }  

        return true;
    }

    public async Task SendEmailAsync(Contact contact)
    {
        var smtpServer = _configuration["SmtpSettings:Server"];
        var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "");
        var smtpUsername = _configuration["SmtpSettings:Username"];
        var smtpPassword = _configuration["SmtpSettings:Password"];

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
        {
            throw new InvalidOperationException($"SMTP settings are not properly configured. Server: {smtpServer}, Username: {smtpUsername}, Password: {(smtpPassword != null ? "****" : null)}");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Contact Form - Itala Veloso", smtpUsername));
        message.To.Add(new MailboxAddress("", smtpUsername));
        message.Subject = "Contact Form Submission - " + contact.Name;
        message.Body = new TextPart("plain")
        {
            Text = "Name: " + contact.Name + "\n" + 
                   "Email: " + contact.Email + "\n" + 
                   "Message: " + contact.Message + "\n" +
                   "TimeStamp: " + DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss")
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
            throw;
        }
    }
}
