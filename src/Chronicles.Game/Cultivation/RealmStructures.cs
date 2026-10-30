using System.Text.Json.Serialization;

namespace Chronicles.Game.Cultivation;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$structure")]
[JsonDerivedType(typeof(QiStructure), "qi")]
[JsonDerivedType(typeof(FoundationStructure), "foundation")]
[JsonDerivedType(typeof(GoldenCoreStructure), "goldenCore")]
[JsonDerivedType(typeof(NascentSoulStructure), "nascentSoul")]
[JsonDerivedType(typeof(SpiritStructure), "spirit")]
[JsonDerivedType(typeof(VoidStructure), "void")]
[JsonDerivedType(typeof(IntegrationStructure), "integration")]
[JsonDerivedType(typeof(TribulationStructure), "tribulation")]
[JsonDerivedType(typeof(MahayanaStructure), "mahayana")]
[JsonDerivedType(typeof(HalfImmortalStructure), "halfImmortal")]
public abstract record RealmStructure
{
    [JsonIgnore] public abstract Realm Realm { get; }
    [JsonIgnore] public abstract IReadOnlyList<long> Measures { get; }
    [JsonIgnore] public long Total => Measures.Sum();
    public abstract RealmStructure WithMeasures(IReadOnlyList<long> values);
}

public sealed record QiStructure(long Purification = 0, long MeridianPractice = 0, long BodyAdaptation = 0, long SpiritAwareness = 0) : RealmStructure
{
    public override Realm Realm => Realm.Qi;
    public override IReadOnlyList<long> Measures => [Purification, MeridianPractice, BodyAdaptation, SpiritAwareness];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new QiStructure(values[0], values[1], values[2], values[3]);
}
public sealed record FoundationStructure(long RootLink = 0, long DantianCapacity = 0, long MeridianNetwork = 0, long FoundationStability = 0, long ElementHarmony = 0) : RealmStructure
{
    public override Realm Realm => Realm.Foundation;
    public override IReadOnlyList<long> Measures => [RootLink, DantianCapacity, MeridianNetwork, FoundationStability, ElementHarmony];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new FoundationStructure(values[0], values[1], values[2], values[3], values[4]);
}
public sealed record GoldenCoreStructure(long Expansion = 0, long Refinement = 0, long Reinforcement = 0, long Circulation = 0) : RealmStructure
{
    public override Realm Realm => Realm.GoldenCore;
    public override IReadOnlyList<long> Measures => [Expansion, Refinement, Reinforcement, Circulation];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new GoldenCoreStructure(values[0], values[1], values[2], values[3]);
}
public sealed record NascentSoulStructure(long InfantMaturity = 0, long Nourishment = 0, long CommandPrecision = 0, long MeridianLink = 0) : RealmStructure
{
    public override Realm Realm => Realm.NascentSoul;
    public override IReadOnlyList<long> Measures => [InfantMaturity, Nourishment, CommandPrecision, MeridianLink];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new NascentSoulStructure(values[0], values[1], values[2], values[3]);
}
public sealed record SpiritStructure(long AdultMaturity = 0, long SubspiritFormation = 0, long ControlCapacity = 0, long Synchronization = 0) : RealmStructure
{
    public override Realm Realm => Realm.Spirit;
    public override IReadOnlyList<long> Measures => [AdultMaturity, SubspiritFormation, ControlCapacity, Synchronization];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new SpiritStructure(values[0], values[1], values[2], values[3]);
}
public sealed record VoidStructure(long ClearedDefects = 0, long RebuiltChannels = 0, long ElementReharmonization = 0, long VerifiedCapacity = 0) : RealmStructure
{
    public override Realm Realm => Realm.Void;
    public override IReadOnlyList<long> Measures => [ClearedDefects, RebuiltChannels, ElementReharmonization, VerifiedCapacity];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new VoidStructure(values[0], values[1], values[2], values[3]);
}
public sealed record IntegrationStructure(long MainSpiritFlow = 0, long SubspiritFlow = 0, long Coherence = 0, long BondStrength = 0) : RealmStructure
{
    public override Realm Realm => Realm.Integration;
    public override IReadOnlyList<long> Measures => [MainSpiritFlow, SubspiritFlow, Coherence, BondStrength];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new IntegrationStructure(values[0], values[1], values[2], values[3]);
}
public sealed record TribulationStructure(long Preparedness = 0, long Aftercare = 0, long ResponseCoordination = 0, int CompletedTrials = 0) : RealmStructure
{
    public override Realm Realm => Realm.Tribulation;
    public override IReadOnlyList<long> Measures => [Preparedness, Aftercare, ResponseCoordination];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new TribulationStructure(values[0], values[1], values[2], CompletedTrials);
}
public sealed record MahayanaStructure(long TechniqueReconciliation = 0, long BodyMindUnity = 0, long TransitionAdaptation = 0) : RealmStructure
{
    public override Realm Realm => Realm.Mahayana;
    public override IReadOnlyList<long> Measures => [TechniqueReconciliation, BodyMindUnity, TransitionAdaptation];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new MahayanaStructure(values[0], values[1], values[2]);
}
public sealed record HalfImmortalStructure(long Maintenance = 0, long RealmAdaptation = 0, long PassageReadiness = 0) : RealmStructure
{
    public override Realm Realm => Realm.HalfImmortal;
    public override IReadOnlyList<long> Measures => [Maintenance, RealmAdaptation, PassageReadiness];
    public override RealmStructure WithMeasures(IReadOnlyList<long> values) => new HalfImmortalStructure(values[0], values[1], values[2]);
}

public sealed record CultivationPathProfile(PathDefinition Definition, IReadOnlyList<int> GrowthWeights,
    string Strength, string Burden, string NextRequirement)
{
    public int Variant => Definition.Id[^1] - '0';
}

