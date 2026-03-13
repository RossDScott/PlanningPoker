namespace PlanningPoker.Client.Features;

public record VoteStats(
    bool IsConsensus,
    string? ConsensusValue,
    string? Min,
    List<string> MinVoters,
    string? Max,
    List<string> MaxVoters,
    string? AverageCard,
    int QuestionMarkCount,
    string? ModeCard,
    List<string> ModeVoters)
{
    private static readonly int[] NumericCardValues = [1, 2, 3, 5, 8, 13, 20];

    public static VoteStats? Calculate(List<RoomUser> voters)
    {
        var voted = voters.Where(x => x.Estimate != null).ToList();
        if (!voted.Any()) return null;

        if (voted.Count == voters.Count && voted.Select(x => x.Estimate).Distinct().Count() == 1)
            return new VoteStats(true, voted[0].Estimate!, null, [], null, [], null, 0, null, []);

        var numericVotes = voted
            .Where(x => int.TryParse(x.Estimate, out _))
            .Select(x => (User: x, Value: int.Parse(x.Estimate!)))
            .ToList();

        var questionMarkCount = voted.Count(x => x.Estimate == "?");

        if (!numericVotes.Any())
            return new VoteStats(false, null, null, [], null, [], null, questionMarkCount, null, []);

        var minVal = numericVotes.Min(x => x.Value);
        var maxVal = numericVotes.Max(x => x.Value);
        var average = numericVotes.Average(x => x.Value);
        var nearestCard = NumericCardValues.FirstOrDefault(c => c >= average, NumericCardValues[^1]);

        var groups = numericVotes.GroupBy(x => x.Value).OrderByDescending(g => g.Count()).ToList();
        var maxCount = groups[0].Count();
        var modeGroups = groups.Where(g => g.Count() == maxCount).OrderBy(g => g.Key).ToList();
        var modeCard = string.Join(" / ", modeGroups.Select(g => g.Key));
        var modeVoters = modeGroups.SelectMany(g => g.Select(x => x.User.Name)).ToList();

        return new VoteStats(
            false, null,
            minVal.ToString(), numericVotes.Where(x => x.Value == minVal).Select(x => x.User.Name).ToList(),
            maxVal.ToString(), numericVotes.Where(x => x.Value == maxVal).Select(x => x.User.Name).ToList(),
            nearestCard.ToString(),
            questionMarkCount,
            modeCard, modeVoters);
    }
}
