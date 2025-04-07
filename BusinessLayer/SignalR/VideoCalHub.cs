
using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

public class VideoCallHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> NonAdminOccupantBySession = new ();

    private static readonly ConcurrentDictionary<string, string> SessionByConnection = new ();

    public async Task SendSignal(string sessionId, string message)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveSignal", message);
    }

    public async Task JoinSession(string sessionId)
    {
        if (Context.User.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            SessionByConnection[Context.ConnectionId] = sessionId;
            return;
        }

        if (NonAdminOccupantBySession.TryGetValue(sessionId, out var occupantConnId))
        {
            if (!string.IsNullOrEmpty(occupantConnId))
            {
                throw new HubException("This session is already taken by a user. Only one user can join.");
            }
        }

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
}
