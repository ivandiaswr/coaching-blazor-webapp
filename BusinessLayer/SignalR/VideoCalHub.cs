
using Microsoft.AspNetCore.SignalR;

public class VideoCallHub : Hub
{
    private static readonly Dictionary<string, string> NonAdminOccupantBySession = new Dictionary<string, string>();

    private static readonly Dictionary<string, string> SessionByConnection = new Dictionary<string, string>();

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

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (SessionByConnection.TryGetValue(Context.ConnectionId, out string sessionId))
        {
            SessionByConnection.Remove(Context.ConnectionId);

            if (NonAdminOccupantBySession.TryGetValue(sessionId, out var occupantConnId))
            {
                if (occupantConnId == Context.ConnectionId)
                {
                    NonAdminOccupantBySession[sessionId] = null;
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
