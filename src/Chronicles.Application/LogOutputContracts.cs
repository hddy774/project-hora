using Chronicles.Core;

namespace Chronicles.Application;

public enum LogOutputKind : byte
{
    World,
    Debug,
    Performance,
    Personal,
}

public enum LogOutputMode : byte
{
    Automatic,
    Manual,
}

public readonly record struct LogOutputTarget(
    LogOutputKind Kind,
    LogOutputMode Mode,
    AgentId? AgentId = null)
{
    public void Validate()
    {
        if (Kind == LogOutputKind.Personal && AgentId is null)
        {
            throw new ArgumentException("개인 로그 출력에는 인물 ID가 필요합니다.", nameof(AgentId));
        }

        if (Kind != LogOutputKind.Personal && AgentId is not null)
        {
            throw new ArgumentException("개인 로그가 아닌 출력에는 인물 ID를 지정할 수 없습니다.", nameof(AgentId));
        }
    }
}

/// <summary>
/// Owns the local output tree contract. Directories are created lazily so enabling
/// observation never creates folders for every simulated person.
/// </summary>
public static class LogOutputDirectories
{
    public static string RootDirectory => Path.Combine(
        GameApplicationSession.DefaultDataDirectory,
        "logs");

    public static string GetDirectory(LogOutputTarget target)
    {
        target.Validate();
        var mode = target.Mode switch
        {
            LogOutputMode.Automatic => "automatic",
            LogOutputMode.Manual => "manual",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
        var kind = target.Kind switch
        {
            LogOutputKind.World => "world",
            LogOutputKind.Debug => "debug",
            LogOutputKind.Performance => "performance",
            LogOutputKind.Personal => "personal",
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        return target.Kind == LogOutputKind.Personal
            ? Path.Combine(RootDirectory, kind, target.AgentId!.Value.ToString(), mode)
            : Path.Combine(RootDirectory, kind, mode);
    }

    public static string EnsureDirectory(LogOutputTarget target)
    {
        var directory = GetDirectory(target);
        Directory.CreateDirectory(directory);
        return directory;
    }
}
