namespace PlanningPoker.Client.Features;

public class RoomUser(string id, string name, string? estimate = null)
{
    public string Id { get; set; } = id;
    public string Name { get; set; } = name;
    public string? Estimate { get; set; } = estimate;
}

