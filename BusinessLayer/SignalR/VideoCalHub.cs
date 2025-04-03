
using Microsoft.AspNetCore.SignalR;

public class VideoCallHub : Hub
{
    public async Task SendSignal(string sessionId, string message)
    {
        await Clients.OthersInGroup(sessionId).SendAsync("ReceiveSignal", message);
    }
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}