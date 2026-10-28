// 선협 관찰 Avalonia 애플리케이션 진입점. 플랫폼별 수명 주기에 맞춰 첫 화면을 연결한다.
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Xianxia.App.Views;

namespace Xianxia.App
{
    /// <summary>선협 관찰 애플리케이션. 데스크톱·Android·헤드리스 테스트가 공통으로 사용한다.</summary>
    public partial class App : Application
    {
        /// <inheritdoc />
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        /// <inheritdoc />
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new Avalonia.Controls.Window
                {
                    Content = new MainView(),
                    Width = 412,
                    Height = 860,
                };
            }
            else if (ApplicationLifetime is IActivityApplicationLifetime activity)
            {
                activity.MainViewFactory = () => new MainView();
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                singleView.MainView = new MainView();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
