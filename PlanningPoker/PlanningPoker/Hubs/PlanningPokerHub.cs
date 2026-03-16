using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PlanningPoker.Client.Features;

namespace PlanningPoker.Hubs;

public class PlanningPokerHub : Hub
{
    // ── Game session state (static = shared across all hub instances) ─────────

    private static readonly ConcurrentDictionary<string, GameSession> _gameSessions = new();

    private record GameSession(
        string[] ExpectedPlayers,
        ConcurrentDictionary<string, (string Name, int Score)> Scores);

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly string licenceKey;
    private readonly IHubContext<PlanningPokerHub> _hubContext;

    public PlanningPokerHub(IConfiguration configuration, IHubContext<PlanningPokerHub> hubContext)
    {
        licenceKey = configuration["PlanningPoker:LicenceKey"] ?? throw new InvalidOperationException("LicenceKey not configured.");
        _hubContext = hubContext;
    }

    // ── Connection lifecycle ──────────────────────────────────────────────────

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

            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // ── Room methods ──────────────────────────────────────────────────────────

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
        _gameSessions.TryRemove(roomId, out _);
        await Clients.Group(roomId).SendAsync(HubMessages.ResetEstimates);
    }

    // ── Mini-game methods ─────────────────────────────────────────────────────

    public async Task StartMinigame(string roomId, string[] voterIds)
    {
        // De-duplicate: only the first caller per room creates the session
        var session = new GameSession(voterIds, new ConcurrentDictionary<string, (string, int)>());
        if (!_gameSessions.TryAdd(roomId, session))
            return;

        await Clients.Group(roomId).SendAsync(HubMessages.StartMinigame);

        // Safety timeout: broadcast results after 90s even if not all players submitted
        var hubContext = _hubContext;
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(90));
            await BroadcastMinigameResults(roomId, hubContext);
        });
    }

    public async Task SubmitGameScore(string roomId, int score)
    {
        if (!_gameSessions.TryGetValue(roomId, out var session))
            return;

        var userId = GetUserId();
        var userName = GetUserName();
        session.Scores.TryAdd(userId, (userName, score));

        bool allIn = session.ExpectedPlayers.All(id => session.Scores.ContainsKey(id));
        if (allIn)
            await BroadcastMinigameResults(roomId, _hubContext);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task BroadcastMinigameResults(string roomId, IHubContext<PlanningPokerHub> hubContext)
    {
        if (!_gameSessions.TryRemove(roomId, out var session) || session.Scores.IsEmpty)
            return;

        var winner = session.Scores.MaxBy(x => x.Value.Score);
        var scores = session.Scores.Values
            .OrderByDescending(x => x.Score)
            .Select(x => new MinigamePlayerResult(x.Name, x.Score))
            .ToList();

        await hubContext.Clients.Group(roomId).SendAsync(HubMessages.MinigameResults, winner.Value.Name, scores);
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
