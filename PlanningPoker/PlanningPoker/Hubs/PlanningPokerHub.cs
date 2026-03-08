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

        string accessToken = httpContext.Request.Query.ContainsKey("access_token")
                                ? httpContext.Request.Query["access_token"].ToString()
                                : httpContext.Request.Headers["Authorization"].ToString();

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
        if (Context.Items.TryGetValue("UserId", out var userId))
            await Clients.All.SendAsync("RemoveFromRoom", userId!.ToString());
        await base.OnDisconnectedAsync(exception);
    }

    public Task Login(string userId, string username)
    {
        Context.Items.TryAdd("UserId", userId);
        Context.Items.TryAdd("UserName", username);

        return Task.CompletedTask;
    }

    public async Task JoinRoom(string roomId, bool observing)
    {
        var userName = GetUserName();

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
        await Clients.Group(roomId).SendAsync("JoinRoom", GetUserId(), userName, observing);
    }

    public async Task AnnounceAlreadyInTheRoom(string forClient, bool observing, string? estimate, bool showEstimates)
    {
        if (forClient == GetUserId())
            return;

        var userName = GetUserName();

        await Clients.Client(forClient).SendAsync("AnnounceAlreadyInTheRoom", GetUserId(), userName, observing, estimate, showEstimates);
    }

    public async Task SubmitEstimate(string roomId, string estimate)
    {
        if (!Client.Features.Cards.AvailableCards.Contains(estimate))
        {
            throw new ArgumentException($"Invalid estimate provided. Available cards: {string.Join(", ", Client.Features.Cards.AvailableCards)}");
        }

        await Clients.Group(roomId).SendAsync("SubmitEstimate", GetUserId(), estimate);
    }

    public async Task RevealEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync("RevealEstimates");
    }

    public async Task ResetEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync("ResetEstimates");
    }

    private string GetUserId()
    {
        if (Context.Items.TryGetValue("UserId", out var userId))
            return userId!.ToString()!;

        throw new InvalidProgramException("UserId is missing");
    }

    private string GetUserName()
    {
        if (Context.Items.TryGetValue("UserName", out var userName))
            return userName!.ToString()!;

        throw new InvalidProgramException("Username is missing");
    }
}

