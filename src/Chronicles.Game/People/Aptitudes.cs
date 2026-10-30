namespace Chronicles.Game.People;

public enum FoundationKind { SpiritRoot, Bone, Constitution, Comprehension, Mind, Soul }
public enum ElementRoot { Metal, Wood, Water, Fire, Earth }
public enum AptitudeInputKind { Foundation, Element, MinimumElement, Talent }
public enum CognitionCapability { ObservationAttention, WorkingMemory, MemoryRetrieval, CausalReasoning, AlternativeComparison, LongTermPlanning, SelfMonitoring, SocialReasoning, EmotionalRegulation, DomainExpertise }

public sealed record InnateProfile(int[] Foundations, int[] ElementRoots, PeopleSource[] Sources)
{
    public void Validate()
    {
        PeopleChecks.Scores(Foundations, 6, nameof(Foundations));
        PeopleChecks.Scores(ElementRoots, 5, nameof(ElementRoots));
        foreach (var source in Sources) source.Validate();
        if (Sources.Length == 0) throw new InvalidDataException("Innate aptitude requires provenance.");
    }
}

public sealed record TalentDevelopment(int[] Learned, int[] Understanding)
{
    public static TalentDevelopment Empty() => new(new int[24], new int[24]);
    public void Validate() { PeopleChecks.Scores(Learned, 24, nameof(Learned)); PeopleChecks.Scores(Understanding, 24, nameof(Understanding)); }
}

/// <summary>Developer-tuned provisional coefficients, persisted with the module state.</summary>
public sealed record PeopleTuning(string PolicyId = "PC-TEMP-V021-PEOPLE-01", long Revision = 1,
    int TalentInnateWeight = 6000, int TalentLearnedWeight = 2500, int TalentUnderstandingWeight = 1500,
    int CognitionInnateWeight = 8000, int CognitionPracticeWeight = 2000,
    string Rationale = "Neutral initial coefficients; development and understanding remain separate from innate aptitude.", bool RequiresReview = true)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PolicyId);
        if (Revision < 1 || TalentInnateWeight < 0 || TalentLearnedWeight < 0 || TalentUnderstandingWeight < 0 ||
            TalentInnateWeight + TalentLearnedWeight + TalentUnderstandingWeight != 10_000 ||
            CognitionInnateWeight < 0 || CognitionPracticeWeight < 0 || CognitionInnateWeight + CognitionPracticeWeight != 10_000)
            throw new InvalidDataException("Invalid people tuning weights.");
    }
}

public readonly record struct AptitudeWeight(AptitudeInputKind Kind, int Index, int BasisPoints);
public sealed record TalentDefinition(int Index, string Name, AptitudeWeight[] Inputs, string LearningPath);
public sealed record CognitionDefinition(CognitionCapability Capability, string Name, AptitudeWeight[] Inputs, int[] PracticeTalents, string LearningPath);
public sealed record AptitudeContext(InnateProfile Innate, TalentDevelopment Development, int PhysicalCondition, int MentalCondition,
    PeopleEffect[] Effects, PeopleTuning Tuning, int DomainTalentIndex = 9);
public sealed record TalentValues(int Index, int Innate, int Learned, int Understanding, int CurrentExpression);
public sealed record CognitionValues(CognitionCapability Capability, int Innate, int Practice, int CurrentExpression);
public sealed record AptitudeResult(TalentValues[] Talents, CognitionValues[] Cognition, EffectResolution Effects, string PolicyId, long PolicyRevision);

public static class AptitudeCatalog
{
    private static AptitudeWeight F(FoundationKind index, int weight) => new(AptitudeInputKind.Foundation, (int)index, weight);
    private static AptitudeWeight E(ElementRoot index, int weight) => new(AptitudeInputKind.Element, (int)index, weight);
    private static AptitudeWeight T(int index, int weight) => new(AptitudeInputKind.Talent, index, weight);

