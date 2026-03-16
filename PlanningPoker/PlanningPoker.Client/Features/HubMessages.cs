namespace PlanningPoker.Client.Features;

public static class HubMessages
{
    // Server → Client
    public const string LoginFailed              = "LoginFailed";
    public const string JoinRoom                 = "JoinRoom";
    public const string AnnounceAlreadyInTheRoom = "AnnounceAlreadyInTheRoom";
    public const string SubmitEstimate           = "SubmitEstimate";
    public const string RemoveFromRoom           = "RemoveFromRoom";
    public const string RevealEstimates          = "RevealEstimates";
    public const string ResetEstimates           = "ResetEstimates";

    // Mini-game (Server → Client)
    public const string StartMinigame            = "StartMinigame";
    public const string MinigameResults          = "MinigameResults";

    // Mini-game (Client → Server)
    public const string SubmitGameScore          = "SubmitGameScore";

    // Client → Server
    public const string Login                    = "Login";
    public const string ExitRoom                 = "ExitRoom";
}
