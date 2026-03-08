namespace PlanningPoker.Client.Features;

public class Room
{
    public string Id { get; set; } = string.Empty;
    public required string Name { get; set; }
    public bool Observing { get; set; } = false;
}