    public static IReadOnlyList<string> FoundationNames { get; } = Array.AsReadOnly(new[] { "영근", "근골", "체질", "오성", "정신", "영혼" });
    public static IReadOnlyList<TalentDefinition> Talents { get; } = Array.AsReadOnly<TalentDefinition>(
    [
        new(0, "금행 감응", [E(ElementRoot.Metal, 6000), F(FoundationKind.SpiritRoot, 2000), F(FoundationKind.Mind, 2000)], "금행 토납·실제 감응 훈련"),
        new(1, "목행 감응", [E(ElementRoot.Wood, 6000), F(FoundationKind.SpiritRoot, 2000), F(FoundationKind.Constitution, 2000)], "목행 토납·실제 감응 훈련"),
        new(2, "수행 감응", [E(ElementRoot.Water, 6000), F(FoundationKind.SpiritRoot, 2000), F(FoundationKind.Mind, 2000)], "수행 토납·실제 감응 훈련"),
        new(3, "화행 감응", [E(ElementRoot.Fire, 6000), F(FoundationKind.SpiritRoot, 2000), F(FoundationKind.Mind, 2000)], "화행 토납·실제 감응 훈련"),
        new(4, "토행 감응", [E(ElementRoot.Earth, 6000), F(FoundationKind.SpiritRoot, 2000), F(FoundationKind.Bone, 2000)], "토행 토납·실제 감응 훈련"),
        new(5, "오행 조화", [new(AptitudeInputKind.MinimumElement, 0, 4000), F(FoundationKind.SpiritRoot, 3000), F(FoundationKind.Comprehension, 3000)], "오행 균형·순환 실습"),
        new(6, "토납·감응", [F(FoundationKind.SpiritRoot, 5000), F(FoundationKind.Mind, 3000), F(FoundationKind.Constitution, 2000)], "감응·흡수 수행"),
        new(7, "운기·경맥", [F(FoundationKind.SpiritRoot, 3000), F(FoundationKind.Bone, 4000), F(FoundationKind.Mind, 3000)], "경맥 순환 훈련"),
        new(8, "정련·체화", [F(FoundationKind.Constitution, 4000), F(FoundationKind.Bone, 3000), F(FoundationKind.SpiritRoot, 3000)], "정련·체화 수행"),
        new(9, "연체·무도", [F(FoundationKind.Bone, 6000), F(FoundationKind.Constitution, 3000), F(FoundationKind.Mind, 1000)], "육체 단련·무도 실습"),
        new(10, "신식·탐지", [F(FoundationKind.Mind, 6500), F(FoundationKind.Soul, 3500)], "탐지·관측 훈련"),
        new(11, "오도·추론", [F(FoundationKind.Comprehension, 7500), F(FoundationKind.Mind, 2500)], "원리 분석·반례 검토"),
        new(12, "법술 운용", [F(FoundationKind.Comprehension, 4500), F(FoundationKind.Mind, 4000), F(FoundationKind.SpiritRoot, 1500)], "배운 법술의 실제 시전"),
        new(13, "전투 판단", [F(FoundationKind.Comprehension, 4500), F(FoundationKind.Mind, 3500), F(FoundationKind.Bone, 2000)], "전투·대련·전투 복기"),
        new(14, "연단", [F(FoundationKind.Comprehension, 3500), F(FoundationKind.Mind, 2500), E(ElementRoot.Fire, 4000)], "단방 학습·실제 연단"),
        new(15, "연기(煉器)", [F(FoundationKind.Bone, 2500), F(FoundationKind.Comprehension, 3500), E(ElementRoot.Metal, 4000)], "제련·제작 실습"),
        new(16, "제부", [F(FoundationKind.Comprehension, 4500), F(FoundationKind.Mind, 3000), F(FoundationKind.SpiritRoot, 2500)], "부문 학습·실제 제부"),
        new(17, "진법", [F(FoundationKind.Comprehension, 5000), F(FoundationKind.Mind, 3500), F(FoundationKind.Soul, 1500)], "진도 분석·설치·검증"),
        new(18, "영농·본초", [F(FoundationKind.Comprehension, 3000), E(ElementRoot.Wood, 3500), E(ElementRoot.Earth, 3500)], "재배·본초 관찰"),
        new(19, "어수·생태", [F(FoundationKind.Mind, 6000), F(FoundationKind.Soul, 4000)], "생물 관찰·돌봄"),
        new(20, "의술·회복", [F(FoundationKind.Comprehension, 6000), F(FoundationKind.Mind, 4000)], "의서·실제 치료"),
        new(21, "감정·문헌", [F(FoundationKind.Comprehension, 6500), F(FoundationKind.Mind, 3500)], "대조·감정·문헌 검증"),
        new(22, "상재·교섭", [F(FoundationKind.Comprehension, 6000), F(FoundationKind.Mind, 4000)], "실제 거래·대화·계약 복기"),
        new(23, "경세·통솔", [F(FoundationKind.Comprehension, 5000), F(FoundationKind.Mind, 5000)], "행정·조직 운영·결과 검토")
    ]);

