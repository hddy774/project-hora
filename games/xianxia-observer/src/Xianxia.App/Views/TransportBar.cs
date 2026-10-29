// 진행 조작은 본문과 분리해 항상 같은 위치에 둔다. 여섯 속도는 별도 선택 화면에서 고른다.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;

namespace Xianxia.App.Views
{
    /// <summary>상시 접근하는 진행·정지 및 현재 속도.</summary>
    internal sealed class TransportBar
    {
        private readonly INavigator _navigator;
        private readonly Button _play;
        private readonly Button _speed;

        /// <summary>터치 영역을 넓힌 두 조작을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal TransportBar(INavigator navigator)
        {
            _navigator = navigator;
            _play = Ui.Button("▶ 진행", Toggle, "primary");
            _speed = Ui.Button("20년/초 ▾", () => navigator.Open(new SheetLink(LinkKind.Speed, 0)));
            AutomationProperties.SetAutomationId(_play, "play-toggle");
            AutomationProperties.SetAutomationId(_speed, "speed-picker");
            Grid grid = Ui.Columns("*,*", _play, _speed);
            grid.ColumnSpacing = 8;
            View = new Border
            {
                Background = Palette.Brush(Palette.PanelBack),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(12, 4),
                Child = grid,
            };
        }

        /// <summary>화면 요소.</summary>
        internal Control View { get; }

        /// <summary>실행기의 실제 상태를 조작 문구·활성 여부와 일치시킨다.</summary>
        /// <param name="runner">실행기.</param>
        internal void Refresh(SimulationRunner runner)
        {
            _play.Content = runner.IsRunning ? "■ 정지" : "▶ 진행";
            _play.IsEnabled = !runner.IsFaulted;
            _speed.Content = SpeedText(runner.SpeedIndex) + " ▾";
        }

        /// <summary>사용자가 고르는 속도 표기.</summary>
        /// <param name="index">실행기 속도 위치.</param>
        /// <returns>년/초 또는 최대.</returns>
        internal static string SpeedText(int index)
        {
            int speed = SimulationRunner.Speeds[index];
            return speed == SimulationRunner.MaxSpeed ? "최대" : speed + "년/초";
        }

        private void Toggle()
        {
            _navigator.Session.Runner.SetRunning(!_navigator.Session.Runner.IsRunning);
            _navigator.Invalidate();
        }
    }
}
