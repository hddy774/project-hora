using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    internal bool TryParsePeopleCreation(GameCommand command, out PeopleCreationProfile profile, out string? error) =>
        PeopleCreationParser.TryParse(command, state.Laws, out profile, out error);

    /// <summary>Only this person's active source-backed preferences enter their goal score.</summary>
    internal int PeoplePreference(Person person, string goal)
    {
        var preferences = Traits.ActivePreferences(state.PeopleDomain.Traits, person.Id, Month);
        int Weight(GoalPreference axis) => goal switch
        {
            "production" => axis switch { GoalPreference.Patience or GoalPreference.Discipline => 3, GoalPreference.Curiosity or GoalPreference.Autonomy => 1, _ => 0 },
            "information" => axis switch { GoalPreference.Curiosity => 4, GoalPreference.Autonomy => 2, GoalPreference.Risk or GoalPreference.SeekingDao => 1, _ => 0 },
            "trade" => axis switch { GoalPreference.Cooperation or GoalPreference.Trust => 2, GoalPreference.Ambition or GoalPreference.Honour => 1, _ => 0 },
            "learning" => axis switch { GoalPreference.SeekingDao or GoalPreference.Curiosity => 3, GoalPreference.Patience or GoalPreference.Discipline => 2, _ => 0 },
            "cooperation" => axis switch { GoalPreference.Family => 4, GoalPreference.Cooperation or GoalPreference.Empathy => 3, GoalPreference.Trust => 2, GoalPreference.Honour => 1, _ => 0 },
            "retreat" => axis switch { GoalPreference.Survival => 4, GoalPreference.Patience => 1, GoalPreference.Risk => -2, GoalPreference.Competition => -1, _ => 0 },
            "politics" => axis switch { GoalPreference.Ambition => 4, GoalPreference.Honour or GoalPreference.Discipline => 2, GoalPreference.Competition or GoalPreference.Family => 1, _ => 0 },
            "cultivation" => axis switch { GoalPreference.SeekingDao => 4, GoalPreference.Discipline or GoalPreference.Patience => 3, GoalPreference.Survival => 1, _ => 0 },
            "martial" => axis switch { GoalPreference.Competition or GoalPreference.Discipline => 3, GoalPreference.SeekingDao => 2, GoalPreference.Risk => 1, _ => 0 },
            _ => 0
        };
        return Math.Clamp(preferences.Sum(p => p.Amount * Weight(p.Preference)) / 100, -500, 500);
    }

    /// <summary>No actual other-person aptitude, title, body, wealth or world ranking is read here.</summary>
    internal SocialJudgment PeopleSocialJudgment(Person observer, long subjectId) =>
        Reputation.Judge(state.PeopleDomain.Reputation, new(observer.Id, subjectId, Month));
}
