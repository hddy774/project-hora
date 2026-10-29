// 목판 표제와 세계 상태. 진행·속도는 하단 진행 바로 옮겨 본문 높이를 확보한다.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>고서 표제·연도·시대·전쟁·천기를 표시하는 상단.</summary>
    internal sealed class HeaderBar
    {
        private readonly TextBlock _year = Ui.Text("", 20, Palette.Ink, bold: true);
        private readonly TextBlock _title = Ui.Text("선로만상록 프로젝트", 20, Palette.Ink, bold: true);
        private readonly BadgeView _era = new();
        private readonly BadgeView _war = new();
        private readonly BadgeView _favor = new();
        private readonly Button _recover;
        private readonly Border _root;

        /// <summary>상태와 복구 진입점을 한 번 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal HeaderBar(INavigator navigator)
        {
            _title.VerticalAlignment = VerticalAlignment.Center;
            _year.VerticalAlignment = VerticalAlignment.Center;
            _year.TextWrapping = TextWrapping.NoWrap;
            Grid top = Ui.Columns("*,Auto", _title, _year);
            top.ColumnSpacing = 8;
            WrapPanel status = new();
            _recover = Ui.Button("오류로 정지 · 복구 ›", () => navigator.Open(new SheetLink(LinkKind.Settings, 0)));
            status.Children.AddRange(new Control[] { _era, _war, _favor, _recover });
            Grid layers = new();
            layers.Children.Add(new InkLandscape());
            layers.Children.Add(Ui.Column(6, top, status));
            _root = new Border
            {
                Background = Palette.Brush(Palette.HeaderBack),
                BorderBrush = Palette.Brush(Palette.Ink),
                BorderThickness = new Thickness(0, 2, 0, 1),
                Padding = new Thickness(12, 8),
                Child = layers,
            };
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _root;

        /// <summary>복잡한 상세 대신 현재 세계의 핵심 상태만 알린다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="runner">실행기.</param>
        internal void Refresh(World world, SimulationRunner runner)
        {
            _title.Text = Typography.Scale >= 1.5 ? "선로만상록\n프로젝트" : "선로만상록 프로젝트";
            _year.Text = Fmt.Years(world.Year);
            _era.Set(new BadgeSpec(Labels.Of(world.Era.Kind), Palette.Muted));
            _war.Set(new BadgeSpec(world.War.AtWar ? "전쟁 중" : "휴전",
                world.War.AtWar ? Palette.Demonic : Palette.Orthodox));
            _favor.Set(new BadgeSpec("천기 " + world.HeavenlyFavor + "/" + Balance.MaxHeavenlyFavor, Palette.Favor));
            _recover.IsVisible = runner.IsFaulted;
        }
    }
}
