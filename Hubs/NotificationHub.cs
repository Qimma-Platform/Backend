using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ELearningPlatform.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}