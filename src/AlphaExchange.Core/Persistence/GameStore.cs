
namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    readonly string directory;
    readonly object writeGate = new();
    readonly Dictionary<string, List<HistoryRecord>> pending = [];
    readonly Dictionary<string, List<EventRecord>> pendingEvents = [];
    readonly Dictionary<string, List<EventRecord>> pendingVotes = [];
    readonly Dictionary<string, List<EventRecord>> pendingBankruptcies = [];
    readonly Dictionary<string, List<(long Sequence, CompanyReport Report)>> pendingReports = [];
    long reportSequence;
    readonly Dictionary<string, long> queuedDailyThrough = [];
    volatile bool databaseReady;
    long lastBackup;
    string databaseName = "history-v5.sqlite";
    string activeRun = "";
    public string SavePath => Path.Combine(directory, databaseName);
    public string BackupPath => SavePath + ".bak";
    public long LastCommittedHour { get; private set; }
    public int PendingCount { get { lock (pending) return pending.Values.Sum(p => p.Count); } }
    public GameStore(string directory) { this.directory = directory; Directory.CreateDirectory(directory); }
    public string SeasonPath(GameState state, long season) => Path.Combine(directory, "seasons", state.RunId, $"season-{season:D8}.json");
}
