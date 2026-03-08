namespace PlanningPoker.Client.Features;

public class AppUser
{
    public string Id { get; set; } = string.Empty;
    public required string Name { get; set; }
    public required string Licence { get; set; }
}
