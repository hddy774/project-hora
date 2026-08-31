using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Simulation;

internal static class AuthorityFoundationTests
{
    public static void Run()
    {
        StableAuthorityRegistryIsClosedAndOrderIndependent();
        FormulaRegistryIsStableAndComplete();
        RuntimeCommandsAreAtomicAndCausal();
        RuntimeRollbackRestoresAllState();
        ApplicationAdapterUsesTheSameContract();
    }

    private static void StableAuthorityRegistryIsClosedAndOrderIndependent()
    {
        var registry = AuthorityRegistry.Current;
        TestAssert.True(registry.Validation.IsValid, "권위 Registry 정적 무결성");
        TestAssert.Equal(14, registry.OwnerBindings.Count, "M01~M14 단일 owner binding");
        TestAssert.Equal(
            registry.OwnerBindings.Count,
            registry.OwnerBindings.Select(binding => binding.DomainId).Distinct().Count(),
            "domain primary owner 중복 0");
        TestAssert.True(
            registry.ChangeSets.Count >= registry.Entries.Count,
            "Registry revision change 계보 보존");

        foreach (var owner in registry.OwnerBindings)
        {
            TestAssert.True(registry.TryGetOwner(owner.DomainId, out var resolved), "owner stable ID 조회");
            TestAssert.Equal(owner, resolved, "owner mapping 결정론");
            TestAssert.True(registry.Resolve(owner.OwnerId).IsUsable, "owner ref 고아 없음");
            TestAssert.True(registry.Resolve(owner.SchemaId).IsUsable, "schema ref 고아 없음");
            TestAssert.True(registry.Resolve(owner.MappingId).IsUsable, "mapping ref 고아 없음");
        }

        var unknown = registry.Resolve(new RegistryStableId(ulong.MaxValue));
        TestAssert.Equal(RegistryResolveStatus.Undefined, unknown.Status, "미해결 ref 기본값 보충 금지");
        TestAssert.True(!unknown.Resolved.IsValid, "미해결 ref는 stable ID 0으로 READY 위장 금지");

        var reordered = new AuthorityRegistrySnapshot(
            registry.RegistryRevision,
            registry.ActiveSchemaId,
            registry.ActiveMappingId,
            registry.Entries.Reverse(),
            registry.OwnerBindings.Reverse(),
            registry.ChangeSets.Reverse());
        TestAssert.True(reordered.Validation.IsValid, "등록 순서 비의존 Registry 검증");
        TestAssert.True(
            registry.Entries.Select(entry => entry.Id)
                .SequenceEqual(reordered.Entries.Select(entry => entry.Id)),
            "표시/등록 순서와 stable 해석 분리");

        var duplicateOwner = registry.OwnerBindings.Concat([registry.OwnerBindings[0]]).ToArray();
        var invalid = AuthorityRegistry.Validate(
            registry.RegistryRevision,
            registry.ActiveSchemaId,
            registry.ActiveMappingId,
            registry.Entries,
            duplicateOwner,
            registry.ChangeSets);
        TestAssert.Equal(
            RegistryValidationError.DuplicateDomainOwner,
            invalid.Error,
            "primary owner 중복 정적 거부");
    }

    private static void FormulaRegistryIsStableAndComplete()
    {
        var definitions = FormulaRegistry.All;
        TestAssert.True(definitions.Count >= 13, "기존 수행 공식과 핵심 도메인 공식 등록");
        TestAssert.Equal(
            definitions.Count,
            definitions.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count(),
            "공식 stable ID 고유성");
        TestAssert.Equal(
            definitions.Count,
            definitions.Select(item => item.RegistryId).Distinct().Count(),
            "공식 numeric Registry ID 고유성");
        TestAssert.Equal(
            CombatPowerFormula.CurrentRevision,
            FormulaRegistry.Get("F-COMBAT-001").Revision,
            "전투력 공식 revision 연결");
        TestAssert.Equal(
            AptitudeRules.FormulaRevision,
            FormulaRegistry.Get(AptitudeRules.SummaryFormulaId).Revision,
            "자질 공식 revision 연결");
        TestAssert.Throws<KeyNotFoundException>(
            () => FormulaRegistry.Get("F-NOT-REGISTERED"),
            "등록되지 않은 공식 ID 거부");
    }

