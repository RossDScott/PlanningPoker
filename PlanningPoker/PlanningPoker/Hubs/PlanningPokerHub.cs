using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Client.Features;

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

            Clients.Caller.SendAsync(HubMessages.LoginFailed, errorMessage);

            // Reject the connection
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
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
        if (Context.Items.TryGetValue("RoomId", out var previousRoom))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previousRoom!.ToString()!);

        Context.Items["RoomId"] = roomId;
        var userName = GetUserName();

        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync(HubMessages.JoinRoom, GetUserId(), Context.ConnectionId, userName, observing);
    }

    public async Task ExitRoom(string roomId)
    {
        var userId = GetUserId();
        Context.Items.Remove("RoomId");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
        await Clients.Group(roomId).SendAsync(HubMessages.RemoveFromRoom, userId);
    }

    public async Task AnnounceAlreadyInTheRoom(string forConnectionId, bool observing, string? estimate, bool showEstimates)
    {
        if (forConnectionId == Context.ConnectionId)
            return;

        var userName = GetUserName();

        await Clients.Client(forConnectionId).SendAsync(HubMessages.AnnounceAlreadyInTheRoom, GetUserId(), userName, observing, estimate, showEstimates);
    }

    public async Task SubmitEstimate(string roomId, string estimate)
    {
        if (!Cards.AvailableCards.Contains(estimate))
        {
            throw new ArgumentException($"Invalid estimate provided. Available cards: {string.Join(", ", Cards.AvailableCards)}");
        }

        await Clients.Group(roomId).SendAsync(HubMessages.SubmitEstimate, GetUserId(), estimate);
    }

    public async Task RevealEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync(HubMessages.RevealEstimates);
    }

    public async Task ResetEstimates(string roomId)
    {
        await Clients.Group(roomId).SendAsync(HubMessages.ResetEstimates);
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
