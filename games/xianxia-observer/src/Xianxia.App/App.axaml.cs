// 선협 관찰 Avalonia 애플리케이션 진입점. 게임 세션을 하나 만들고 플랫폼별 수명 주기에 맞춰 화면과 저장을 연결한다.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Xianxia.App.Services;
using Xianxia.App.Views;

namespace Xianxia.App
{
    /// <summary>선협 관찰 애플리케이션. 데스크톱·Android·헤드리스 테스트가 공통으로 사용한다.</summary>
    public partial class App : Application
    {
        private GameSession? _session;

        /// <inheritdoc />
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <inheritdoc />
        public override void OnFrameworkInitializationCompleted()
        {
            // 헤드리스 시험에는 수명 주기가 없다. 시험은 자기 세션과 화면을 직접 만든다.
            if (ApplicationLifetime is null)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            GameSession session = _session ??= GameSession.LoadOrCreate(SaveStore.CreateDefault());
            session.Runner.Start();
            session.Runner.SetRunning(true);
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Title = "선협 관찰",
                    Content = new MainView(session),
                    Width = 412,
                    Height = 860,
                };
                desktop.ShutdownRequested += (_, _) => session.Shutdown();
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime activity)
            {
                activity.MainViewFactory = () => new MainView(session);
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainView(session);
            }

            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
            {
                // Android: 백그라운드로 가면 멈추고 저장, 돌아오면 이어서 진행한다.
                activatable.Deactivated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Background)
                    {
                        session.OnBackground();
                    }
                };
                activatable.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Background)
                    {
                        session.OnForeground();
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
