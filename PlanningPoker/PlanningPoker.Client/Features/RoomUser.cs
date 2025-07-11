namespace PlanningPoker.Client.Features;

public class RoomUser(string id, string name, bool observing, string? estimate = null)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public bool Observing { get; set; } = observing;
    public string? Estimate { get; set; } = estimate;
}

