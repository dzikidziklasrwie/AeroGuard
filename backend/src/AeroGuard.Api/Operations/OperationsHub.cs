using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using AeroGuard.Api.Identity;

namespace AeroGuard.Api.Operations;

[Authorize]
public sealed class OperationsHub(
    OperationsConnectionRegistry connections)
    : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = CurrentUser.FromPrincipal(
            Context.User
            ?? throw new InvalidOperationException("The hub connection has no authenticated principal."));
        connections.Register(Context.ConnectionId, user.Id, user.SessionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
