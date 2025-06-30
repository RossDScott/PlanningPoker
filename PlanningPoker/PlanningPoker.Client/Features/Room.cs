namespace PlanningPoker.Client.Features;

public record Room
{
    public string Id { get; set; }
    public required string Name { get; set; }
}

