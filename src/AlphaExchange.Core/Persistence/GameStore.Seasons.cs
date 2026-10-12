using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    public SeasonResult? ReadSeason(GameState state, long season)
    {
        if (season < 1 || season >= state.Season) return null;
        var pendingSeason = state.PendingSeasons.FirstOrDefault(s => s.Season == season); if (pendingSeason is not null) return pendingSeason;
        if (File.Exists(SavePath))
        {
            using var connection = Open(SavePath, true); using var command = connection.CreateCommand();
            command.CommandText = "SELECT data FROM seasons WHERE run=$run AND season=$season";
            command.Parameters.AddWithValue("$run", state.RunId); command.Parameters.AddWithValue("$season", season);
            if (command.ExecuteScalar() is byte[] data) return JsonSerializer.Deserialize<SeasonResult>(Decode(data));
        }
        string path = SeasonPath(state, season);
        return File.Exists(path) ? JsonSerializer.Deserialize<SeasonResult>(File.ReadAllText(path)) : null;
    }
    public static bool StorageException(Exception e) => e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException
        or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException or SqliteException;
}
