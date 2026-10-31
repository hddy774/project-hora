using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    internal string? PeopleActionError(Person person, string kind)
    {
        kind = kind.ToLowerInvariant();
        var profile = state.PeopleDomain.Profiles.FirstOrDefault(p => p.PersonId == person.Id);
        if (profile is null) return "현재 육신 프로필이 없습니다.";
        var body = profile.Body; var species = SpeciesCatalog.Get(body.SpeciesId);
        if (kind == "travel" && !body.Capabilities.Mobility) return "현재 육신에는 실제 이동 능력/운반 수단이 없습니다.";
        if (kind == "martial" && !body.Capabilities.Mobility && !body.Capabilities.SpiritualManipulation)
            return "현재 육신에는 무예 동작에 필요한 실제 이동/기운 조작 능력이 없습니다.";
        var participation = kind switch
        {
            "craft" or "repair" or "transcribe" or "installarray" or "upkeeparray" or "equip" or "store" or "redeemcurrency" => ParticipationKind.Craft,
            "farm" => ParticipationKind.Agriculture,
            "teach" or "requestteaching" or "requestassessment" or "requestreputation" or "requestoath" or "acceptpeoplerequest" or "rejectpeoplerequest" => ParticipationKind.Conversation,
            "govern" or "policy" or "adoptpolicy" or "preparepolicy" or "implementpolicy" or "found" => ParticipationKind.Governance,
            "martial" => ParticipationKind.Martial,
            _ => (ParticipationKind?)null
        };
        return participation is { } activity && !Embodiments.CanParticipate(species, body, activity)
            ? "현재 육신의 실제 조작·의사소통 능력으로 수행할 수 없습니다. 명시적인 신체/기운 적응이 필요합니다." : null;
    }

    public PeopleEcologyObservation? PeopleEcology(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return null;
        return state.PeopleDomain.Social.Ecology.LastOrDefault(e => e.Person == personId);
    }

    internal int PeoplePhysiologyPreference(Person person, string goal)
    {
        var own = state.PeopleDomain.Social.Ecology.LastOrDefault(e => e.Person == person.Id && e.Body == person.BodyId);
        if (own is null || own.Month > Month || Month - own.Month > 2 || own.HealthCost == 0) return 0;
        return goal switch { "retreat" => 180, "information" => 120, "production" => own.ActualAccommodation ? 0 : 100, _ => 0 };
    }

    private void SettlePeopleEcology(Person person)
    {
        if (person.Species == "human") return;
        var profile = PeopleProfile(person); var species = SpeciesCatalog.Get(profile.Body.SpeciesId); var place = state.Places[person.Place];
        var habitat = place.Terrain switch
        {
            "숲" => HabitatKind.Forest, "산지" => HabitatKind.Mountain, "하천" => HabitatKind.Water,
            "습지" => HabitatKind.Marsh, "사막" => HabitatKind.Desert, _ => HabitatKind.Grassland
        };
        var suitable = Embodiments.AssessHabitat(species, habitat).Suitable;
        // A completed farm is an actual soil/water-care accommodation, not a newly generated resource.
        var accommodation = profile.Body.CurrentForm == BodyForm.RootedPlant && place.Facilities.Contains("경작지");
        var qiNeeded = species.ResourceNeeds.Contains("ambient-qi", StringComparer.Ordinal);
        var qiConsumed = qiNeeded && place.Qi > 0 ? 1 : 0;
        if (qiConsumed != 0) place.Qi--;
        var cost = (suitable || accommodation ? 0 : 1) + (qiNeeded && qiConsumed == 0 ? 1 : 0);
        var source = new PeopleSource($"physiology:{person.BodyId}:{Month}", PeopleSourceKind.Development,
            "현재 몸이 직접 겪은 서식지. 기존 유한 food 소모는 먹이·물·식물 관리의 보편 유지 자원 추상화; 별도 물/토양 재고를 생성하지 않음. " +
            (qiNeeded ? $"수령 유지에 실제 현지 기운 {qiConsumed} 사용." : ""), Month, state.LawRevision, person.Id, person.BodyId, person.SoulId);
        var observation = new PeopleEcologyObservation(person.Id, person.BodyId, Month, person.Place, habitat, suitable,
            accommodation, true, qiConsumed, cost, source);
        SetPeopleSocial(state.PeopleDomain.Social with { Ecology = state.PeopleDomain.Social.Ecology.Where(e => e.Person != person.Id || Month - e.Month < 12)
            .Append(observation).OrderBy(e => e.Month).ThenBy(e => e.Person).ToArray() });
        if (cost > 0)
        {
            person.Health = Math.Max(0, person.Health - cost);
            person.Reason = "직접 겪은 서식지/기운 유지 조건 부족·실제 보호 환경 또는 이동 수단 필요";
            Remember(person, $"physiology:{person.BodyId}", $"현지 {habitat} 적합={suitable}, 실제 적응시설={accommodation}, 유지 건강비용={cost}", source.CauseId, 100);
            if (person.Health == 0) EndLife(person, 3, "실제 서식지·기운 유지 조건 부족으로 육신 붕괴");
        }
    }
}