    private static void RuntimeCommandsAreAtomicAndCausal()
    {
        var sessionId = new AuthoritySessionId(0x15UL);
        var port = new FakeRuntimeControlPort();
        var authority = new RuntimeControlAuthoritySession(sessionId, port);
        var threadId = new CausalThreadId(sessionId, 1);

        var first = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 1),
            ExpectedRevision: 0,
            Paused: true,
            Speed: 10,
            threadId));
        TestAssert.True(first.Applied, "첫 typed command 적용");
        TestAssert.Equal(2, first.Effects.Count, "pause+speed effect 두 개");
        TestAssert.Equal(1L, first.RevisionAfter, "첫 authority revision");
        TestAssert.True(port.IsPaused, "pause effect 적용");
        TestAssert.Equal(10, port.Speed, "speed effect 적용");
        var firstEvent = first.EventId ?? throw new InvalidOperationException("첫 event ID가 없습니다.");

        var duplicate = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 1),
            ExpectedRevision: 1,
            Paused: false,
            Speed: null,
            threadId,
            firstEvent));
        TestAssert.Equal(AuthorityCommandStatus.SequenceConflict, duplicate.Status, "command 재생 거부");
        TestAssert.True(port.IsPaused, "재생 거부 시 상태 불변");

        var second = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 2),
            ExpectedRevision: 1,
            Paused: false,
            Speed: 25,
            threadId,
            firstEvent));
        TestAssert.True(second.Applied, "인과 후속 command 적용");
        TestAssert.Equal(2L, second.RevisionAfter, "후속 authority revision");
        var journal = authority.GetJournal();
        TestAssert.Equal(2, journal.Count, "성공 event journal 크기");
        TestAssert.Equal(journal[0].Id, journal[0].RootEventId, "첫 event는 root");
        TestAssert.Equal(journal[0].Id, journal[1].RootEventId, "후속 event root 보존");
        TestAssert.Equal(journal[0].Id, journal[1].ParentEventId, "후속 event parent 보존");

        var beforeInvalid = (port.IsPaused, port.Speed, authority.Revision, journal.Count);
        var invalidParent = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 3),
            ExpectedRevision: 2,
            Paused: true,
            Speed: null,
            threadId,
            new AuthorityEventId(sessionId, 999)));
        TestAssert.Equal(
            AuthorityCommandStatus.InvalidCausalReference,
            invalidParent.Status,
            "존재하지 않는 causal parent 거부");
        TestAssert.Equal(beforeInvalid, (port.IsPaused, port.Speed, authority.Revision, authority.GetJournal().Count),
            "causal 검증 실패 원자 불변");
    }

    private static void RuntimeRollbackRestoresAllState()
    {
        var sessionId = new AuthoritySessionId(0x1500UL);
        var port = new FakeRuntimeControlPort { ThrowOnSpeed = 25 };
        var authority = new RuntimeControlAuthoritySession(sessionId, port);
        var result = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 1),
            ExpectedRevision: 0,
            Paused: true,
            Speed: 25,
            new CausalThreadId(sessionId, 1)));

        TestAssert.Equal(AuthorityCommandStatus.Rejected, result.Status, "apply 실패 typed 결과");
        TestAssert.Equal("APPLY_FAILED_ROLLED_BACK", result.ErrorCode, "rollback 결과 코드");
        TestAssert.True(!port.IsPaused, "rollback pause 복원");
        TestAssert.Equal(1, port.Speed, "rollback speed 복원");
        TestAssert.Equal(0L, authority.Revision, "rollback revision 불변");
        TestAssert.Equal(0, authority.GetJournal().Count, "rollback event 미기록");
    }

    private static void ApplicationAdapterUsesTheSameContract()
    {
        using var application = GameApplicationSession.CreateDefault(0x150015UL);
        var sessionId = new AuthoritySessionId(0x150015UL);
        var authority = new RuntimeControlAuthoritySession(sessionId, application);
        var result = authority.Execute(new RuntimeControlCommand(
            new AuthorityCommandId(sessionId, 1),
            ExpectedRevision: 0,
            Paused: true,
            Speed: 10,
            new CausalThreadId(sessionId, 1)));
        TestAssert.True(result.Applied, "실제 Application adapter command 적용");
        TestAssert.True(application.IsPaused, "실제 Application pause 반영");
        TestAssert.Equal(10, application.Speed, "실제 Application speed 반영");
    }

    private sealed class FakeRuntimeControlPort : IRuntimeControlPort
    {
        public bool IsPaused { get; private set; }
        public int Speed { get; private set; } = 1;
        public int? ThrowOnSpeed { get; init; }

        public void SetPaused(bool paused) => IsPaused = paused;

        public void SetSpeed(int speed)
        {
            if (speed == ThrowOnSpeed)
            {
                throw new InvalidOperationException("의도한 speed apply 실패");
            }
            Speed = speed;
        }
    }
}

internal static class TestAssert
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
        }
    }

    public static void Throws<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"{message}: {typeof(TException).Name}이 발생하지 않았습니다.");
    }
}
