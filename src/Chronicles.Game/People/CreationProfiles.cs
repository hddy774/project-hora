using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chronicles.Game.People;

public sealed record PeopleCreationProfile(string Name, string Species, bool Gestates, long MaturityMonths, long LifespanMonths);

public static class PeopleCreationParser
{
    private sealed record Request(string Name = "", string Species = "human", bool? Gestates = null);
    private static readonly JsonSerializerOptions Options = new()
    { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    public static bool TryParse(GameCommand command, IReadOnlyDictionary<string, long> laws, out PeopleCreationProfile profile, out string? error)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(laws);
        profile = null!; error = "이름과 공개 종족 정의·생식 프로필을 확인하세요.";
        Request request;
        if (command.Data.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            try { request = JsonSerializer.Deserialize<Request>(command.Data, Options) ?? new(); }
            catch (JsonException) { return false; }
        }
        else request = new(command.Data, "human");
        var name = request.Name?.Trim();
        var species = SpeciesCatalog.All.FirstOrDefault(s => string.Equals(s.Id, request.Species, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40 || species is null) return false;
        var maturity = species.Id == "human" ? laws["birth.maturity"] : species.MaturityMonths;
        var lifespan = species.Id == "human" ? laws["life.mortal"] : species.BasicLifespanMonths;
        if (maturity < 0 || lifespan <= maturity) return false;
        profile = new(name, species.Id, request.Gestates ?? command.Value != 0, maturity, lifespan);
        error = null; return true;
    }
}
