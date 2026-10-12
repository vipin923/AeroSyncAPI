using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AeroSyncAPI.Hubs
{
    [Authorize]
    public class TerminalHub : Hub
    {

    }
}
