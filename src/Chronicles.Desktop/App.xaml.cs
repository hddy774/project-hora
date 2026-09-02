using Chronicles.Application;
using Chronicles.Core;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Chronicles.Desktop;

public partial class App : System.Windows.Application
{
    private int _controlledShutdownStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        try
        {
            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            var logPath = TryWriteFailure("게임 시작 실패", exception);
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

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_UnobservedTaskException;
        base.OnExit(e);
    }

    internal void ReportAuthorityFault(Exception exception) =>
        BeginControlledShutdown("시뮬레이션 권위 상태 오류", exception);

    private void App_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        if (FindSaveOperationException(e.Exception) is { IsRecoverable: true } saveError)
        {
            var logPath = TryWriteFailure("복구 가능한 저장 처리 실패", saveError);
            e.Handled = true;
            MessageBox.Show(
                $"저장 작업을 완료하지 못했지만 게임은 계속 실행됩니다.\n\n" +
                $"{saveError.Message}\n\n{FormatLogNotice(logPath)}",
                $"{ProductInfo.KoreanTitle} PC 저장 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        e.Handled = true;
        BeginControlledShutdown("처리되지 않은 UI 오류", e.Exception);
    }

    private void CurrentDomain_UnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _ = TryWriteFailure("처리되지 않은 런타임 오류", exception);
        }
    }

    private void TaskScheduler_UnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        var saveError = FindSaveOperationException(e.Exception);
        _ = TryWriteFailure(
            saveError is { IsRecoverable: true }
                ? "복구 가능한 비동기 저장 처리 실패"
                : "처리되지 않은 비동기 오류",
            (Exception?)saveError ?? e.Exception);
        e.SetObserved();
        if (saveError is { IsRecoverable: true })
        {
            return;
        }

        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            _ = Dispatcher.BeginInvoke(
                new Action(() => BeginControlledShutdown(
                    "처리되지 않은 비동기 오류",
                    e.Exception)));
        }
    }

    private void BeginControlledShutdown(string context, Exception exception)
    {
        if (Interlocked.Exchange(ref _controlledShutdownStarted, 1) != 0)
        {
            return;
        }

        var logPath = TryWriteFailure(context, exception);
        MessageBox.Show(
            $"권위 세계의 안전을 보장할 수 없는 오류가 발생하여 게임을 종료합니다.\n\n" +
            $"{exception.Message}\n\n{FormatLogNotice(logPath)}",
            $"{ProductInfo.KoreanTitle} PC 오류",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(-1);
    }

    private static string? TryWriteFailure(string context, Exception exception)
    {
        try
        {
            var target = new LogOutputTarget(
                LogOutputKind.Debug,
                LogOutputMode.Automatic);
            var directory = LogOutputDirectories.EnsureDirectory(target);
            var lines = new List<string>
            {
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [ERROR] {context} · VERSION {ProductInfo.Version}",
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

    private static SaveOperationException? FindSaveOperationException(Exception exception)
    {
        if (exception is SaveOperationException saveOperationException)
        {
            return saveOperationException;
        }

        if (exception is AggregateException aggregate)
        {
            return aggregate.Flatten().InnerExceptions
                .Select(FindSaveOperationException)
                .FirstOrDefault(candidate => candidate is not null);
        }

        return exception.InnerException is null
            ? null
            : FindSaveOperationException(exception.InnerException);
    }

    private static string FormatLogNotice(string? logPath) => logPath is null
        ? "오류 로그를 기록하지 못했습니다."
        : $"오류 로그: {logPath}";
}
