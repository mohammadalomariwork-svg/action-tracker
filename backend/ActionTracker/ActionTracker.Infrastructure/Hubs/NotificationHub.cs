using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ActionTracker.Infrastructure.Hubs;

[Authorize(AuthenticationSchemes = "LocalBearer,AzureAD")]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
