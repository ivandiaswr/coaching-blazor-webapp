using System.Net;
using System.Net.Mail;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;
using Microsoft.Extensions.Configuration;
using BusinessLayer.Services.Interfaces;

namespace BusinessLayer;

public class EmailSubscriptionService : IEmailSubscriptionService
{
    private readonly CoachingDbContext _context;
    private readonly IConfiguration _configuration;

    public EmailSubscriptionService(CoachingDbContext context)
    {
        this._context = context;
    }
    
    public async Task<bool> SubscriptionAsync(string email)
    {
        if(email is null){
            return false; // Email is null
        }

        var EmailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

        if(EmailSubscription != null)
        {
            if (EmailSubscription.IsSubscribed)
            {
                return false; // Already subscribed
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
            var subscription = new EmailSubscription(){
                Email = email
            };

            _context.EmailSubscriptions.Add(subscription);
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SubscriptionGiftAsync(string email)
    {
        if(email is null){
            return false; // Email is null
        }

        var EmailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

        if(EmailSubscription != null)
        {
            if (EmailSubscription.IsSubscribed)
            {
                return false; // Already subscribed
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
            var subscription = new EmailSubscription(){
                Email = email
            };

            _context.EmailSubscriptions.Add(subscription);

            // Guardou na bd com sucesso por isso envia email com o free gift 
            await SendEmailAsync(subscription.Email);
        }

        await _context.SaveChangesAsync();

        

        return true;
    }

    public async Task SendEmailAsync(string email)
    {
        var smtpServer = _configuration["SmtpSettings:Server"];
        var smtpPort = int.Parse(_configuration["SmtpSettings:Port"]);
        var smtpUsername = _configuration["SmtpSettings:Username"];
        var smtpPassword = _configuration["SmtpSettings:Password"];
        var smtpMailTo = email;

        using var client = new SmtpClient(smtpServer, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(smtpUsername),
            Subject = "Gift from Jostic",
            Body = $"TESTEEEEEEEEEEEee",
            IsBodyHtml = false,
        };

        mailMessage.To.Add(smtpMailTo);

        await client.SendMailAsync(mailMessage);
    }
}
