namespace Chronicles.Game.Exploration;

public enum SecretEntryStatus { Active, Returning, Completed, Failed, Returned, Aborted }
public sealed record SecretEntryRules(long Revision, int Period, int OpenMonths, int Months, int Capacity,
    Realm MinimumRealm, int Food, int ToolWear, int Risk, int Injury);
public sealed class SecretSite
{
    public int Place { get; set; }
    public string Name { get; set; } = "";
    public int Offset { get; set; }
    public string RewardDefinition { get; set; } = "";
    public int InitialStock { get; set; }
    public int Remaining { get; set; }
    public long OriginRevision { get; set; }
    public string Source { get; set; } = "";
}
public sealed record EntranceObservation(long Person, long Body, int Place, long Month, int Period, int OpenMonths,
    int Offset, Realm MinimumRealm, int Capacity, int FoodCost, int ObservedOccupancy, string? RewardDefinition,
    int? ObservedStock, string Source);
public sealed class SecretEntry
{
    public long Id { get; set; }
    public long Person { get; set; }
    public long Body { get; set; }
    public long Soul { get; set; }
    public int Place { get; set; }
    public long Started { get; set; }
    public long Due { get; set; }
    public long Ended { get; set; }
    public SecretEntryStatus Status { get; set; }
    public SecretEntryRules Rules { get; set; } = null!;
    public int Progress { get; set; }
    public long Tool { get; set; }
    public long ToolReservation { get; set; }
    public long FoodReservation { get; set; }
    public long FoodConsumptionSequence { get; set; }
    public long FoodFinishedSequence { get; set; }
    public int ToolQuality { get; set; }
    public string KnowledgeSource { get; set; } = "";
    public int Understanding { get; set; }
    public int Risk { get; set; }
    public int Roll { get; set; }
    public bool RiskApplied { get; set; }
    public int Injury { get; set; }
    public long RewardItem { get; set; }
    public int RewardQuantity { get; set; }
    public string Source { get; set; } = "";
    public string EndReason { get; set; } = "";
}
public sealed class ExplorationState
{
    public bool Initialized { get; set; }
    public long NextId { get; set; } = 1;
    public List<SecretSite> Sites { get; set; } = [];
    public List<EntranceObservation> Observations { get; set; } = [];
    public List<SecretEntry> Entries { get; set; } = [];
}

public sealed record EntranceView(int Place, string Name, long Observed, int Period, int OpenMonths, int Offset,
    string MinimumRealm, int Capacity, int FoodCost, int ObservedOccupancy, string? RewardDefinition, int? ObservedStock,
    bool? OpenNow, IReadOnlyList<long>? ActualParticipants, string Source);
public sealed record SecretProgressView(long Id, int Place, string Status, int Progress, int Required,
    long Started, long Due, long Ended, int FoodSpent, long Tool, int Injury, long RewardItem,
    int RewardQuantity, string Preparation, string EndReason)
{
    public long Person { get; init; }
    public long? ActualBody { get; init; }
    public long? ActualSoul { get; init; }
    public SecretEntryRules? ActualRules { get; init; }
    public int? ActualRisk { get; init; }
    public int? ActualRoll { get; init; }
    public int? ActualToolQuality { get; init; }
    public string? ActualKnowledgeSource { get; init; }
}
public sealed record ExplorationView(IReadOnlyList<EntranceView> Entrances, IReadOnlyList<SecretProgressView> OwnEntries);

public static class ExplorationAuthority
{
    public static bool Occupies(SecretEntry entry) => entry.Status is SecretEntryStatus.Active or SecretEntryStatus.Returning;
    public static bool Open(long month, int offset, int period, int openMonths) =>
        ((month - offset) % period + period) % period < openMonths;

