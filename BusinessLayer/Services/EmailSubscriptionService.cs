﻿using DataAccessLayer;
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

    public EmailSubscriptionService(CoachingDbContext context, IConfiguration configuration, ILogger<EmailSubscriptionService> logger)
    {
        this._context = context;
        this._configuration = configuration;
        this._logger = logger;
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
                var subscription = new EmailSubscription(){
                    Email = email
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

    public async Task<bool> SubscriptionGiftAsync(string email)
    {
        if(email is null){
            return false; // Email is null
        }

        var EmailSubscription = await _context.EmailSubscriptions.FirstOrDefaultAsync(e => e.Email == email);

        if(EmailSubscription != null)
        {
            if (EmailSubscription.IsSubscribed) // Already subscribed
            {
                try
                {
                    await SendEmailAsync(EmailSubscription.Email);
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
            }
        } 
        else 
        {
            try
            {    
                var subscription = new EmailSubscription(){
                    Email = email
                };
                
                _context.EmailSubscriptions.Add(subscription);

                // Guardou na bd com sucesso por isso envia email com o free gift 
                await SendEmailAsync(subscription.Email);
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

    public async Task SendEmailAsync(string email)
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
        message.From.Add(new MailboxAddress("Itala Veloso", smtpUsername));
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
        string pdfPath = Path.Combine(projectRoot, "coachingWebapp/wwwroot", "Files", "test.pdf");

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
}
