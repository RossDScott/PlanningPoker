namespace PlanningPoker.Client.Features;

public record VoteStats(
    bool IsConsensus,
    string? ConsensusValue,
    string? Min,
    List<string> MinVoters,
    string? Max,
    List<string> MaxVoters,
    string? AverageCard,
    int QuestionMarkCount)
{
    private static readonly int[] NumericCardValues = [1, 2, 3, 5, 8, 13, 20];

    public static VoteStats? Calculate(List<RoomUser> voters)
    {
        var voted = voters.Where(x => x.Estimate != null).ToList();
        if (!voted.Any()) return null;

        if (voted.Count == voters.Count && voted.Select(x => x.Estimate).Distinct().Count() == 1)
            return new VoteStats(true, voted[0].Estimate!, null, [], null, [], null, 0);

        var numericVotes = voted
            .Where(x => int.TryParse(x.Estimate, out _))
            .Select(x => (User: x, Value: int.Parse(x.Estimate!)))
            .ToList();

        var questionMarkCount = voted.Count(x => x.Estimate == "?");

        if (!numericVotes.Any())
            return new VoteStats(false, null, null, [], null, [], null, questionMarkCount);

        var minVal = numericVotes.Min(x => x.Value);
        var maxVal = numericVotes.Max(x => x.Value);
        var average = numericVotes.Average(x => x.Value);
        var nearestCard = NumericCardValues.FirstOrDefault(c => c >= average, NumericCardValues[^1]);

        return new VoteStats(
            false, null,
            minVal.ToString(), numericVotes.Where(x => x.Value == minVal).Select(x => x.User.Name).ToList(),
            maxVal.ToString(), numericVotes.Where(x => x.Value == maxVal).Select(x => x.User.Name).ToList(),
            nearestCard.ToString(),
            questionMarkCount);
    }
}