    public static IReadOnlyList<CognitionDefinition> Cognition { get; } = Array.AsReadOnly<CognitionDefinition>(
    [
        new(CognitionCapability.ObservationAttention, "관측·주의", [F(FoundationKind.Mind, 4000), F(FoundationKind.Soul, 2000), T(10, 4000)], [10], "관측 실습·주의 분산/피로 관리"),
        new(CognitionCapability.WorkingMemory, "작업 기억", [F(FoundationKind.Mind, 4000), F(FoundationKind.Soul, 2000), T(10, 2000), T(23, 2000)], [10, 23], "복수 의무/기한 관리·도구/기록 사용"),
        new(CognitionCapability.MemoryRetrieval, "기억 인출", [F(FoundationKind.Soul, 3000), F(FoundationKind.Mind, 3000), T(21, 2000), T(11, 2000)], [21, 11], "관련 사건 회상·판본 대조·혼흔 접근"),
        new(CognitionCapability.CausalReasoning, "인과 추론", [F(FoundationKind.Comprehension, 4000), F(FoundationKind.Mind, 2000), T(11, 2000), T(21, 2000)], [11, 21], "실패/반례·실험·지도·가설 교정"),
        new(CognitionCapability.AlternativeComparison, "대안 비교", [F(FoundationKind.Comprehension, 3000), F(FoundationKind.Mind, 3000), T(22, 2000), T(23, 2000)], [22, 23], "실제 거래/이동·자원/위험 비교"),
        new(CognitionCapability.LongTermPlanning, "장기 계획", [F(FoundationKind.Comprehension, 3000), F(FoundationKind.Mind, 3000), T(23, 2000), T(-1, 2000)], [23, -1], "장기 수행·부양·제작/운송 계획 실행/교정"),
        new(CognitionCapability.SelfMonitoring, "자기 점검", [F(FoundationKind.Comprehension, 3000), F(FoundationKind.Mind, 3000), T(11, 2000), T(21, 2000)], [11, 21], "예상-관측 오차·자기 오해/한계 확인"),
        new(CognitionCapability.SocialReasoning, "사회 추론", [F(FoundationKind.Comprehension, 3000), F(FoundationKind.Mind, 3000), T(22, 2000), T(23, 2000)], [22, 23], "대화·약속/거절·관계/협상 경험"),
        new(CognitionCapability.EmotionalRegulation, "정서·도심 조절", [F(FoundationKind.Mind, 4000), F(FoundationKind.Soul, 2000), T(10, 2000), T(11, 2000)], [10, 11], "정의된 감정 회복·도심/경험; 성향 가치와 정확도 분리"),
        new(CognitionCapability.DomainExpertise, "영역 숙련", [T(-1, 10000)], [-1], "관련24재능 영역의 실제 학습·이해·경험")
    ]);
}

