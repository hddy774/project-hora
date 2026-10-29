// 선로만상록 프로젝트 Avalonia 애플리케이션 진입점. 게임 세션을 하나 만들고 플랫폼별 수명 주기에 맞춰 화면과 저장을 연결한다.
using System;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.App.Views;

namespace Xianxia.App
{
    /// <summary>선로만상록 프로젝트 애플리케이션. 데스크톱·Android·헤드리스 테스트가 공통으로 사용한다.</summary>
    public partial class App : Application
    {
        private GameSession? _session;

        /// <summary>Android가 화면 전환 수용 로그를 플랫폼 출력에 연결한다.</summary>
        /// <param name="sink">게임 ID·조작만 기록하는 출력.</param>
        public static void SetUiTraceSink(Action<string> sink) => UiTrace.Sink = sink;

        /// <summary>Android 입력 좌표와 현재 렌더 트리의 히트 검사를 연결한다.</summary>
        /// <param name="content">Activity의 현재 화면.</param>
        /// <param name="x">입력의 물리 가로 좌표.</param>
        /// <param name="y">입력의 물리 세로 좌표.</param>
        public static void TraceNativeHit(object? content, double x, double y)
        {
            if (content is Control control && TopLevel.GetTopLevel(control) is { } top)
            {
                Point point = new(x / top.RenderScaling, y / top.RenderScaling);
                UiTrace.Sink?.Invoke("[xo-input] hit " + JsonSerializer.Serialize(new
                {
                    point.X,
                    point.Y,
                    scale = top.RenderScaling,
                    width = control.Bounds.Width,
                    height = control.Bounds.Height,
                    enabled = control.IsEffectivelyEnabled,
                    rootVisible = top.IsVisible,
                    rootHitVisible = top.IsHitTestVisible,
                    rootEnabled = top.IsEffectivelyEnabled,
                    hit = top.InputHitTest(point)?.GetType().Name,
                    viewHit = control.InputHitTest(top.TranslatePoint(point, control) ?? point)?.GetType().Name,
                }));
            }
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            Typography.Initialize();
        }

        /// <summary>Android 시스템 글자 설정을 열린 화면과 새 화면에 반영한다.</summary>
        /// <param name="scale">기기 글자 배율.</param>
        public static void SetDeviceFontScale(double scale) => Typography.SetScale(scale);

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
            session.Runner.SetRunning(false);
            session.Runner.Start();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Window
                {
                    Title = "선로만상록 프로젝트",
                    Content = new MainView(session, startWhenReady: true),
                    Width = 412,
                    Height = 860,
                };
                desktop.ShutdownRequested += (_, _) => session.Shutdown();
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime activity)
            {
                activity.MainViewFactory = () => new MainView(session, startWhenReady: true);
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainView(session, startWhenReady: true);
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
