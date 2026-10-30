namespace Chronicles.Game.Tests;

public interface IFeatureScenario
{
    string Name { get; }
    void Run();
}

internal static class FeatureScenarioCatalog
{
    public static IEnumerable<IFeatureScenario> Discover()
    {
        foreach (var type in typeof(IFeatureScenario).Assembly.GetTypes().Where(t =>
            t.IsClass && !t.IsAbstract && typeof(IFeatureScenario).IsAssignableFrom(t)).OrderBy(t => t.FullName, StringComparer.Ordinal))
            yield return (IFeatureScenario)Activator.CreateInstance(type)!;
    }
}
