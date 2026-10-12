using System.Text;
using System.Text.Json;

namespace AlphaExchange.Core;

public sealed record ArchivedSnapshot(long Season, string Path, string Json);
public sealed record HistoryRecord(long Hour, int Resolution, string Json);
public sealed record EventRecord(long Id, long Hour, string Json);
public sealed record SaveSnapshot
{
    Lazy<string> json = null!;
    Lazy<byte[]> encoded = null!;
    public string RunId { get; init; }
    public long Hour { get; init; }
    public long Minute { get; init; }
    public string Json
    {
        get => json.Value;
        init
        {
            json = new Lazy<string>(() => value);
            encoded = new Lazy<byte[]>(() => Encoding.UTF8.GetBytes(value));
        }
    }
    // The writer hashes, validates and compresses UTF-8 directly. Materialize the
    // large UTF-16 string only for callers that explicitly request Json.
    internal byte[] EncodedBytes => encoded.Value;
    public ArchivedSnapshot[] Seasons { get; init; }
    public HistoryRecord[] History { get; init; }
    public EventRecord[] Events { get; init; } = [];
    public EventRecord[] Votes { get; init; } = [];
    public EventRecord[] Bankruptcies { get; init; } = [];
    public EventRecord[] Activities { get; init; } = [];
    public EventRecord[] WorldVotes { get; init; } = [];
    internal long ReportThrough { get; init; }
    public CompanyReport[] Reports { get; init; } = [];
    public SaveSnapshot(string RunId, long Hour, string Json, ArchivedSnapshot[] Seasons, HistoryRecord[] History)
    { this.RunId = RunId; this.Hour = Hour; this.Minute = Hour * 60; this.Json = Json; this.Seasons = Seasons; this.History = History; }
    internal SaveSnapshot(GameState state, ArchivedSnapshot[] seasons, HistoryRecord[] history)
    {
        RunId = state.RunId; Hour = state.CompletedHours; Minute = state.CompletedMinutes; Seasons = seasons; History = history;
        var bytes = new Lazy<byte[]>(() => JsonSerializer.SerializeToUtf8Bytes(state, GameEngine.StateJson));
        encoded = bytes;
        json = new Lazy<string>(() => Encoding.UTF8.GetString(bytes.Value));
    }
}
