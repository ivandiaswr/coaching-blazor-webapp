using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public class VideoCallHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> NonAdminOccupantBySession = new();
    private static readonly ConcurrentDictionary<string, string> SessionByConnection = new();
    private static readonly ConcurrentDictionary<string, string> OccupantUserBySession = new();

    public async Task SendSignal(string sessionId, string message)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveSignal", message);
    }

    public async Task JoinSession(string sessionId)
    {
        var user = Context.User;
        var userName = user.Identity?.Name ?? Context.ConnectionId;

        if (user.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            SessionByConnection[Context.ConnectionId] = sessionId;
            return;
        }

        if (OccupantUserBySession.TryGetValue(sessionId, out var existingUser))
        {
            if (existingUser != userName)
            {
                throw new HubException("This session is already taken by another user.");
            }
        }

        OccupantUserBySession[sessionId] = userName;
        NonAdminOccupantBySession[sessionId] = Context.ConnectionId;
        SessionByConnection[Context.ConnectionId] = sessionId;

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    public async Task SendChatMessage(string sessionId, string userName, string message)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveChatMessage", userName, DateTime.UtcNow.ToString("HH:mm:ss"), message);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (SessionByConnection.TryRemove(Context.ConnectionId, out string sessionId))
        {
            if (NonAdminOccupantBySession.TryGetValue(sessionId, out var occupantConnId))
            {
                if (occupantConnId == Context.ConnectionId)
                {
                    NonAdminOccupantBySession.TryRemove(sessionId, out _);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }


    public async Task SendFileAttachment(string sessionId, string userName, string fileName, string base64Data, string contentType)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
        await Clients.Group(sessionId).SendAsync("ReceiveFileAttachment", userName, timestamp, fileName, base64Data, contentType);
    }
}