public static class CultivationProfiles
{
    public static IReadOnlyList<CultivationPathProfile> Paths { get; } = BuildPaths();
    public static CultivationPathProfile? Find(string id) => Paths.FirstOrDefault(p => p.Definition.Id == id);
    public static RealmStructure Empty(Realm realm) => realm switch
    {
        Realm.Qi => new QiStructure(), Realm.Foundation => new FoundationStructure(), Realm.GoldenCore => new GoldenCoreStructure(),
        Realm.NascentSoul => new NascentSoulStructure(), Realm.Spirit => new SpiritStructure(), Realm.Void => new VoidStructure(),
        Realm.Integration => new IntegrationStructure(), Realm.Tribulation => new TribulationStructure(),
        Realm.Mahayana => new MahayanaStructure(), Realm.HalfImmortal => new HalfImmortalStructure(),
        _ => throw new ArgumentOutOfRangeException(nameof(realm))
    };

    public static IReadOnlyList<string> FormationKeys(Realm realm) => realm switch
    {
        Realm.Qi => ["감응", "정탁", "경맥", "육신적응", "신식"],
        // These ten measurable implementation fields are provisional, not a new quality ladder.
        Realm.Foundation => ["영근연결", "단전용량", "경맥연결", "기반완전성", "기반안정", "오행조화", "순도", "운행제어", "육신수용", "신식적합"],
        Realm.GoldenCore => ["규모", "밀도", "순도", "완전성", "안정성", "오행조화", "경맥연계"],
        Realm.NascentSoul => ["영태완전성", "정양순환", "신식명령", "경맥연계"],
        Realm.Spirit => ["성체성숙", "분령형성", "제어용량", "동조"],
        Realm.Void => ["결함제거", "연결재구축", "재조화", "검증"],
        Realm.Integration => ["본령합류", "분령합류", "상충완화", "결속"],
        Realm.Tribulation => ["응겁준비", "겁후회복", "대응협응"],
        Realm.Mahayana => ["공법회통", "형신원융", "전이적응"],
        Realm.HalfImmortal => ["온양", "계역적응", "통로준비"],
        _ => []
    };

    public static CultivationState ForNewBody(long body, long soul, string rootOrigin) =>
        new() { BodyId = body, SoulId = soul, RootOrigin = rootOrigin };

    public static CultivationState ForFounder(long body, long soul, long month, CultivationPolicy policy, string path = "path.10.0")
    {
        var profile = Find(path) ?? throw new ArgumentException("Unknown founder path.", nameof(path));
        if (profile.Definition.Realm != Realm.HalfImmortal) throw new ArgumentException("Founder path must be Half Immortal.", nameof(path));
        var structure = Empty(Realm.HalfImmortal);
        var required = policy.RequiredWork(Realm.HalfImmortal);
        structure = structure.WithMeasures(Enumerable.Range(0, structure.Measures.Count).Select(i => required * 6 / 10 / structure.Measures.Count).ToArray());
        return new()
        {
            BodyId = body, SoulId = soul, RootOrigin = "창세 조건", ActivePath = path,
            RootGrowth = Elements.Uniform(policy.RequiredWork(Realm.Qi) / 5), Structure = structure,
            Formations = [new(Realm.HalfImmortal, path, 7000, FormationKeys(Realm.HalfImmortal).Select(k => new FormationFactor(k, 7000)).ToArray(),
                month, 0, policy.Revision, null, null, "창세 조건")]
        };
    }

    private static CultivationPathProfile[] BuildPaths()
    {
        string[][] strengths =
        [
            ["주 속성 집중", "오행 전환", "정탁·지속"], ["자기 제어·이동", "진지 지속", "시기 연계"],
            ["비축 확장", "정밀 출력·내구", "전달·회복"], ["정양·방어", "신식 정밀", "이동 협응"],
            ["집중 지휘", "분령 역할 분담", "동조 협공"], ["결함 회복", "운용 폭", "복합 안정"],
            ["집중 합류", "완충 지속", "상생 대응"], ["내부 방호", "외부 대응", "협응 응변"],
            ["공법 교대", "전체 지속", "환경 전환"], ["저공급 유지", "계역 대응", "통로 준비"]
        ];
        string[][] burdens =
        [
            ["오행 격차", "전환 제어", "출력·용량"], ["자기 제어 결함", "실제 진법·공급", "실제 시기·재현성"],
            ["방출·제어 병목", "비축 부족", "경맥·육신 부하"], ["확장 시간", "정신 교란", "육신 손상"],
            ["동시 위협", "거리·영력·주의력", "동조 붕괴"], ["일시 약화", "재숙련", "특화 손실"],
            ["공통 교란", "순차 지연", "연쇄 상충"], ["치료 부담", "실물 소모", "오판·주의력"],
            ["연구·재숙련", "국부 성능", "조건 검증"], ["비축·급출력", "미지 조건", "장소·기회 의존"]
        ];
        string[] next = ["다섯 영근 완성", "기반 연결·결단", "파단 전 정양·결함 검사", "성체 성장 후 분령", "비움·재구축", "합류 결속 검사",
            "매 소경지 겁난", "겁후 회복·회통", "반선 유지·적응", "실제 통로·비승 판정"];
        return Catalog.Paths.Select(p =>
        {
            var r = (int)p.Realm - 1; var n = p.Id[^1] - '0'; var count = Empty(p.Realm).Measures.Count;
            var weights = Enumerable.Repeat(100, count).ToArray();
            if (n != 0) { weights[n % count] = 160; weights[(n + 1) % count] = 70; }
            return new CultivationPathProfile(p, Array.AsReadOnly(weights), strengths[r][n], burdens[r][n], next[r]);
        }).ToArray();
    }
}
