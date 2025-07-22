using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public class VideoCallHub : Hub
{
    // Using ConcurrentDictionary for thread-safe operations
    private static readonly ConcurrentDictionary<string, string> NonAdminOccupantBySession = new();
    private static readonly ConcurrentDictionary<string, string> SessionByConnection = new();
    private static readonly ConcurrentDictionary<string, string> OccupantUserBySession = new();
    private static readonly ConcurrentDictionary<string, string> AdminConnectionBySession = new();
    private static readonly ConcurrentDictionary<string, DateTime> ConnectionTimestamps = new();

    // Timer for periodic cleanup of stale connections
    private static readonly Timer CleanupTimer = new Timer(CleanupStaleConnections, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));

    private static void CleanupStaleConnections(object? state)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-2); // Connections older than 2 hours are considered stale
        var staleConnections = ConnectionTimestamps
            .Where(kvp => kvp.Value < cutoffTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var connectionId in staleConnections)
        {
            ConnectionTimestamps.TryRemove(connectionId, out _);

            if (SessionByConnection.TryRemove(connectionId, out var sessionId))
            {
                if (NonAdminOccupantBySession.TryGetValue(sessionId, out var occupantConnId) && occupantConnId == connectionId)
                {
                    NonAdminOccupantBySession.TryRemove(sessionId, out _);
                    OccupantUserBySession.TryRemove(sessionId, out _);
                }
                if (AdminConnectionBySession.TryGetValue(sessionId, out var adminConnId) && adminConnId == connectionId)
                {
                    AdminConnectionBySession.TryRemove(sessionId, out _);
                }
            }
        }
        Console.WriteLine($"Cleaned up {staleConnections.Count} stale connections.");
    }

    public async Task SendSignal(string sessionId, string message)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveSignal", message);
    }

    public async Task JoinSession(string sessionId)
    {
        var connectionId = Context.ConnectionId;
        await Groups.AddToGroupAsync(connectionId, sessionId);
        SessionByConnection[connectionId] = sessionId;
        ConnectionTimestamps[connectionId] = DateTime.UtcNow;

        var user = Context.User;
        var participantType = user.IsInRole("Admin") ? "Admin" : "Client";

        // Smart cleanup: if a user of the same type is already in the session, clean up their old connection
        if (participantType == "Admin")
        {
            if (AdminConnectionBySession.TryGetValue(sessionId, out var oldAdminConnectionId) && oldAdminConnectionId != connectionId)
            {
                await Groups.RemoveFromGroupAsync(oldAdminConnectionId, sessionId);
                SessionByConnection.TryRemove(oldAdminConnectionId, out _);
            }
            AdminConnectionBySession[sessionId] = connectionId;
        }
        else // Client
        {
            if (NonAdminOccupantBySession.TryGetValue(sessionId, out var oldClientConnectionId) && oldClientConnectionId != connectionId)
            {
                await Groups.RemoveFromGroupAsync(oldClientConnectionId, sessionId);
                SessionByConnection.TryRemove(oldClientConnectionId, out _);
            }
            NonAdminOccupantBySession[sessionId] = connectionId;
            OccupantUserBySession[sessionId] = user.Identity?.Name ?? "Client";
        }

        // Notify others that a participant has joined
        await Clients.OthersInGroup(sessionId).SendAsync("ParticipantJoined", participantType);
        // Also notify the caller if the other role is already in session
        if (participantType == "Admin")
        {
            // If a client is already present, notify admin of client
            if (NonAdminOccupantBySession.ContainsKey(sessionId))
            {
                await Clients.Caller.SendAsync("ParticipantJoined", "Client");
            }
        }
        else // Client
        {
            // If an admin is already present, notify client of admin
            if (AdminConnectionBySession.ContainsKey(sessionId))
            {
                await Clients.Caller.SendAsync("ParticipantJoined", "Admin");
            }
        }
    }

    public Task ForceCleanupSession(string sessionId)
    {
        var connectionsToClean = SessionByConnection
            .Where(kvp => kvp.Value == sessionId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var connectionId in connectionsToClean)
        {
            SessionByConnection.TryRemove(connectionId, out _);
            ConnectionTimestamps.TryRemove(connectionId, out _);
        }

        NonAdminOccupantBySession.TryRemove(sessionId, out _);
        OccupantUserBySession.TryRemove(sessionId, out _);
        AdminConnectionBySession.TryRemove(sessionId, out _);

        Console.WriteLine($"Force cleanup completed for session {sessionId}.");
        return Task.CompletedTask;
    }

    public async Task SendChatMessage(string sessionId, string userName, string message)
    {
        var user = Context.User;
        var userRole = user.IsInRole("Admin") ? "Admin" : "Client";
        await Clients.Group(sessionId).SendAsync("ReceiveChatMessage", userName, DateTime.UtcNow.ToString("HH:mm:ss"), message, userRole);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (SessionByConnection.TryRemove(Context.ConnectionId, out var sessionId))
        {
            ConnectionTimestamps.TryRemove(Context.ConnectionId, out _);
            var user = Context.User;
            var participantType = user.IsInRole("Admin") ? "Admin" : "Client";

            if (participantType == "Admin")
            {
                if (AdminConnectionBySession.TryGetValue(sessionId, out var adminConnId) && adminConnId == Context.ConnectionId)
                {
                    AdminConnectionBySession.TryRemove(sessionId, out _);
                }
            }
            else
            {
                if (NonAdminOccupantBySession.TryGetValue(sessionId, out var clientConnId) && clientConnId == Context.ConnectionId)
                {
                    NonAdminOccupantBySession.TryRemove(sessionId, out _);
                    OccupantUserBySession.TryRemove(sessionId, out _);
                }
            }

            await Clients.OthersInGroup(sessionId).SendAsync("ParticipantLeft", participantType);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendFileAttachment(string sessionId, string userName, string fileName, string base64Data, string contentType)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
        await Clients.Group(sessionId).SendAsync("ReceiveFileAttachment", userName, timestamp, fileName, base64Data, contentType);
    }
}
