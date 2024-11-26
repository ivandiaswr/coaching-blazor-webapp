using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using Microsoft.Extensions.Configuration;
using BusinessLayer.Services.Interfaces;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BusinessLayer;

public class EmailSubscriptionService : IEmailSubscriptionService
{
    private readonly CoachingDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSubscriptionService> _logger;
    private readonly ISecurityService _securityService;

    public EmailSubscriptionService(CoachingDbContext context, IConfiguration configuration, ILogger<EmailSubscriptionService> logger, ISecurityService securityService)
    {
        this._context = context;
        this._configuration = configuration;
        this._logger = logger;
        this._securityService = securityService;
    }

    public List<EmailSubscription> GetAllEmailSubscriptions()
    {
        try
        {
            var emailSubscription = _context.EmailSubscriptions.ToList();

            return emailSubscription;
        }
        catch(Exception ex)
        {
             _logger.LogError(ex, "Error during GetAllEmailSubscriptions");
             throw;
        }
    }
    
    public async Task<bool> SubscriptionAsync(string email)
    {
        if(email is null)
            return false; 

        var EmailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

        if(EmailSubscription != null)
        {
            if (EmailSubscription.IsSubscribed)
            {
                return true; // Already subscribed
            }
            else
            {
                // Resubscribe
                EmailSubscription.IsSubscribed = true;
                EmailSubscription.SubscribedAt = DateTime.UtcNow;
            }
        } 
        else 
        {
            try 
            {
                var subscription = new EmailSubscription()
                {
                    Email = email,
                    IsSubscribed = true,
                    SubscribedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EmailSubscriptions.Add(subscription);
            } 
            catch (Exception ex){
                _logger.LogError(ex, "Error during subscription");
                return false;
            }
        }

        await _context.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("UnsubscribeAsync called with a null or empty email.");
            return false;
        }

        try
        {
            var emailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

            if(emailSubscription is null)
            {
                _logger.LogError($"Error during UnsubscribeAsync: No subscription found for email {email}");
                return false;
            }

            emailSubscription.IsSubscribed = false;
            emailSubscription.UnsubscribedAt = DateTime.UtcNow;

            // Dont need to call Update method cause EF tracks the changes
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during UnsubscribeAsync");
            return false;
        }
    }

    public async Task<bool> SubscriptionGiftAsync(string email, GiftCategory giftCategory)
    {
        if(email is null)
            return false; 

        var EmailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

        if(EmailSubscription != null)
        {
            if (EmailSubscription.IsSubscribed)
            {
                try
                {
                    await SendEmailAsync(EmailSubscription.Email, giftCategory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during subscription gift when IsSubscribed");
                    return false;
                }

                return true; 
            }
            else
            {
                // Resubscribe
                EmailSubscription.IsSubscribed = true;
                EmailSubscription.SubscribedAt = DateTime.UtcNow;
                EmailSubscription.Gift = giftCategory;
            }
        } 
        else 
        {
            try
            {    
                var subscription = new EmailSubscription(){
                    Email = email,
                    Gift = giftCategory,
                    IsSubscribed = true
                };
                
                _context.EmailSubscriptions.Add(subscription);

                // Saves into db with success and sends email with a free gift 
                await SendEmailAsync(subscription.Email, giftCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during subscription gift when IsSubscribed");
                return false;
            };
        }

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task SendEmailAsync(string email, GiftCategory giftCategory)
    {
        var smtpServer = _configuration["SmtpSettings:Server"];
        var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "");
        var smtpUsername = _configuration["SmtpSettings:Username"];
        var smtpPassword = _configuration["SmtpSettings:Password"];
        var smtpMailTo = email;

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
        {
            throw new InvalidOperationException($"SMTP settings are not properly configured. Server: {smtpServer}, Username: {smtpUsername}, Password: {(smtpPassword != null ? "****" : null)}");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Ítala Veloso", smtpUsername));
        message.To.Add(new MailboxAddress("", smtpMailTo));
        message.Subject = "Gift from Jostic";

        var builder = new BodyBuilder();
        builder.TextBody = "Here's your free gift from Jostic!";


        // Construct the correct path to the PDF file
        string projectRoot = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(projectRoot, "coachingWebapp")))
        {
            projectRoot = Directory.GetParent(projectRoot).FullName;
        }
        string pdfPath = string.Empty;

        if (giftCategory == GiftCategory.Gift1)
        {
            pdfPath = Path.Combine(projectRoot, "coachingWebapp/wwwroot", "Files", "Reclaim & Regain Inner  Peace - Ítala Veloso.pdf");
        } else if (giftCategory == GiftCategory.Gift2)
        {
            pdfPath = Path.Combine(projectRoot, "coachingWebapp/wwwroot", "Files", "Stress Free - Itala Veloso.pdf");
        }

        // Check if the file exists
        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException($"The PDF file was not found at path: {pdfPath}");
        }

        // Add the PDF file to the email
        builder.Attachments.Add(pdfPath);

        message.Body = builder.ToMessageBody();

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
            _logger.LogError($"SMTP Error: {ex.Message}");
            _logger.LogError($"SMTP Server: {smtpServer}");
            _logger.LogError($"SMTP Port: {smtpPort}");
            _logger.LogError($"SMTP Username: {smtpUsername}");
            throw;
        }
    }

    public async Task<bool> SendCustomEmailAsync(List<string> recipientEmails, string subject, string body)
    {
        if (recipientEmails == null || !recipientEmails.Any())
            throw new ArgumentException("Recipient list is empty.");
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Email subject is required.");
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Email body is required.");

        var smtpServer = _configuration["SmtpSettings:Server"];
        var smtpPort = int.Parse(_configuration["SmtpSettings:Port"] ?? "");
        var smtpUsername = _configuration["SmtpSettings:Username"];
        var smtpPassword = _configuration["SmtpSettings:Password"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Ítala Veloso", smtpUsername));
        message.Subject = subject;

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUsername, smtpPassword);

            foreach (var email in recipientEmails)
            {
                // Generate Unsubscribe Token
                var token = _securityService.GenerateUnsubscribeToken(email);
                string unsubscribeUrl = $"{_configuration["AppSettings:BaseUrl"]}/unsubscribe?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

                var builder = new BodyBuilder
                {
                    HtmlBody = $"{body}<br><p>If you wish to unsubscribe, click <a href='{unsubscribeUrl}'>here</a>.</p>"

                };

                message.Body = builder.ToMessageBody();

                message.To.Clear();
                message.To.Add(MailboxAddress.Parse(email));
                
                await client.SendAsync(message);
            }

            await client.DisconnectAsync(true);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending custom emails");
            throw;
        }
    }
}
