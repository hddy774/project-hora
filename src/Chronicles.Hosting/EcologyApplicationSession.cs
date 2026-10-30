using Chronicles.Game;

namespace Chronicles.Application;

/// <summary>Desktop boundary: no mutable game state or authority type escapes this facade.</summary>
public sealed class EcologyApplicationSession
{
    public static string Version => Chronicles.Core.ProductInfo.Version;
    private readonly GameSession session = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeonroMansangrok", "worlds"));
    public string HandleJson(string json) => session.HandleJson(json);
    public void Suspend() => session.Suspend();
}
