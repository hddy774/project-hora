using Chronicles.Application;
using Chronicles.Core;
using System.IO;
using System.Windows;

namespace Chronicles.Desktop;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
            var window = new EcologyWindow(e.Args.Contains("--smoke-test", StringComparer.Ordinal));
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            var logPath = TryWriteStartupFailure(exception);
            var logNotice = logPath is null
                ? "시작 오류 로그를 기록하지 못했습니다."
                : $"오류 로그: {logPath}";
            MessageBox.Show(
                $"게임을 시작하지 못했습니다.\n\n{exception.Message}\n\n{logNotice}",
                $"{ProductInfo.KoreanTitle} PC 시작 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static string? TryWriteStartupFailure(Exception exception)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SeonroMansangrok", "logs", "debug", "automatic");
            var lines = new List<string>
            {
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [ERROR] 게임 시작 실패 · VERSION {ProductInfo.Version}",
                $"예외 형식: {exception.GetType().FullName}",
                $"메시지: {exception.Message}",
                "[예외 상세]",
            };
            using var reader = new StringReader(exception.ToString());
            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            var result = BoundedLogFileWriter.AppendSeries(
                directory,
                $"debug-auto-{DateTime.Now:yyyyMMdd}",
                lines);
            return result.LastPath;
        }
        catch (Exception logException) when (
            logException is IOException
                or UnauthorizedAccessException
                or ArgumentException)
        {
            return null;
        }
    }
}
