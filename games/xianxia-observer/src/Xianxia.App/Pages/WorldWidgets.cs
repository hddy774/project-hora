// 천하 탭의 부품: 종문 대결 카드(원본 VS 카드)와 접경지 세력도 줄(원본 세력 막대).
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Pages
{
    /// <summary>종문 대결 카드. 누르면 종문 시트를 연다.</summary>
    internal sealed class SectCard
    {
        private readonly int _sectId;
        private readonly TextBlock _name;
        private readonly TextBlock _ideology = Ui.Muted(string.Empty, 9.5);
        private readonly BadgeStrip _badges = new();
        private readonly TextBlock _lines = Ui.Text(string.Empty, 11);

        /// <summary>카드를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="sectId">종문 번호.</param>
        internal SectCard(INavigator navigator, int sectId)
        {
            _sectId = sectId;
            Color color = Palette.Sect(sectId);
            _name = Ui.Text(string.Empty, 14, color, bold: true);
            _name.HorizontalAlignment = HorizontalAlignment.Center;
            _ideology.HorizontalAlignment = HorizontalAlignment.Center;
            _ideology.TextAlignment = TextAlignment.Center;
            _badges.View.HorizontalAlignment = HorizontalAlignment.Center;
            _lines.TextAlignment = TextAlignment.Center;
            _lines.HorizontalAlignment = HorizontalAlignment.Center;
            View = Ui.Button(Ui.Column(3, _name, _ideology, _badges.View, _lines),
                () => navigator.Open(SheetLink.ToSect(sectId)), "card");
            View.Background = Palette.Tint(color, 0x14);
            View.BorderBrush = Palette.Tint(color, 0x50);
        }

        /// <summary>화면 요소.</summary>
        internal Button View { get; }

        /// <summary>내용을 고친다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="summary">종문 요약.</param>
        internal void Refresh(World world, SectSummary summary)
        {
            Sect? sect = world.FindSect(_sectId);
            if (sect is null)
            {
                return;
            }

            Color color = Palette.Sect(_sectId);
            _name.Text = sect.Name + " ›";
            _ideology.Text = sect.IdeologyName + " · " + sect.IdeologyDescription;
            _badges.Set(new[]
            {
                new BadgeSpec("점령 " + summary.Borders + "곳", color),
                new BadgeSpec(Fmt.Number(summary.Members) + "명", Palette.Peace),
            });
            Cultivator? leader = world.Find(sect.LeaderId);
            _lines.Text = "종주 " + (leader?.Name ?? "공석") + "\n전투력 " + Fmt.Short(summary.Power) + " · 금고 "
                + Fmt.Short(sect.Treasury) + "\n전쟁 " + sect.WarsWon + "승 " + sect.WarsLost + "패 · 대비무 "
                + sect.TournamentWins + "회 우승";
        }
    }

    /// <summary>접경지 세력도: 지역마다 지배 종문과 줄다리기 막대.</summary>
    internal sealed class BorderRows
    {
        private readonly StackPanel _panel = new() { Spacing = 6 };
        private readonly List<(TextBlock Owner, TextBlock Battles, TugBar Bar)> _rows = new();

        /// <summary>세력도를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal BorderRows(INavigator navigator)
        {
            foreach (int regionId in RegionCatalog.BorderIds)
            {
                RegionDefinition definition = RegionCatalog.All[regionId];
                TextBlock name = Ui.Text(definition.Name, 11.5, bold: true);
                TextBlock owner = Ui.Text(string.Empty, 11, bold: true);
                owner.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock battles = Ui.Muted(string.Empty, 10.5);
                battles.HorizontalAlignment = HorizontalAlignment.Right;
                TugBar bar = new() { Margin = new Thickness(0, 3, 0, 0) };
                StackPanel content = Ui.Column(1, Ui.Columns("*,*,*", name, owner, battles), bar);
                int id = regionId;
                Button row = Ui.Button(content, () => navigator.Open(SheetLink.ToRegion(id)), "ghost");
                row.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                row.Padding = new Thickness(2, 3);
                _rows.Add((owner, battles, bar));
                _panel.Children.Add(row);
            }

            TextBlock legend = Ui.Muted("왼쪽(파랑): 청운종 우세 · 오른쪽(빨강): 천마문 우세 · 눈금: 점령 문턱", 9.5);
            _panel.Children.Add(legend);
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _panel;

        /// <summary>내용을 고친다.</summary>
        /// <param name="world">세계.</param>
        internal void Refresh(World world)
        {
            for (int index = 0; index < _rows.Count; index++)
            {
                Region region = world.Regions[RegionCatalog.BorderIds[index]];
                (TextBlock owner, TextBlock battles, TugBar bar) = _rows[index];
                owner.Text = region.ControllerSectId == 0 ? "중립"
                    : Narrator.Affiliation(world, region.ControllerSectId);
                owner.Foreground = Palette.Brush(region.ControllerSectId == 0 ? Palette.Rogue
                    : Palette.Sect(region.ControllerSectId));
                string sign = region.ControlPoints > 0 ? "+" : string.Empty;
                battles.Text = "전투 " + Fmt.Number(region.Battles) + "회 · " + sign + region.ControlPoints;
                bar.Set(region.ControlPoints, WarSystem.PointLimit, WarSystem.CaptureThreshold);
            }
        }
    }
}