public static class Aptitudes
{
    /// <summary>Pure compatibility projection for Catalog; all 24 formulas remain in one source map.</summary>
    public static int InnateTalent(int[] foundations, int[] elementRoots, int index)
    {
        PeopleChecks.Scores(foundations, 6, nameof(foundations)); PeopleChecks.Scores(elementRoots, 5, nameof(elementRoots));
        if ((uint)index >= 24) throw new ArgumentOutOfRangeException(nameof(index));
        return Weighted(AptitudeCatalog.Talents[index].Inputs, new(foundations, elementRoots, []), []);
    }

    public static int AbsoluteIndex(InnateProfile innate)
    {
        ArgumentNullException.ThrowIfNull(innate);
        innate.Validate();
        return innate.Foundations.Sum();
    }

    public static AptitudeResult Assess(AptitudeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Innate.Validate(); context.Development.Validate(); context.Tuning.Validate();
        PeopleChecks.Score(context.PhysicalCondition, nameof(context.PhysicalCondition));
        PeopleChecks.Score(context.MentalCondition, nameof(context.MentalCondition));
        if ((uint)context.DomainTalentIndex >= 24) throw new InvalidDataException("Domain talent must be in 0..23.");
        var effects = PeopleEffects.Resolve(context.Effects);
        var innateTalents = AptitudeCatalog.Talents.Select(t => Weighted(t.Inputs, context.Innate, [])).ToArray();
        var tuning = context.Tuning;
        var talents = AptitudeCatalog.Talents.Select(t =>
        {
            var learned = context.Development.Learned[t.Index]; var understanding = context.Development.Understanding[t.Index];
            var expression = (innateTalents[t.Index] * tuning.TalentInnateWeight + learned * tuning.TalentLearnedWeight +
                understanding * tuning.TalentUnderstandingWeight) / 10_000;
            expression = (int)effects.Apply(new(EffectDomain.Talent, t.Index), expression);
            var condition = t.Index == 9 ? context.PhysicalCondition : context.MentalCondition;
            return new TalentValues(t.Index, innateTalents[t.Index], learned, understanding, expression * condition / 10_000);
        }).ToArray();
        var cognition = AptitudeCatalog.Cognition.Select(c =>
        {
            var inputs = c.Inputs.Select(w => w.Kind == AptitudeInputKind.Talent && w.Index == -1 ? w with { Index = context.DomainTalentIndex } : w).ToArray();
            var innate = Weighted(inputs, context.Innate, innateTalents);
            var practice = c.PracticeTalents.Select(i => i == -1 ? context.DomainTalentIndex : i).Sum(i => (context.Development.Learned[i] + context.Development.Understanding[i]) / 2) / c.PracticeTalents.Length;
            var expression = (innate * tuning.CognitionInnateWeight + practice * tuning.CognitionPracticeWeight) / 10_000;
            expression = (int)effects.Apply(new(EffectDomain.Cognition, (int)c.Capability), expression);
            return new CognitionValues(c.Capability, innate, practice, expression * context.MentalCondition / 10_000);
        }).ToArray();
        return new(talents, cognition, effects, tuning.PolicyId, tuning.Revision);
    }

    private static int Weighted(AptitudeWeight[] weights, InnateProfile innate, int[] talents)
    {
        if (weights.Sum(w => w.BasisPoints) != 10_000) throw new InvalidDataException("Aptitude source weights must total 100%.");
        return (int)(weights.Sum(w => (long)w.BasisPoints * (w.Kind switch
        {
            AptitudeInputKind.Foundation => innate.Foundations[w.Index],
            AptitudeInputKind.Element => innate.ElementRoots[w.Index],
            AptitudeInputKind.MinimumElement => innate.ElementRoots.Min(),
            AptitudeInputKind.Talent => talents[w.Index],
            _ => throw new InvalidDataException("Unknown aptitude input.")
        })) / 10_000);
    }
}

internal static class PeopleChecks
{
    internal static void Score(int value, string name)
    {
        if (value is < 0 or > 10_000) throw new InvalidDataException($"{name} must be in 0..10000.");
    }

    internal static void Scores(int[] values, int length, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length != length) throw new InvalidDataException($"{name} must contain exactly {length} values.");
        foreach (var value in values) Score(value, name);
    }
}