    public static void Validate(GameState state)
    {
        var data = state.Exploration;
        if (data is null || !data.Initialized || data.NextId <= 0 || data.Sites is null || data.Entries is null || data.Observations is null ||
            data.Sites.Count != 10 || data.Sites.Select(s => s.Place).Distinct().Count() != 10 ||
            data.Entries.Select(e => e.Id).Distinct().Count() != data.Entries.Count ||
            data.NextId <= data.Entries.Select(e => e.Id).DefaultIfEmpty().Max())
            throw new InvalidDataException("비경 원천·입장 식별자가 초기 세계와 일치하지 않습니다.");
        foreach (var site in data.Sites)
        {
            if (site.Place is < 0 or >= 100 || site.Place % 10 != 8 || site.Offset != site.Place / 10 ||
                string.IsNullOrWhiteSpace(site.Name) || !Catalog.Items.ContainsKey(site.RewardDefinition) || site.InitialStock < 1 ||
                site.Remaining < 0 || site.Remaining > site.InitialStock || site.OriginRevision <= 0 || site.OriginRevision > state.LawRevision ||
                string.IsNullOrWhiteSpace(site.Source) || site.InitialStock - site.Remaining != data.Entries.Where(e => e.Place == site.Place).Sum(e => e.RewardQuantity))
                throw new InvalidDataException("비경의 유한 보상 원천과 실제 선점 결과가 일치하지 않습니다.");
        }
        foreach (var o in data.Observations)
        {
            var person = state.People.SingleOrDefault(p => p.Id == o.Person);
            if (person is null || person.BodyId != o.Body || !data.Sites.Any(s => s.Place == o.Place) || o.Month < 0 || o.Month > state.Month ||
                o.Period < 1 || o.OpenMonths < 1 || o.OpenMonths > o.Period || o.Offset != o.Place / 10 || !Enum.IsDefined(o.MinimumRealm) ||
                o.Capacity is < 1 or > 16 || o.FoodCost is < 1 or > 100 || o.ObservedOccupancy < 0 || o.ObservedStock < 0 ||
                o.RewardDefinition is not null && !Catalog.Items.ContainsKey(o.RewardDefinition) || string.IsNullOrWhiteSpace(o.Source))
                throw new InvalidDataException("비경 입구의 개인 관측·출처가 올바르지 않습니다.");
        }
        foreach (var e in data.Entries)
        {
            var person = state.People.SingleOrDefault(p => p.Id == e.Person);
            var rules = e.Rules;
            if (e.Id <= 0 || person is null || person.BodyId != e.Body || person.SoulId != e.Soul || !data.Sites.Any(s => s.Place == e.Place) ||
                e.Started < 0 || e.Started > state.Month || e.Ended < 0 || e.Ended > state.Month || !Enum.IsDefined(e.Status) ||
                rules is null || rules.Revision <= 0 || rules.Revision > state.LawRevision || rules.Period < 1 || rules.OpenMonths < 1 || rules.OpenMonths > rules.Period ||
                rules.Months is < 2 or > 120 || rules.Capacity is < 1 or > 16 || !Enum.IsDefined(rules.MinimumRealm) ||
                rules.Food is < 1 or > 100 || rules.ToolWear is < 0 or > 100 || rules.Risk is < 0 or > 10000 || rules.Injury is < 0 or > 100 ||
                e.Progress < 0 || e.Progress > rules.Months || e.Tool <= 0 || e.ToolReservation <= 0 || e.FoodReservation <= 0 ||
                e.FoodConsumptionSequence <= 0 || e.FoodFinishedSequence < e.FoodConsumptionSequence ||
                e.ToolQuality is < 0 or > 10000 || e.Understanding is < 0 or > 100 || e.Risk is < 0 or > 10000 || e.Roll is < 0 or >= 10000 ||
                e.Injury < 0 || e.Injury > rules.Injury || e.RewardQuantity < 0 || string.IsNullOrWhiteSpace(e.Source) || string.IsNullOrWhiteSpace(e.KnowledgeSource))
                throw new InvalidDataException("비경 입장의 실제 몸·시간·준비·위험 영수증이 올바르지 않습니다.");
            var consumed = state.Inventory.Events.Where(r => r.Sequence >= e.FoodConsumptionSequence && r.Sequence <= e.FoodFinishedSequence &&
                r.Kind == "physical-consumption" && r.Actor == e.Person && r.Definition == "food" && !r.IsLoss).Sum(r => r.Quantity);
            if (consumed != rules.Food || !state.Inventory.Reservations.Any(r => r.Id == e.FoodReservation && r.Actor == e.Person && !r.Active) ||
                e.RewardQuantity > 0 && (e.Status != SecretEntryStatus.Completed || e.RewardItem <= 0))
                throw new InvalidDataException("비경 준비 식량 소비와 실제 보상이 원장과 일치하지 않습니다.");
            if (Occupies(e))
            {
                if (person.Status != LifeStatus.Alive || person.Place != e.Place || person.Action != ActionKind.Explore ||
                    person.ActionTarget != e.Id || person.ActionData != "exploration:" + e.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
                    person.Due != e.Due || e.Due <= state.Month || e.Due > state.Month + 1 || e.Ended != 0 ||
                    !state.Inventory.Reservations.Any(r => r.Id == e.ToolReservation && r.Actor == e.Person && r.Active && r.Items.Contains(e.Tool)) ||
                    !state.Items.Any(i => i.Id == e.Tool && i.Owner == e.Person && i.Reserved == e.ToolReservation))
                    throw new InvalidDataException("비경의 실제 입장 자리·노동·도구 예약이 사라졌습니다.");
                var root = state.Items.Single(i => i.Id == e.Tool);
                while (root.Container != 0) root = state.Items.Single(i => i.Id == root.Container);
                var custody = state.Inventory.Metadata.FirstOrDefault(m => m.Item == root.Id);
                var holder = custody?.Carrier > 0 ? custody.Carrier : custody?.Custodian > 0 ? custody.Custodian : root.Owner;
                if (root.Place != -1 || holder != e.Person)
                    throw new InvalidDataException("비경 조사 도구가 실제 입장자의 휴대 실물이 아닙니다.");
            }
            else if (e.Ended < e.Started || state.Inventory.Reservations.Any(r => r.Id == e.ToolReservation && r.Active))
                throw new InvalidDataException("종료한 비경 입장이 실제 도구 예약을 계속 보유합니다.");
        }
        if (data.Entries.Where(Occupies).GroupBy(e => e.Person).Any(g => g.Count() > 1) ||
            data.Entries.Where(Occupies).GroupBy(e => e.Place).Any(g => g.Count() > g.Max(e => e.Rules.Capacity)))
            throw new InvalidDataException("비경의 실제 참여자·입장 자리가 중복되었습니다.");
    }
}
