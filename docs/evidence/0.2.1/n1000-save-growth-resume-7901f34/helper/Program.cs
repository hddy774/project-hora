using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game;

internal static class ResumeProbe
{
    private const long InitialMonth = 30;
    private const long TargetMonth = 120;
    private const string SlotName = "population-1000.seonro";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly InputSpec[] Inputs =
    [
        new(SlotName, 94252646, "13963d180c546c934e32e22b46fc11a7578416c60752e709a9a898c41d0a63f4"),
        new(SlotName + ".backup", 23035565, "ff49e518e279b5febaaa24595650ed827c4738df08d9c621f54c0336869995c8")
    ];

    private static int Main(string[] args)
    {
        string? resultPath = null;
        Report? report = null;
        try
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("원래 저장 폴더와 존재하지 않는 새 출력 폴더를 지정하세요.");
            }

            var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[0]));
            var output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
            if (source == output || output.StartsWith(source + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                source.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Directory.Exists(output))
            {
                throw new IOException("원본과 분리된 존재하지 않는 새 출력 폴더만 허용합니다.");
            }

            foreach (var spec in Inputs)
            {
                var path = Path.Combine(source, spec.Name);
                Require(new FileInfo(path).Length == spec.Bytes && HashFile(path) == spec.Sha256,
                    "원본 slot 크기 또는 복구 관측 SHA가 다릅니다: " + spec.Name);
            }
            Require(!File.Exists(Path.Combine(source, SlotName + ".pending")), "미검토 pending 원본이 있습니다.");
            Directory.CreateDirectory(output);
            report = new Report();
            resultPath = Path.Combine(output, "resume-result.json");
            foreach (var spec in Inputs)
            {
                var origin = Path.Combine(source, spec.Name);
                var copy = Path.Combine(output, spec.Name);
                File.Copy(origin, copy, overwrite: false);
                Require(new FileInfo(copy).Length == spec.Bytes && HashFile(copy) == spec.Sha256,
                    "새 slot 사본의 바이트가 다릅니다.");
                report.Inputs.Add(new(spec.Name, spec.Bytes, spec.Sha256));
            }
            SaveReport(resultPath, report);

            var slot = Path.Combine(output, SlotName);
            var world = GameSaveStore.Load(slot);
            Require(world.Month == InitialMonth, "복원본이 정확히30개월이 아닙니다. 조용히 backup 시점에서 재개하지 않습니다.");
            var opening = world.Checkpoint();
            Invariants.Validate(opening);
            report.OpeningHash = world.StateHash();
            report.OpeningRandomState = opening.RandomState;
            report.OpeningNextId = opening.NextId;
            report.CurrentMonth = world.Month;
            report.Status = "resuming";
            SaveReport(resultPath, report);

            // 이미 기록된30월 observe/저장을 반복하지 않는다. 실제31..120월만 진행한다.
            for (long month = InitialMonth + 1; month <= TargetMonth; month++)
            {
                world.AdvanceTo(month);
                if (world.Month % 6 == 0)
                {
                    Invariants.Validate(world.Checkpoint());
                    report.ValidatedMonths.Add(world.Month);
                }
                if (world.Month is 60 or 90 or 120)
                {
                    world = Checkpoint(world, slot, report);
                }
                report.CurrentMonth = world.Month;
                SaveReport(resultPath, report);
                Console.WriteLine($"RESUME MONTH {world.Month}");
            }

            report.OriginalsUnchanged = Inputs.All(spec =>
                new FileInfo(Path.Combine(source, spec.Name)).Length == spec.Bytes &&
                HashFile(Path.Combine(source, spec.Name)) == spec.Sha256);
            Require(report.OriginalsUnchanged, "진단 도중 원본 slot이 달라졌습니다.");
            Require(world.Month == TargetMonth && report.Checkpoints.Count == 3, "진단의 논리 완료 경계가 다릅니다.");
            report.Status = "diagnostic_completed";
            report.DiagnosticCompleted = true;
            SaveReport(resultPath, report);
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or
            ArgumentException or OverflowException)
        {
            if (report is not null && resultPath is not null)
            {
                report.Status = "diagnostic_failed";
                report.Failure = error.ToString();
                SaveReport(resultPath, report);
            }
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static GameWorld Checkpoint(GameWorld world, string slot, Report report)
    {
        // 원래 순서: INV → View 무영향 → observe → hash → 원자저장/Load → hash → INV.
        var state = world.Checkpoint();
        Invariants.Validate(state);
        var beforeQuery = world.StateHash();
        world.View();
        Require(world.StateHash() == beforeQuery, "원래 View 무영향 검사가 실패했습니다.");
        var commandId = $"stability:1000:{world.Month}";
        var command = world.Execute(new GameCommand(commandId, 0, "observe"));
        Require(command.Accepted, command.Message);
        var savedHash = world.StateHash();
        GameSaveStore.SaveAtomic(world, slot);
        world = GameSaveStore.Load(slot);
        Require(world.StateHash() == savedHash, "실제 파일 저장/재개 Hash가 다릅니다.");
        var loaded = world.Checkpoint();
        Invariants.Validate(loaded);
        report.Checkpoints.Add(new
        {
            month = world.Month,
            primaryBytes = new FileInfo(slot).Length,
            backupBytes = new FileInfo(slot + ".backup").Length,
            commandId,
            queryUnchanged = true,
            publicCommandAccepted = true,
            saveResumeEqual = true,
            invariantValid = true,
            hash = savedHash,
            randomState = loaded.RandomState,
            nextId = loaded.NextId,
            revision = loaded.Revision,
            people = loaded.People.Count,
            living = loaded.People.Count(p => p.Status == LifeStatus.Alive),
            births = loaded.BirthCount,
            deaths = loaded.DeathCount,
            completedKnowledgeActivities = loaded.Knowledge.Activities.Count(a =>
                a.Status == Chronicles.Game.Knowledge.KnowledgeActivityStatus.Completed),
            completedPhysicalJobs = loaded.Inventory.Jobs.Count(j => j.Status == "completed"),
            actualExtraction = loaded.Materials.Values.Sum(m => m.Extraction),
            actualProduction = loaded.Materials.Values.Sum(m => m.Production)
        });
        Console.WriteLine($"RESUME CHECKPOINT {world.Month} primaryBytes={new FileInfo(slot).Length}");
        return world;
    }

    private static string HashFile(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    private static void SaveReport(string path, Report report)
    {
        var pending = path + ".pending";
        File.WriteAllText(pending, JsonSerializer.Serialize(report, JsonOptions));
        File.Move(pending, path, overwrite: true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    private sealed record InputSpec(string Name, long Bytes, string Sha256);
    private sealed record InputReceipt(string Name, long Bytes, string Sha256);

    private sealed class Report
    {
        public string Schema { get; } = "seonro-save-growth-resume-diagnostic.v1";
        public string ProductSource { get; } = "7901f344462ebc1f98d7105efd79ab0aa058b8fe";
        public string Scope { get; } = "새 프로세스의30월 저장 재개·31..120 파일 증가/정수 논리 진단. 정식 시간/관리힙/전체120개월/T04 합격 근거가 아니다.";
        public bool FormalT04Eligible { get; } = false;
        public int InitialPopulationLabel { get; } = 1000;
        public long ResumedAtMonth { get; } = InitialMonth;
        public long TargetMonth { get; } = ResumeProbe.TargetMonth;
        public string Status { get; set; } = "preparing";
        public bool DiagnosticCompleted { get; set; }
        public long CurrentMonth { get; set; }
        public string? OpeningHash { get; set; }
        public ulong OpeningRandomState { get; set; }
        public long OpeningNextId { get; set; }
        public bool OriginalsUnchanged { get; set; }
        public string? Failure { get; set; }
        public List<InputReceipt> Inputs { get; } = [];
        public List<long> ValidatedMonths { get; } = [];
        public List<object> Checkpoints { get; } = [];
    }
}
