using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;
using ModelLayer.Models;
using Microsoft.EntityFrameworkCore;
namespace BusinessLayer.Services;

public class SessionService : ISessionService
{
    private readonly CoachingDbContext _context;
    private readonly IHelperService _helperService;
    private readonly IEmailSubscriptionService _emailSubscriptionService;
    private readonly ILogService _logService;

    public SessionService(CoachingDbContext context, 
        IHelperService helperService,
        IEmailSubscriptionService emailSubscriptionService,
        ILogService logService)
    {
        this._context = context;
        this._helperService = helperService;
        this._emailSubscriptionService = emailSubscriptionService;
        this._logService = logService;
    }

    public async Task<bool> CreateSessionAsync(Session session)
    {
        if(session is null)
            return false;

        try
        {
            session.CreatedAt = DateTime.UtcNow;
            session.IsSessionBooking = true; // add Outro option?
            session.DiscoveryCall = true;
            session.UpdateFullName();

            _context.Sessions.Add(session);

            //await SendEmailAsync(contact); // send email to the admin

            await _context.SaveChangesAsync();

            var videoSession = new VideoSession
            {
                UserId = session.Email,
                ScheduledAt = session.PreferredDateTime,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                SessionRefId = session.Id,
            };

            _context.VideoSessions.Add(videoSession);
            await _context.SaveChangesAsync();

            await SendSessionConfirmationEmailAsync(session, videoSession);

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

    public Session GetSessionById(int id)
    {
        try
        {
            return _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefault(s => s.Id == id) 
                ?? throw new InvalidOperationException($"Session with ID {id} not found.");
        }
        catch (Exception ex)
        {
            _logService.LogError("GetSessionById", ex.Message);
            throw;
        }
    }

    public void UpdateSession(Session session)
    {
        try
        {
            var existing = _context.Sessions.FirstOrDefault(s => s.Id == session.Id);
            if (existing == null)
                throw new Exception("Session not found.");

            existing.Email = session.Email;
            existing.SessionCategory = session.SessionCategory;
            existing.Message = session.Message;
            existing.PreferredDateTime = session.PreferredDateTime;
            existing.DiscoveryCall = session.DiscoveryCall;
            existing.IsSessionBooking = session.IsSessionBooking;

            existing.FullName = session.FullName;

            _context.Sessions.Update(existing);
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logService.LogError("UpdateSession", ex.Message);
            throw;
        }
    }


    public void DeleteSession(int id)
    {
        try
        {
            var session = _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefault(s => s.Id == id);

            if (session == null)
                throw new Exception("Session not found.");

            if (session.VideoSession != null)
            {
                _context.VideoSessions.Remove(session.VideoSession);
            }

            _context.Sessions.Remove(session);
            _context.SaveChanges();
        }
        catch (Exception ex)
        {
            _logService.LogError("DeleteSession", ex.Message);
            throw;
        }
    }

    public async Task<List<Session>> GetAllSessions()
    {
        try
        {
            var sessions = await _context.Sessions
                            .Include(s => s.VideoSession)
                            .ToListAsync();

            return sessions;
        } 
        catch(Exception ex)
        {
            await _logService.LogError("GetAllSessions", ex.Message);
            throw;
        }
    }

    public async Task SendEmailAsync(Session session)
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
        message.Subject = $"Meeting Scheduled - {session.FullName}";
        message.Body = new TextPart("plain")
        {
            Text = $"Name: {session.FullName}\n" + 
                   "Email: " + session.Email + "\n" +
                   "Session Category: " + session.SessionCategory + "\n" +  
                   "Message: " + session.Message + "\n" +
                   "Preferred Meeting Date: " + session.PreferredDateTime
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

    public async Task SendSessionConfirmationEmailAsync(Session session, VideoSession videoSession)
    {
        var smtpServer = _helperService.GetConfigValue("SmtpSettings:Server");
        var smtpPort = int.Parse(_helperService.GetConfigValue("SmtpSettings:Port") ?? "587");
        var smtpUsername = _helperService.GetConfigValue("SmtpSettings:Username");
        var smtpPassword = _helperService.GetConfigValue("SmtpSettings:Password");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Ítala Veloso", smtpUsername));
        message.To.Add(new MailboxAddress(session.FullName, session.Email));
        message.Subject = "Your Discovery Call is Confirmed";

        string baseUrl = _helperService.GetConfigValue("AppSettings:BaseUrl");
        string videoCallLink = $"{baseUrl}/session/{videoSession.SessionId}";

        string formattedDate = session.PreferredDateTime.ToString("dddd, dd MMMM yyyy 'at' HH:mm");

        var builder = new BodyBuilder
        {
            HtmlBody = $@"
            <div style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
                <p>Dear {session.FirstName},</p>

                <p>Thank you for booking a free Discovery Call with Ítala Veloso.</p>

                <p><strong>📅 Date:</strong> {formattedDate}<br>
                <strong>💼 Session:</strong> {session.SessionCategory}<br>
                <strong>📍 Access Link:</strong> <a href='{videoCallLink}' target='_blank'>{videoCallLink}</a></p>

                <p>Please join a few minutes before your session time. If you need to reschedule, just reply to this email.</p>

                <p>Looking forward to seeing you!</p>

                <p>Warm regards,<br><strong>Ítala Veloso</strong></p>
            </div>"
        };

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtpServer, smtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtpUsername, smtpPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
