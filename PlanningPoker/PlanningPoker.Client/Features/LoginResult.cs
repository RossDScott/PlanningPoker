using Microsoft.AspNetCore.SignalR.Client;

namespace PlanningPoker.Client.Features;

public class LoginResult
{
    public AppUser User { get; set; }
    public HubConnection HubConnection { get; set; }
}
