// 천하 탭(원본 "전쟁" 탭 확장): 전쟁·휴전 현황, 두 종문 대결 카드, 지역 약도, 접경지 세력도, 진행 중인 사건,
// 누적 통계, 천하 십강, 최근 연대기.
using System;
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
    /// <summary>천하 탭.</summary>
    internal sealed class WorldPage : PageBase
    {
        private readonly TextBlock _warTitle = Ui.Text(string.Empty, 15, bold: true);
        private readonly TextBlock _warLine = Ui.Muted(string.Empty, 11);
        private readonly TextBlock _warDetail = Ui.Text(string.Empty, 12);
        private readonly TextBlock _warHighlights = Ui.Muted(string.Empty, 10.5);
        private readonly Border _warPanel;
        private readonly SectCard _orthodox;
        private readonly SectCard _demonic;
        private readonly RegionMap _map = new();
        private readonly BorderRows _borders;
        private readonly KeyValueList _events = new();
        private readonly RowList _incidents;
        private readonly TextBlock[] _totals = new TextBlock[8];
        private readonly RowList _strongest;
        private readonly RowList _recent;

        /// <summary>천하 탭을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal WorldPage(INavigator navigator)
            : base(navigator)
        {
            _warTitle.HorizontalAlignment = HorizontalAlignment.Center;
            _warLine.HorizontalAlignment = HorizontalAlignment.Center;
            _warDetail.HorizontalAlignment = HorizontalAlignment.Center;
            _warDetail.TextAlignment = TextAlignment.Center;
            _warPanel = Ui.Panel(Ui.Column(3, _warTitle, _warLine, _warDetail, _warHighlights));
            Body.Children.Add(_warPanel);

            _orthodox = new SectCard(navigator, RegionCatalog.OrthodoxSectId);
            _demonic = new SectCard(navigator, RegionCatalog.DemonicSectId);
            TextBlock versus = Ui.Text("VS", 18, Palette.Demonic, bold: true);
            versus.VerticalAlignment = VerticalAlignment.Center;
            versus.Margin = new Thickness(6, 0);
            Body.Children.Add(Ui.Columns("*,Auto,*", _orthodox.View, versus, _demonic.View));

            _map.RegionTapped += regionId => Navigator.Open(SheetLink.ToRegion(regionId));
            Body.Children.Add(Ui.Section("천하 약도", _map,
                Ui.Muted("원 안 숫자: 거주자 · 색: 지배 종문 · 점선: 접경지 · 秘 비경 · 獸 수조 · 누르면 지역 정보", 10)));

            _borders = new BorderRows(navigator);
            Body.Children.Add(Ui.Section("접경지 세력도 (±4점이면 점령)", _borders.View));

            _incidents = new RowList(Navigator.Open, "진행 중인 비경·수조가 없습니다.");
            Body.Children.Add(Ui.Section("천하의 사건", _events.View, _incidents.View));

            string[] totalNames = { "전투", "종문 대비무", "대경지 돌파", "천겁", "비승", "수명 좌화", "경지 붕괴", "배신" };
            Control[] cells = new Control[totalNames.Length];
            for (int index = 0; index < totalNames.Length; index++)
            {
                _totals[index] = new TextBlock();
                cells[index] = Ui.Stat(totalNames[index], _totals[index]);
            }

            Body.Children.Add(Ui.Section("누적 통계", Ui.Cells(4, 4, cells)));
            _strongest = new RowList(Navigator.Open);
            Body.Children.Add(Ui.Section("천하 십강 (전투력)", _strongest.View));
            _recent = new RowList(Navigator.Open);
            Body.Children.Add(Ui.Section("최근 연대기", _recent.View));
        }

        /// <inheritdoc />
        internal override string Name => "천하";

        /// <inheritdoc />
        internal override string Glyph => "天";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            RefreshWar(world);
            SectSummary orthodox = Queries.Summarize(world, RegionCatalog.OrthodoxSectId);
            SectSummary demonic = Queries.Summarize(world, RegionCatalog.DemonicSectId);
            _orthodox.Refresh(world, orthodox);
            _demonic.Refresh(world, demonic);
            int[] residents = Queries.ResidentCounts(world);
            MapRegion[] regions = new MapRegion[world.Regions.Count];
            for (int index = 0; index < regions.Length; index++)
            {
                Region region = world.Regions[index];
                regions[index] = new MapRegion(index, region.ControllerSectId, residents[index],
                    Queries.SecretRealmAt(world, index) is not null, Queries.BeastTideAt(world, index) is not null);
            }

            _map.Set(regions);
            _borders.Refresh(world);
            RefreshEvents(world);
            RefreshTotals(world);
            List<RowModel> strongest = new();
            int place = 1;
            foreach ((Cultivator cultivator, double power) in Queries.Strongest(world, 10))
            {
                strongest.Add(Rows.Cultivator(world, cultivator, place++, Fmt.Short(power), "전투력"));
            }

            _strongest.Update(strongest);
            List<RowModel> recent = new();
            List<ChronicleEntry> entries = world.Chronicle.Entries;
            for (int index = entries.Count - 1; index >= 0 && recent.Count < 5; index--)
            {
                recent.Add(Rows.Chronicle(entries[index]));
            }

            _recent.Update(recent);
        }

        private void RefreshWar(World world)
        {
            int change = WarSystem.YearsUntilChange(world.Year);
            WarState war = world.War;
            Color color = war.AtWar ? Palette.War : Palette.Peace;
            _warPanel.BorderBrush = Palette.Tint(color, 0x90);
            _warPanel.Background = Palette.Tint(color, 0x12);
            _warTitle.Foreground = Palette.Brush(color);
            if (war.AtWar)
            {
                _warTitle.Text = "제" + war.WarNumber + "차 정마대전 진행 중";
                _warLine.Text = "종전까지 " + Fmt.Years(change) + " (천 년 전쟁 · 만 년 휴전)";
                _warDetail.Text = "청운종 " + war.OrthodoxWins + "승 : " + war.DemonicWins + "승 천마문 · 사상자 "
                    + Fmt.Number(war.Casualties) + "명";
                _warHighlights.Text = Highlights(war.Highlights);
                _warHighlights.IsVisible = war.Highlights.Count > 0;
                return;
            }

            _warTitle.Text = world.Year < Balance.FirstWarYear ? "태평성대 · 첫 전쟁 전" : "휴전 · 천하 대비무의 시대";
            _warLine.Text = "다음 전쟁까지 " + Fmt.Years(change);
            int tournament = TournamentSystem.YearsUntilNext(world);
            string last = world.Wars.Count == 0 ? "아직 전쟁 기록이 없다."
                : LastWar(world, world.Wars[^1]);
            _warDetail.Text = "다음 대비무: " + Fmt.After(tournament) + "\n" + last;
            _warHighlights.IsVisible = false;
        }

        private static string LastWar(World world, WarRecord record)
        {
            string victor = record.VictorSectId == 0 ? "무승부" : Narrator.Affiliation(world, record.VictorSectId) + " 승리";
            return "지난 전쟁: 제" + record.Number + "차 " + victor + " · 영웅 " + record.HeroName;
        }

        private static string Highlights(List<string> highlights)
        {
            int start = Math.Max(0, highlights.Count - 3);
            List<string> lines = new();
            for (int index = highlights.Count - 1; index >= start; index--)
            {
                lines.Add("· " + highlights[index]);
            }

            return string.Join("\n", lines);
        }

        private void RefreshEvents(World world)
        {
            _events.Begin();
            _events.Add("시대", Labels.Of(world.Era.Kind) + " (" + Fmt.Years(Math.Max(0, world.Era.EndYear - world.Year))
                + " 남음)");
            _events.Add("시대의 기운", EraSystem.Describe(world.Era.Kind));
            _events.Add("다음 천하 대비무", Fmt.After(TournamentSystem.YearsUntilNext(world)));
            _events.Add("다음 중원 경매", Fmt.After(AuctionSystem.YearsUntilNext(world.Year)));
            _events.Add("천기(관찰자 개입)", world.HeavenlyFavor + " / " + Balance.MaxHeavenlyFavor + " · 200년마다 1점",
                Palette.Favor);
            _events.End();
            List<RowModel> incidents = new();
            foreach (SecretRealmState realm in world.SecretRealms)
            {
                incidents.Add(new RowModel
                {
                    Title = realm.Name + " — " + world.Regions[realm.RegionId].Name,
                    Subtitle = Realms.Name(realm.MinRealm) + "~" + Realms.Name(realm.MaxRealm) + " 입장 · 보물 "
                        + realm.Treasures + "개 남음 · 진입 " + realm.Entrants.Count + "명",
                    Value = Fmt.Years(Math.Max(0, realm.CloseYear - world.Year)),
                    Caption = "닫힐 때까지",
                    Accent = Palette.Of(LogKind.SecretRealm),
                    Badges = new[] { new BadgeSpec(realm.Summoned ? "천기 개방" : "비경", Palette.Of(LogKind.SecretRealm)) },
                    Link = SheetLink.ToRegion(realm.RegionId),
                });
            }

            foreach (BeastTideState tide in world.BeastTides)
            {
                double ratio = tide.InitialStrength <= 0 ? 0 : tide.Strength / tide.InitialStrength;
                incidents.Add(new RowModel
                {
                    Title = tide.BeastName + " 수조 — " + world.Regions[tide.RegionId].Name,
                    Subtitle = Realms.Name(tide.Realm) + " 요수 · 남은 기세 " + Fmt.Percent(ratio) + " · 방어 "
                        + tide.DefenderIds.Count + "명",
                    Value = Fmt.Years(Math.Max(0, tide.EndYear - world.Year)),
                    Caption = "잦아들 때까지",
                    Accent = Palette.Danger,
                    Badges = new[] { new BadgeSpec(tide.Summoned ? "천기 소환" : "수조", Palette.Danger) },
                    Link = SheetLink.ToRegion(tide.RegionId),
                });
            }

            _incidents.Update(incidents);
        }

        private void RefreshTotals(World world)
        {
            WorldTotals totals = world.Totals;
            long[] values =
            {
                totals.Battles, totals.CrossSectSpars, totals.MajorBreakthroughs, totals.Tribulations,
                totals.Ascensions, totals.NaturalDeaths, totals.CollapseDeaths, totals.Defections,
            };
            Color[] colors =
            {
                Palette.Of(LogKind.Battle), Palette.Of(LogKind.Tournament), Palette.Of(LogKind.Breakthrough),
                Palette.Of(LogKind.Tribulation), Palette.Gold, Palette.Rogue, Palette.Danger, Palette.Demonic,
            };
            for (int index = 0; index < values.Length; index++)
            {
                _totals[index].Text = Fmt.Short(values[index]);
                _totals[index].Foreground = Palette.Brush(colors[index]);
            }
        }
    }
}
