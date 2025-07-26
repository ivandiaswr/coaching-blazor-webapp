using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;
using ModelLayer.Models;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models.Enums;
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
        if (session == null)
            return false;

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var existingSession = await _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefaultAsync(s => s.Id == session.Id);

            if (existingSession != null)
            {
                if (existingSession.IsPending)
                {
                    existingSession.IsPending = false;
                    existingSession.Email = session.Email;
                    existingSession.SessionCategory = session.SessionCategory;
                    existingSession.Message = session.Message;
                    existingSession.PreferredDateTime = session.PreferredDateTime;
                    existingSession.DiscoveryCall = session.DiscoveryCall;
                    existingSession.IsSessionBooking = session.IsSessionBooking;
                    existingSession.FirstName = session.FirstName;
                    existingSession.LastName = session.LastName;
                    existingSession.FullName = session.FullName;
                    existingSession.PackId = session.PackId;
                    existingSession.IsPaid = session.IsPaid;
                    existingSession.PaidAt = session.PaidAt;
                    existingSession.UpdateFullName();
                    _context.Sessions.Update(existingSession);
                    await _logService.LogInfo("CreateSessionAsync", $"Confirmed pending session Id: {existingSession.Id}, UserId: {existingSession.Email}, PackId: {existingSession.PackId}");
                }
                else
                {
                    existingSession.Email = session.Email;
                    existingSession.SessionCategory = session.SessionCategory;
                    existingSession.Message = session.Message;
                    existingSession.PreferredDateTime = session.PreferredDateTime;
                    existingSession.DiscoveryCall = session.DiscoveryCall;
                    existingSession.IsSessionBooking = session.IsSessionBooking;
                    existingSession.FirstName = session.FirstName;
                    existingSession.LastName = session.LastName;
                    existingSession.FullName = session.FullName;
                    existingSession.PackId = session.PackId;
                    existingSession.IsPaid = session.IsPaid;
                    existingSession.PaidAt = session.PaidAt;
                    existingSession.UpdateFullName();
                    _context.Sessions.Update(existingSession);
                    await _logService.LogInfo("CreateSessionAsync", $"Updated session Id: {existingSession.Id}, UserId: {existingSession.Email}, PackId: {existingSession.PackId}");
                }
            }
            else
            {
                session.CreatedAt = DateTime.UtcNow;
                session.IsSessionBooking = true;
                session.IsPending = false;
                session.UpdateFullName();
                _context.Sessions.Add(session);
                await _logService.LogInfo("CreateSessionAsync", $"Created new session Id: {session.Id}, UserId: {session.Email}, PackId: {session.PackId}");
            }

            var targetSession = existingSession ?? session;

            if (targetSession.VideoSession == null)
            {
                var videoSession = new VideoSession
                {
                    UserId = targetSession.Email,
                    ScheduledAt = targetSession.PreferredDateTime,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Session = targetSession,
                    SessionRefId = targetSession.Id
                };

                _context.VideoSessions.Add(videoSession);
            }
            else
            {
                await _logService.LogInfo("CreateSessionAsync", $"VideoSession already exists for session Id: {targetSession.Id}, VideoSession Id: {targetSession.VideoSession.Id}");
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await SendSessionConfirmationEmailAsync(targetSession, targetSession.VideoSession ?? await _context.VideoSessions.FirstAsync(vs => vs.SessionRefId == targetSession.Id));
            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateSessionAsync", ex.Message);
            await _emailSubscriptionService.SendCustomEmailAsync(
                new List<string>
                {
                    _helperService.GetConfigValue("AdminEmail:Primary"),
                    _helperService.GetConfigValue("AdminEmail:Secondary")
                },
                "Schedule Error",
                $"Exception: {ex}<br>Message: {ex.Message}"
            );
            return false;
        }
    }

    public async Task<bool> CreateSessionWithPackConsumptionAsync(Session session, string userId, string packId, ISessionPackService sessionPackService)
    {
        if (session == null)
            return false;

        try
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            // First, consume the session from the pack (without saving)
            var consumeSuccess = await sessionPackService.ConsumeSessionWithoutSave(userId, packId);
            if (!consumeSuccess)
            {
                await _logService.LogError("CreateSessionWithPackConsumption", $"Failed to consume session for PackId: {packId}, User: {userId}");
                return false;
            }

            // Then create the session (without saving)
            var existingSession = await _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefaultAsync(s => s.Id == session.Id);

            if (existingSession != null)
            {
                if (existingSession.IsPending)
                {
                    existingSession.IsPending = false;
                    existingSession.Email = session.Email;
                    existingSession.SessionCategory = session.SessionCategory;
                    existingSession.Message = session.Message;
                    existingSession.PreferredDateTime = session.PreferredDateTime;
                    existingSession.DiscoveryCall = session.DiscoveryCall;
                    existingSession.IsSessionBooking = session.IsSessionBooking;
                    existingSession.FirstName = session.FirstName;
                    existingSession.LastName = session.LastName;
                    existingSession.FullName = session.FullName;
                    existingSession.PackId = session.PackId;
                    existingSession.IsPaid = session.IsPaid;
                    existingSession.PaidAt = session.PaidAt;
                    existingSession.UpdateFullName();
                    _context.Sessions.Update(existingSession);
                    await _logService.LogInfo("CreateSessionWithPackConsumption", $"Confirmed pending session Id: {existingSession.Id}, UserId: {existingSession.Email}, PackId: {existingSession.PackId}");
                }
                else
                {
                    existingSession.Email = session.Email;
                    existingSession.SessionCategory = session.SessionCategory;
                    existingSession.Message = session.Message;
                    existingSession.PreferredDateTime = session.PreferredDateTime;
                    existingSession.DiscoveryCall = session.DiscoveryCall;
                    existingSession.IsSessionBooking = session.IsSessionBooking;
                    existingSession.FirstName = session.FirstName;
                    existingSession.LastName = session.LastName;
                    existingSession.FullName = session.FullName;
                    existingSession.PackId = session.PackId;
                    existingSession.IsPaid = session.IsPaid;
                    existingSession.PaidAt = session.PaidAt;
                    existingSession.UpdateFullName();
                    _context.Sessions.Update(existingSession);
                    await _logService.LogInfo("CreateSessionWithPackConsumption", $"Updated session Id: {existingSession.Id}, UserId: {existingSession.Email}, PackId: {existingSession.PackId}");
                }
            }
            else
            {
                session.CreatedAt = DateTime.UtcNow;
                session.IsSessionBooking = true;
                session.IsPending = false;
                session.UpdateFullName();
                _context.Sessions.Add(session);
                await _logService.LogInfo("CreateSessionWithPackConsumption", $"Created new session Id: {session.Id}, UserId: {session.Email}, PackId: {session.PackId}");
            }

            var targetSession = existingSession ?? session;

            if (targetSession.VideoSession == null)
            {
                var videoSession = new VideoSession
                {
                    UserId = targetSession.Email,
                    ScheduledAt = targetSession.PreferredDateTime,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Session = targetSession,
                    SessionRefId = targetSession.Id
                };

                _context.VideoSessions.Add(videoSession);
            }
            else
            {
                await _logService.LogInfo("CreateSessionWithPackConsumption", $"VideoSession already exists for session Id: {targetSession.Id}, VideoSession Id: {targetSession.VideoSession.Id}");
            }

            // Save all changes together (session pack consumption + session creation)
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _logService.LogInfo("CreateSessionWithPackConsumption", $"Successfully created session and consumed pack session for PackId: {packId}, UserId: {userId}");

            await SendSessionConfirmationEmailAsync(targetSession, targetSession.VideoSession ?? await _context.VideoSessions.FirstAsync(vs => vs.SessionRefId == targetSession.Id));
            return true;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreateSessionWithPackConsumption", $"Failed to create session with pack consumption: {ex.Message}");
            return false;
        }
    }

    public async Task<Session> CreatePendingSessionAsync(Session session)
    {
        try
        {
            if (session == null)
            {
                await _logService.LogError("CreatePendingSessionAsync", "Session object is null.");
                throw new ArgumentNullException(nameof(session));
            }

            var existingPendingSession = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Email == session.Email &&
                                        s.IsPending &&
                                        s.SessionCategory == session.SessionCategory &&
                                        s.PreferredDateTime == session.PreferredDateTime &&
                                        (string.IsNullOrEmpty(session.PackId) ? s.PackId == null : s.PackId == session.PackId) &&
                                        (session.Id == 0 || s.Id != session.Id));

            if (existingPendingSession != null)
            {
                await _logService.LogWarning("CreatePendingSessionAsync",
                    $"Duplicate pending session found for Email: {session.Email}, SessionId: {existingPendingSession.Id}, SessionCategory: {existingPendingSession.SessionCategory}, PreferredDateTime: {existingPendingSession.PreferredDateTime}, PackId: {existingPendingSession.PackId}");

                // Check if the existing session is stale (older than 30 minutes) or has no StripeSessionId
                var staleThreshold = DateTime.UtcNow.AddMinutes(-30);
                if (existingPendingSession.CreatedAt < staleThreshold || string.IsNullOrEmpty(existingPendingSession.StripeSessionId))
                {
                    await _logService.LogInfo("CreatePendingSessionAsync",
                        $"Cleaning up stale pending session Id: {existingPendingSession.Id}, CreatedAt: {existingPendingSession.CreatedAt}, StripeSessionId: {existingPendingSession.StripeSessionId ?? "null"}");

                    // Clean up the stale session and continue with creating the new one
                    existingPendingSession.IsPending = false;
                    _context.Sessions.Update(existingPendingSession);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // If the session is recent and has a StripeSessionId, it's likely a legitimate pending session
                    throw new InvalidOperationException($"A pending session already exists (SessionId: {existingPendingSession.Id}).");
                }
            }

            session.IsPending = true;
            session.CreatedAt = DateTime.UtcNow;
            session.PaidAt = default;

            if (string.IsNullOrEmpty(session.StripeSessionId))
            {
                await _logService.LogInfo("CreatePendingSessionAsync",
                    $"Creating pending session without StripeSessionId for Email: {session.Email}");
            }
            else
            {
                await _logService.LogInfo("CreatePendingSessionAsync",
                    $"Creating pending session with StripeSessionId: {session.StripeSessionId} for Email: {session.Email}");
            }

            if (session.Id != 0)
            {
                var existingSession = await _context.Sessions.FindAsync(session.Id);
                if (existingSession != null)
                {
                    existingSession.StripeSessionId = session.StripeSessionId;
                    existingSession.IsPending = session.IsPending;
                    existingSession.CreatedAt = session.CreatedAt;
                    existingSession.PaidAt = session.PaidAt;
                    existingSession.Email = session.Email;
                    existingSession.SessionCategory = session.SessionCategory;
                    existingSession.PreferredDateTime = session.PreferredDateTime;
                    existingSession.PackId = session.PackId;
                    existingSession.FirstName = session.FirstName;
                    existingSession.LastName = session.LastName;
                    existingSession.FullName = session.FullName;
                    existingSession.Message = session.Message;
                    existingSession.DiscoveryCall = session.DiscoveryCall;
                    existingSession.IsSessionBooking = session.IsSessionBooking;
                    existingSession.UpdateFullName();

                    _context.Sessions.Update(existingSession);
                    await _logService.LogInfo("CreatePendingSessionAsync",
                        $"Updating existing session with Id: {session.Id}, Email: {session.Email}, StripeSessionId: {session.StripeSessionId}");
                }
                else
                {
                    _context.Sessions.Add(session);
                    await _logService.LogInfo("CreatePendingSessionAsync",
                        $"Adding new session with Id: {session.Id}, Email: {session.Email}, StripeSessionId: {session.StripeSessionId}");
                }
            }
            else
            {
                _context.Sessions.Add(session);
                await _logService.LogInfo("CreatePendingSessionAsync",
                    $"Adding new session with auto-generated Id for Email: {session.Email}, StripeSessionId: {session.StripeSessionId}");
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                await _logService.LogError("CreatePendingSessionAsync SQLite Error",
                    $"Failed to save session for Email: {session.Email}, Id: {session.Id}, Error: {ex.Message}, SqliteErrorCode: {ex.SqliteErrorCode}, InnerException: {ex.InnerException?.Message}");
                throw;
            }
            catch (DbUpdateException ex)
            {
                await _logService.LogError("CreatePendingSessionAsync DbUpdate Error",
                    $"Failed to save session for Email: {session.Email}, Id: {session.Id}, Error: {ex.Message}, InnerException: {ex.InnerException?.Message}");
                throw;
            }

            await _logService.LogInfo("CreatePendingSessionAsync",
                $"Created or updated pending session with Id: {session.Id}, Email: {session.Email}, SessionCategory: {session.SessionCategory}, PreferredDateTime: {session.PreferredDateTime}");

            return session;
        }
        catch (Exception ex)
        {
            await _logService.LogError("CreatePendingSessionAsync Error",
                $"Failed to create or update pending session for Email: {session?.Email}, Id: {session?.Id}, Error: {ex.Message}, InnerException: {ex.InnerException?.Message}, StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<Session> GetSessionByStripeSessionIdAsync(string stripeSessionId)
    {
        try
        {
            var session = await _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefaultAsync(s => s.StripeSessionId == stripeSessionId);

            if (session == null)
            {
                await _logService.LogError("GetSessionByStripeSessionIdAsync", $"No session found for StripeSessionId: {stripeSessionId}");
            }

            return session;
        }
        catch (Exception ex)
        {
            await _logService.LogError("GetSessionByStripeSessionIdAsync", ex.Message);
            throw;
        }
    }

    public async Task<Session> GetSessionByIdAsync(int id)
    {
        try
        {
            var session = await _context.Sessions
                .Include(s => s.VideoSession)
                .FirstOrDefaultAsync(s => s.Id == id);

            return session ?? throw new InvalidOperationException($"Session with ID {id} not found.");
        }
        catch (Exception ex)
        {
            await _logService.LogError("GetSessionByIdAsync", ex.Message);
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

    public async Task<List<Session>> GetAllSessionsAsync(bool includePending = false)
    {
        try
        {
            var query = _context.Sessions
                .Include(s => s.VideoSession)
                .AsQueryable();

            if (!includePending)
            {
                query = query.Where(s => !s.IsPending);
            }

            var sessions = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            await _logService.LogInfo("GetAllSessionsAsync", $"Retrieved {sessions.Count} sessions (IncludePending: {includePending}).");
            return sessions;
        }
        catch (Exception ex)
        {
            await _logService.LogError("GetAllSessionsAsync", $"Exception: {ex.Message}, StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<Session?> GetLatestSessionByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return await _context.Sessions
            .Where(s => s.Email == email)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Session?> GetSessionByEmailAsync(string email)
    {
        return await _context.Sessions
            .FirstOrDefaultAsync(s => s.Email.ToLower() == email.ToLower());
    }

    public async Task<Session> GetPendingSessionByEmailAndPackAsync(string email, string packId, SessionType sessionCategory, DateTime preferredDateTime)
    {
        try
        {
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.Email == email &&
                                        s.IsPending &&
                                        s.SessionCategory == sessionCategory &&
                                        s.PreferredDateTime == preferredDateTime &&
                                        (string.IsNullOrEmpty(packId) ? s.PackId == null : s.PackId == packId));

            if (session != null)
            {
                await _logService.LogInfo("GetPendingSessionByEmailAndPackAsync",
                    $"Found pending session Id: {session.Id} for Email: {email}, SessionCategory: {sessionCategory}, PreferredDateTime: {preferredDateTime}, PackId: {packId}");
            }

            return session;
        }
        catch (Exception ex)
        {
            await _logService.LogError("GetPendingSessionByEmailAndPackAsync",
                $"Error querying pending session for Email: {email}, PackId: {packId}. Error: {ex.Message}");
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

        // Dynamic subject based on whether it's a discovery call or regular session
        string sessionTypeDisplayName = session.SessionCategory.GetDisplayName();
        bool isDiscoveryCall = session.DiscoveryCall;
        message.Subject = isDiscoveryCall ? "Your Discovery Call is Confirmed" : $"Your {sessionTypeDisplayName} Session is Confirmed";

        string baseUrl = _helperService.GetConfigValue("AppSettings:BaseUrl");
        string videoCallLink = $"{baseUrl}/session/{videoSession.SessionId}";

        string formattedDate = session.PreferredDateTime.ToString("dddd, dd MMMM yyyy 'at' HH:mm");

        // Dynamic email content based on session type
        string sessionDescription = isDiscoveryCall ? "free Discovery Call" : $"{sessionTypeDisplayName} session";

        var builder = new BodyBuilder
        {
            HtmlBody = $@"
            <div style='font-family: Arial, sans-serif; font-size: 14px; color: #333;'>
                <p>Dear {session.FirstName},</p>

                <p>Thank you for booking a {sessionDescription} with Ítala Veloso.</p>

                <p><strong>📅 Date:</strong> {formattedDate}<br>
                <strong>💼 Session:</strong> {sessionTypeDisplayName}<br>
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

    public async Task CleanupPendingSessionsForUserAsync(string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                await _logService.LogWarning("CleanupPendingSessionsForUserAsync", "Email is null or empty.");
                return;
            }

            // Delete ALL pending sessions for this user when they're trying to make a new booking
            // This includes sessions that may have been created recently but abandoned (e.g., Stripe cancellation)
            var pendingSessions = await _context.Sessions
                .Include(s => s.VideoSession)
                .Where(s => s.Email == email && s.IsPending)
                .ToListAsync();

            if (pendingSessions.Any())
            {
                await _logService.LogInfo("CleanupPendingSessionsForUserAsync",
                    $"Found {pendingSessions.Count} pending sessions for email: {email}");

                foreach (var session in pendingSessions)
                {
                    var sessionAge = DateTime.UtcNow - session.CreatedAt;
                    await _logService.LogInfo("CleanupPendingSessionsForUserAsync",
                        $"Deleting pending session Id: {session.Id}, Email: {session.Email}, CreatedAt: {session.CreatedAt}, Age: {sessionAge.TotalMinutes:F1} minutes, StripeSessionId: {session.StripeSessionId ?? "null"}");

                    // Remove related VideoSession if it exists (though unlikely for pending sessions)
                    if (session.VideoSession != null)
                    {
                        await _logService.LogInfo("CleanupPendingSessionsForUserAsync",
                            $"Also removing VideoSession for SessionId: {session.Id}");
                        _context.VideoSessions.Remove(session.VideoSession);
                    }

                    _context.Sessions.Remove(session);
                }

                await _context.SaveChangesAsync();
                await _logService.LogInfo("CleanupPendingSessionsForUserAsync",
                    $"Successfully deleted {pendingSessions.Count} pending sessions for email: {email}");
            }
            else
            {
                await _logService.LogInfo("CleanupPendingSessionsForUserAsync",
                    $"No pending sessions found for email: {email}");
            }
        }
        catch (Exception ex)
        {
            await _logService.LogError("CleanupPendingSessionsForUserAsync",
                $"Error cleaning up pending sessions for email: {email}. Error: {ex.Message}");
        }
    }
}
