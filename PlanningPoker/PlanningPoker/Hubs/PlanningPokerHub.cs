using Microsoft.AspNetCore.SignalR;

namespace PlanningPoker.Hubs;

public class PlanningPokerHub : Hub
{
    private readonly string licenceKey;

    public PlanningPokerHub(IConfiguration configuration)
    {
        licenceKey = configuration["PlanningPoker:LicenceKey"] ?? throw new InvalidOperationException("LicenceKey not configured.");
    }

    public override Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();

        var accessToken = httpContext.Request.Query["access_token"].ToString() ??
            httpContext.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(accessToken) || !accessToken.Contains(licenceKey))
        {
            var errorMessage = string.IsNullOrEmpty(accessToken)
                ? "No licence key provided. Please log in with a valid licence key."
                : "Invalid licence key provided. Please log in with a valid licence key.";

            Clients.Caller.SendAsync("LoginFailed", errorMessage);

            // Reject the connection
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.All.SendAsync("RemoveFromRoom", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task Login(string username)
    {
        var httpContext = Context.GetHttpContext();
        var accessToken = httpContext.Request.Query["access_token"].ToString();

        Context.Items.TryAdd("UserName", username);

        return Task.CompletedTask;
    }

    public async Task JoinRoom(string roomId, bool observing)
    {
        var userName = GetUserName();

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        await Clients.Group(roomId).SendAsync("JoinRoom", Context.ConnectionId, userName, observing);
    }

    public async Task AnnounceAlreadyInTheRoom(string forClient, bool observing)
    {
        if (forClient == Context.ConnectionId)
            return;

        var userName = GetUserName();

        await Clients.Client(forClient).SendAsync("AnnounceAlreadyInTheRoom", Context.ConnectionId, userName, observing);
    }

    public async Task SubmitEstimate(string roomId, string estimate)
    {
        await Clients.Group(roomId).SendAsync("SubmitEstimate", Context.ConnectionId, estimate);
    }

    public async Task RevealEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync("RevealEstimates");
    }

    public async Task ResetEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync("ResetEstimates");
    }

    private string GetUserName()
    {
        if (Context.Items.TryGetValue("UserName", out var userName))
            return userName.ToString();

        throw new InvalidProgramException("Username is missing");
    }
}

