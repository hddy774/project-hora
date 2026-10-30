using System.Numerics;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Chronicles.Game;
using Chronicles.Game.Execution;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Experiments;

/// <summary>Explicit EX01/02/04 host measurement source. No feature scenario invokes Run.</summary>
internal static class ExecutionMeasurements
{
    private sealed class FailingBackend(bool partial) : ITalentBatchBackend
    {
        public string Id => partial ? "partial-output-discard-and-scalar" : "unsupported-and-scalar";
        public bool Calculate(TalentBatchInput input, Span<int> output)
        { if (partial && !output.IsEmpty) output[0] = 10001; return false; }
    }

    internal static void Run(ExecutionMeasurementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath) || File.Exists(request.OutputPath) || File.Exists(request.OutputPath + ".pending"))
            throw new ArgumentException("이전/부분 결과를 보존하도록 새 출력 경로를 지정하세요.", nameof(request));
        if (request.Repetitions < 1 || request.Populations.Length == 0 ||
            request.Populations.Distinct().Count() != request.Populations.Length ||
            request.Populations.Any(n => !SyntheticPopulationFixture.Sizes.Contains(n)))
            throw new ArgumentException("지원 규모와 독립 반복 수를 명시하세요.", nameof(request));
        var began = Stopwatch.GetTimestamp(); var host = ExecutionMeasurementHost.Capture(); var rows = new List<ExecutionMeasuredVariant>();
        var finished = new List<string>(); var requestedComplete = false; string? failure = null;
        void Save() => ExecutionMeasurementHost.Write(request.OutputPath, new
        {
            schema_version = 1, kind = "seonro-execution-ex01-ex02-ex04", failure, host,
            complete = requestedComplete && request.Populations.Order().SequenceEqual(SyntheticPopulationFixture.Sizes),
            requested_complete = requestedComplete,
            total_elapsed_nanoseconds = checked((long)((Int128)(Stopwatch.GetTimestamp() - began) * 1_000_000_000 / Stopwatch.Frequency)),
            requested_populations = request.Populations, repetitions = request.Repetitions, finished, rows,
            adoption = "revisit", mandatory_population_baseline_replacement = false,
            note = "EX02 별도 생사 경계 초기조건은 기본 인구 기준선을 대체하지 않는다. 전체시간은 소스확인/fixture/예열/GC/앞선 보고쓰기를 포함하며 마지막 보고쓰기만 제외한다. 부분규모는 complete가 아니다. 실제 표본 외 비용·플랫폼·전략 채택을 주장하지 않는다."
        });
        Save();
        try
        {
            foreach (var population in request.Populations)
            {
                for (var repetition = 0; repetition < request.Repetitions; repetition++)
                {
                    var initial = SyntheticPopulationFixture.Build(population);
                    WorldPair("EX-01", initial, [], population, repetition, host, rows);
                    finished.Add($"EX-01:population:{population}:repetition:{repetition}"); Save();
                    var lifecycle = LifecycleFixture(population);
                    WorldPair("EX-02", lifecycle.Initial, lifecycle.Script, population, repetition, host, rows);
                    finished.Add($"EX-02:population:{population}:repetition:{repetition}"); Save();
                    TalentWorld(initial, population, repetition, host, rows);
                    finished.Add($"EX-04:population:{population}:repetition:{repetition}"); Save();
                }
            }
            // The direct numerical cases add explicit empty, small, vector-boundary and larger batches.
            TalentKernel(SyntheticPopulationFixture.Build(2), host, rows, request.Repetitions);
            finished.Add("EX-04:direct-kernel-boundaries"); requestedComplete = true; Save();
        }
        catch (Exception e)
        { failure = e.GetType().Name + ": " + e.Message; Save(); throw; }
    }

    internal static (GameState Initial, ExecutionScript[] Script) LifecycleFixture(int population)
    {
        var state = SyntheticPopulationFixture.Build(population);
        // Deliberately declared EX02 boundary, not an optimization that kills or idles canonical baseline actors.
        // All ordinary AI/work stays active. Actual pregnancy, lifespan death, creation and birth execute in WorldPair.
        state.Laws["birth.enabled"] = 1; state.Laws["birth.fertility"] = 100;
        var father = state.People[0]; var mother = state.People[1]; father.Lifespan = state.Month - father.Born + 1;
        Invariants.Validate(state);
        return (state,
        [new(0, new("ex02:actual-pregnancy", mother.Id, "conceive", father.Id)),
            new(1, new("ex02:actual-arrival-after-death", 0, "create", Data: "EX02 새 육신"))]);
    }

    private static void WorldPair(string experiment, GameState initial, ExecutionScript[] script, int population,
        int repetition, ExperimentHostIdentity host, List<ExecutionMeasuredVariant> rows)
    {
        var baseline = MeasureWorld(experiment, initial, script, population, repetition, host, scalar: true);
        var candidate = MeasureWorld(experiment, initial, script, population, repetition, host, scalar: false);
        rows.Add(baseline); rows.Add(candidate);
        if (!baseline.Boundaries.SequenceEqual(candidate.Boundaries) || baseline.OutputDigest != candidate.OutputDigest)
            throw new InvalidDataException(experiment + " 원본/새 실행의 hash·사건열·RNG·생사·슬롯이 다릅니다.");
    }

    private static ExecutionMeasuredVariant MeasureWorld(string experiment, GameState initial, ExecutionScript[] script,
        int population, int repetition, ExperimentHostIdentity host, bool scalar)
    {
        var fixtureHash = GameCodec.Hash(initial); var commands = ExecutionMeasurementHost.Digest(script);
        var warm = GameWorld.Restore(initial); if (scalar) warm.UseScalarExecutionReferenceForVerification();
        var warmHash = warm.StateHash(); Drive(warm, script, null, null); warm = null;
        var world = GameWorld.Restore(initial); if (scalar) world.UseScalarExecutionReferenceForVerification();
        var opening = world.StateHash();
        if (opening != fixtureHash || warmHash != opening) throw new InvalidDataException("예열/측정은 같은 초기 입력의 별도 세계여야 합니다.");
        var series = new ExecutionMeasurementSeries(); var boundaries = new List<ExperimentBoundary>();
        Drive(world, script, series, boundaries);
        var retained = ExecutionMeasurementHost.RetainedManagedBytes(); GC.KeepAlive(world);
        var final = world.Checkpoint(); Invariants.Validate(final);
        if (experiment == "EX-02" && (final.BirthCount != initial.BirthCount + 1 || final.DeathCount != initial.DeathCount + 1 ||
            final.People.Count != initial.People.Count + 2 || !final.People.Any(p => p.ParentOne != 0 && p.ParentTwo == initial.People[0].Id) ||
            final.ActiveSlots[initial.People[0].Slot].Generation <= initial.People[0].SlotGeneration))
            throw new InvalidDataException("EX02는 실제 임신/출생·수명 사망·새 입장/슬롯 세대 변경을 완료해야 합니다.");
        return series.Finish(experiment, scalar ? "original-person-array" : "hot-columns-due-heap", population, repetition, host,
            fixtureHash, commands, "actual-world-step-and-commands-v1", population, "single-writer;integer;current typed adapters",
            warmHash, opening, retained,
            "각 실제 AdvanceTo 한 달 및 공개 명령 Execute 전체. 열 갱신·힙 재조정/정리·fingerprint·배치/스칼라 검증·게시를 포함한다.",
            "초기 fixture/restore, 독립 전체 12개월 예열, 경계 clone/hash/사건·슬롯 digest, 최종 불변식/보고/GC는 raw 표본 밖이다. 보유 메모리는 공통 fixture와 실제 세계 및 작은 결과가 남은 전체 프로세스 관리 힙이며 세계별 귀속값이 아니다.",
            boundaries.ToArray(), world.StateHash());
    }

    private static void Drive(GameWorld world, IReadOnlyList<ExecutionScript> script, ExecutionMeasurementSeries? series,
        List<ExperimentBoundary>? boundaries)
    {
        var start = world.Month; long operation = 0;
        for (var month = start; month <= start + 12; month++)
        {
            if (month > start)
            {
                if (series is null) world.AdvanceTo(month); else series.Measure("advance:" + month, () => world.AdvanceTo(month));
                if (boundaries is not null) boundaries.Add(Boundary(world, operation)); operation++;
            }
            foreach (var step in script.Where(s => s.Month == month))
            {
                var result = series is null ? world.Execute(step.Command) : series.Measure("command:" + step.Command.Kind + ":" + step.Command.Id, () => world.Execute(step.Command));
                if (!result.Accepted) throw new InvalidDataException(step.Command.Id + ": " + result.Message);
                if (boundaries is not null) boundaries.Add(Boundary(world, operation)); operation++;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ExperimentBoundary Boundary(GameWorld world, long operation)
    {
        var state = world.Checkpoint();
        return new(operation, state.Month, GameCodec.Hash(state), state.RandomState, ExecutionMeasurementHost.Digest(state.Events),
            state.BirthCount, state.DeathCount, state.People.Count(p => p.Status == LifeStatus.Alive),
            ExecutionMeasurementHost.Digest(state.ActiveSlots.Select(s => new { s.Person, s.Generation }).ToArray()));
    }

    private static ITalentBatchBackend[] Backends() =>
        [new ScalarTalentBackend(), new VectorTalentBackend(), new FailingBackend(true), new FailingBackend(false)];

    private static void TalentWorld(GameState initial, int population, int repetition, ExperimentHostIdentity host,
        List<ExecutionMeasuredVariant> rows)
    {
        var fixture = GameCodec.Hash(initial); var output = "";
        foreach (var backend in Backends())
        {
            var warm = GameWorld.Restore(initial); warm.UseTalentBackendForVerification(backend);
            var warmHash = warm.StateHash(); _ = AssessAll(warm, initial); _ = AssessAll(warm, initial); warm = null;
            var world = GameWorld.Restore(initial); world.UseTalentBackendForVerification(backend); var opening = world.StateHash();
            if (warmHash != opening || opening != fixture) throw new InvalidDataException("24재능 예열/측정 입력이 다릅니다.");
            var series = new ExecutionMeasurementSeries();
            var cold = series.Measure("cold-source-capture-verify-publish", () => AssessAll(world, initial));
            var hit = series.Measure("warm-derived-cache-and-dynamic-current-expression", () => AssessAll(world, initial));
            var digest = ExecutionMeasurementHost.Digest(cold);
            if (ExecutionMeasurementHost.Digest(hit) != digest || world.StateHash() != opening || output.Length > 0 && digest != output)
                throw new InvalidDataException("실제 재능 전체 계산의 값 또는 조회 순수성이 다릅니다.");
            output = digest;
            var retained = ExecutionMeasurementHost.RetainedManagedBytes(); GC.KeepAlive(world);
            rows.Add(series.Finish("EX-04", backend.Id + ":actual-assess", population, repetition, host, fixture,
                ExecutionMeasurementHost.Digest(new { operation = "PeopleAptitude", population }), "actual-assess-full-boundary-v1", population * 24,
                "Vector.IsHardwareAccelerated=" + Vector.IsHardwareAccelerated + ";Vector<int>.Count=" + Vector<int>.Count,
                warmHash, opening, retained,
                "실제 PeopleAptitude 전체: context·선천/유전/source fingerprint·입력 복사·pool·커널·정본/범위 검증·한 번 게시·동적 학습/건강/특성·출력 배열. cold와 warm raw 표본을 별도 label로 남긴다.",
                "fixture/restore·별도 예열·반환값 digest/동일성 검사·GC·보고는 표본 밖. thread 할당은 background/native를 포함하지 않으며 보유 메모리는 프로세스 범위다.", [], digest));
        }
    }

    private static AptitudeResult[] AssessAll(GameWorld world, GameState input) => input.People.Select(world.PeopleAptitude).ToArray();

    private static void TalentKernel(GameState initial, ExperimentHostIdentity host, List<ExecutionMeasuredVariant> rows, int repetitions)
    {
        var person = initial.People[0]; var innate = initial.PeopleDomain.Profiles.Single(p => p.PersonId == person.Id).Innate;
        foreach (var count in new[] { 0, 1, Vector<int>.Count - 1, Vector<int>.Count, Vector<int>.Count + 1, 24, 2400 }.Distinct())
        {
            var indices = Enumerable.Range(0, count).Select(i => i % 24).ToArray();
            var fixture = ExecutionMeasurementHost.Digest(new { innate, indices });
            int[] Calculate(ITalentBatchBackend backend)
            {
                var fingerprint = ExecutionMeasurementHost.Digest(innate);
                var stamp = new TalentInputStamp(ExecutionIdentity.Of(person), initial.Revision, initial.LawRevision, fingerprint);
                var input = new TalentBatchInput(stamp, innate.Foundations, innate.ElementRoots, indices);
                var pending = InnateTalentBatch.Calculate(input, backend);
                var current = stamp with { SourceFingerprint = ExecutionMeasurementHost.Digest(innate) };
                if (InnateTalentBatch.Commit(pending, current, out var values) != TalentBatchStatus.Applied)
                    throw new InvalidDataException("Kernel 배치를 게시하지 못했습니다.");
                return values;
            }
            var expected = ExecutionMeasurementHost.Digest(Calculate(new ScalarTalentBackend()));
            foreach (var backend in Backends())
            {
                _ = Calculate(backend); var series = new ExecutionMeasurementSeries();
                for (var repetition = 0; repetition < repetitions; repetition++)
                    if (ExecutionMeasurementHost.Digest(series.Measure("capture-calculate-verify-commit", () => Calculate(backend))) != expected)
                        throw new InvalidDataException("직접 kernel의 완전한 정수 출력이 정본과 다릅니다.");
                rows.Add(series.Finish("EX-04", backend.Id + ":direct-kernel", 1, 0, host, fixture,
                    ExecutionMeasurementHost.Digest(indices), "canonical-24talent-request-batch-v1", count,
                    "Vector.IsHardwareAccelerated=" + Vector.IsHardwareAccelerated + ";Vector<int>.Count=" + Vector<int>.Count,
                    fixture, fixture, ExecutionMeasurementHost.RetainedManagedBytes(),
                    "같은 실제 초기 인물의 재능 요청 배치(사람 수가 아님). 입력 fingerprint·복사·임시 배열·변환·정본/범위 검증·현재 입력 대조·전체 결과 게시를 포함한다.",
                    "초기 인물 fixture·요청 index 배열·독립 예열·반환값 digest 비교·GC·보고는 표본 밖이다. 대량 요청을 대량 인구 실행으로 주장하지 않는다.", [], expected));
            }
        }
    }
}
