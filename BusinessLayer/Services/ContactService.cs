using System;
using System.Data.SqlTypes;
using System.Net;
using System.Net.Mail;
using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Microsoft.Extensions.Configuration;
using ModelLayer.Models;
namespace BusinessLayer.Services;

public class ContactService : IContactService
{
    private readonly CoachingDbContext _context;
    private readonly IConfiguration _configuration;

    public ContactService(CoachingDbContext context, IConfiguration configuration)
    {
        this._context = context;
        this._configuration = configuration;
    }

    public async Task<bool> HandleContactSubmitAsync(Contact contact)
    {
        
        if(contact is null) return false;

        try{
            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            await SendEmailAsync(contact);

            return true;
        } 
        catch (Exception ex)
        {
            
        }

        return false;
    }

    public async Task SendEmailAsync(Contact contact)
    {
        var smtpServer = _configuration["SmtpSettings:Server"];
        var smtpPort = int.Parse(_configuration["SmtpSettings:Port"]);
        var smtpUsername = _configuration["SmtpSettings:Username"];
        var smtpPassword = _configuration["SmtpSettings:Password"];

        using var client = new SmtpClient(smtpServer, smtpPort)
        {
            Credentials = new NetworkCredential(smtpUsername, smtpPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(smtpUsername),
            Subject = "New Contact Form Submission",
            Body = $"Name: {contact.Name}\nEmail: {contact.Email}\nMessage: {contact.Message}",
            IsBodyHtml = false,
        };

        mailMessage.To.Add(contact.Email);

        await client.SendMailAsync(mailMessage);
    }
}
