// 상단 막대(원본 헤더): 제목·연도, 시대·전쟁 상태·천기, 진행/정지·속도·설정·도감 단추.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Views
{
    /// <summary>상단 막대.</summary>
    internal sealed class HeaderBar
    {
        private static readonly Color[] s_eraColors =
        {
            Palette.Muted, Palette.Peace, Palette.Rogue, Palette.Demonic, Palette.Danger,
        };

        private readonly INavigator _navigator;
        private readonly TextBlock _subtitle = Ui.Muted(string.Empty, 10);
        private readonly TextBlock _year = Ui.Text(string.Empty, 17, Palette.Accent, bold: true);
        private readonly TextBlock _pace = Ui.Muted(string.Empty, 9.5);
        private readonly BadgeView _era = new();
        private readonly BadgeView _war = new();
        private readonly BadgeView _favor = new();
        private readonly BadgeView _error = new();
        private readonly Button _play;
        private readonly Button[] _speeds;
        private readonly Border _root;

        /// <summary>상단 막대를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal HeaderBar(INavigator navigator)
        {
            _navigator = navigator;
            Border seal = new()
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = Palette.Brush(Palette.Accent),
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = "仙",
                    FontSize = 21,
                    FontWeight = FontWeight.Bold,
                    Foreground = Palette.Brush(Palette.OnAccent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            TextBlock title = Ui.Text("선로만상록 프로젝트", 15, Palette.Accent, bold: true);
            StackPanel titles = Ui.Column(0, title, _subtitle);
            titles.VerticalAlignment = VerticalAlignment.Center;
            _year.HorizontalAlignment = HorizontalAlignment.Right;
            _year.TextWrapping = TextWrapping.NoWrap;
            _pace.HorizontalAlignment = HorizontalAlignment.Right;
            StackPanel yearBox = Ui.Column(0, _year, _pace);
            yearBox.VerticalAlignment = VerticalAlignment.Center;
            Grid top = Ui.Columns("Auto,*,Auto", seal, titles, yearBox);

            WrapPanel status = new();
            status.Children.AddRange(new Control[] { _era, _war, _favor, _error });

            _play = Ui.Button("▶ 진행", TogglePlay, "primary");
            _play.MinWidth = 70;
            UniformGrid speedGrid = new() { Rows = 1, Margin = new Thickness(6, 0) };
            ReadOnlySpan<int> speeds = SimulationRunner.Speeds;
            _speeds = new Button[speeds.Length];
            for (int index = 0; index < speeds.Length; index++)
            {
                int position = index;
                string label = speeds[index] == SimulationRunner.MaxSpeed ? "최대"
                    : speeds[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
                Button button = Ui.Button(label, () => ChangeSpeed(position), "small");
                button.Margin = new Thickness(1.5, 0);
                button.Padding = new Thickness(0, 5);
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                _speeds[index] = button;
                speedGrid.Children.Add(button);
            }

            Button settings = Ui.Button("설정", () => _navigator.Open(new SheetLink(LinkKind.Settings, 0)), "small");
            Button help = Ui.Button("도감", () => _navigator.Open(new SheetLink(LinkKind.Help, 0)), "small");
            StackPanel extra = Ui.Row(4, help, settings);
            Grid controls = Ui.Columns("Auto,*,Auto", _play, speedGrid, extra);

            _root = new Border
            {
                Background = Palette.Brush(Palette.HeaderBack),
                BorderBrush = Palette.Brush(Palette.Line),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8, 12, 8),
                Child = Ui.Column(7, top, status, controls),
            };
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _root;

        /// <summary>안전 영역(상태 표시줄) 여백을 반영한다.</summary>
        /// <param name="top">위 여백.</param>
        internal void SetTopInset(double top)
        {
            _root.Padding = new Thickness(12, 8 + top, 12, 8);
        }

        /// <summary>세계 상태를 반영한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="runner">실행기.</param>
        internal void Refresh(World world, SimulationRunner runner)
        {
            _subtitle.Text = world.Settings.Name + " · 시드 " + world.Settings.Seed + " · "
                + Fmt.Number(world.Cultivators.Count) + "명";
            _year.Text = Fmt.Years(world.Year);
            bool running = runner.IsRunning;
            _pace.Text = running ? "실제 " + Fmt.Number(runner.MeasuredYearsPerSecond) + "년/초" : "정지됨";
            _era.Set(new BadgeSpec(Labels.Of(world.Era.Kind) + " · " + Fmt.Years(Math.Max(0, world.Era.EndYear
                - world.Year)) + " 남음", s_eraColors[(int)world.Era.Kind]));
            int change = WarSystem.YearsUntilChange(world.Year);
            _war.Set(world.War.AtWar
                ? new BadgeSpec("전쟁 중 · 종전까지 " + Fmt.Years(change), Palette.War)
                : new BadgeSpec("휴전 · 전쟁까지 " + Fmt.Years(change), Palette.Peace));
            _favor.Set(new BadgeSpec("天機 " + world.HeavenlyFavor + "/" + Balance.MaxHeavenlyFavor, Palette.Favor));
            string? error = runner.Error;
            _error.IsVisible = error is not null;
            if (error is not null)
            {
                _error.Set(new BadgeSpec("오류로 정지: " + error, Palette.Danger));
            }

            _play.Content = running ? "■ 정지" : "▶ 진행";
            _play.IsEnabled = !runner.IsFaulted;
            _play.Classes.Set("primary", !running);
            int speed = runner.SpeedIndex;
            for (int index = 0; index < _speeds.Length; index++)
            {
                _speeds[index].Classes.Set("active", index == speed);
            }
        }

        private void TogglePlay()
        {
            SimulationRunner runner = _navigator.Session.Runner;
            runner.SetRunning(!runner.IsRunning);
            _navigator.Invalidate();
        }

        private void ChangeSpeed(int index)
        {
            _navigator.Session.Runner.SetSpeed(index);
            _navigator.Invalidate();
        }
    }
}
