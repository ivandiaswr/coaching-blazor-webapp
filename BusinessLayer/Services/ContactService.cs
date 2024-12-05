using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using ModelLayer.Models;
using System.CodeDom;
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

            var CreateEventAdminAsyncResult = await _googleService.CreateEventAdminAsync(contact);

            if(!CreateEventAdminAsyncResult)
            {
                _logger.LogError("Failed to create admin's event.");
                return false;
            }

            var CreateEventIntervalAsyncResult = await _googleService.CreateEventIntervalAsync(contact);

            if(!CreateEventIntervalAsyncResult)
            {
                _logger.LogError("Failed to create admin's event.");
                return false;
            }

            await SendEmailAsync(contact);

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
            throw;
        }
    }
}
