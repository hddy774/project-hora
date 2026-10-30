// 현황 탭: 경지 분포와 통계 추이, 영석 보존 장부, 누적 사건 건수를 표시한다.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Theme;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>관측된 값만 보여 주는 세계 통계 화면.</summary>
    internal sealed class StatsPage : PageBase
    {
        private readonly KeyValueList _realms = new();
        private readonly KeyValueList _ledger = new();
        private readonly KeyValueList _events = new();
        private readonly StackChart _composition = new();
        private readonly LineChart _members = new();
        private readonly LineChart _power = new();
        private readonly LineChart _treasury = new();
        private readonly BadgeStrip _realmLegend = new();

        /// <summary>통계 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal StatsPage(INavigator navigator) : base(navigator)
        {
            Body.Children.Add(Ui.Section("현재 경지 분포", _realms.View));
            Body.Children.Add(Ui.Section("경지 구성 추이 · 인구 비율", _composition, _realmLegend.View,
                Ui.Muted("가로축은 실제 표본 연도입니다. 오래된 표본의 간격이 넓어질 수 있습니다.")));
            Body.Children.Add(Ui.Section("인구 추이 · 명", SectLegend(true), _members));
            Body.Children.Add(Ui.Section("전투력 추이", SectLegend(false), _power));
            Body.Children.Add(Ui.Section("금고 추이 · 영석", SectLegend(false), _treasury));
            Body.Children.Add(Ui.Section("영석 장부", _ledger.View));
            Body.Children.Add(Ui.Section("누적 사건 통계", _events.View));
        }

        /// <inheritdoc />
        internal override string Name => "현황";

        /// <inheritdoc />
        internal override string Glyph => "統";

        private static Control SectLegend(bool includeRogues)
        {
            BadgeStrip legend = new();
            List<BadgeSpec> badges = new()
            {
                new BadgeSpec("청운종", Palette.Orthodox), new BadgeSpec("천마문", Palette.Demonic),
            };
            if (includeRogues)
            {
                badges.Add(new BadgeSpec("산수", Palette.Rogue));
            }

            legend.Set(badges.ToArray());
            return legend.View;
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            int[] counts = new int[Realms.Count];
            long held = 0;
            foreach (Cultivator person in world.Cultivators)
            {
                counts[person.Realm]++;
                held += person.SpiritStones;
            }

            held += world.Sects.Sum(sect => sect.Treasury);
            _realms.Begin();
            for (int realm = 0; realm < counts.Length; realm++)
            {
                _realms.Add(Realms.Name(realm), counts[realm] + "명 · "
                    + Fmt.Percent(counts[realm] / (double)Math.Max(1, world.Cultivators.Count)), Palette.Realm(realm));
            }

            _realms.End();
            _realmLegend.Set(Enumerable.Range(0, Realms.Count)
                .Select(realm => new BadgeSpec(Realms.Name(realm), Palette.Realm(realm))).ToArray());
            RefreshLedger(world, held);
            RefreshCharts(world.Statistics.Samples);
            _events.Begin();
            foreach (LogKind kind in Enum.GetValues<LogKind>())
            {
                _events.Add(Labels.Of(kind), Fmt.Number(world.Log.Counts[(int)kind]), Palette.Of(kind));
            }

            _events.End();
        }

        private void RefreshLedger(World world, long held)
        {
            string[] sources = { "처음 지급", "탐색", "상회 판매", "영지 산출", "기연·비경", "임무" };
            string[] sinks = { "상회 구입", "시설", "경매", "제련", "비승", "군비", "유지비", "유품 소실", "수행 자원" };
            _ledger.Begin();
            _ledger.Add("실제 보유 / 장부 총량", Fmt.Number(held) + " / " + Fmt.Number(world.Ledger.Expected()));
            _ledger.Add("보존 차이 (정상: 0)", Fmt.Number(held - world.Ledger.Expected()));
            for (int index = 0; index < sources.Length; index++)
            {
                _ledger.Add("생성 · " + sources[index], Fmt.Number(world.Ledger.Created[index]));
            }

            for (int index = 0; index < sinks.Length; index++)
            {
                _ledger.Add("소멸 · " + sinks[index], Fmt.Number(world.Ledger.Destroyed[index]));
            }

            _ledger.End();
        }

        private void RefreshCharts(List<StatSample> samples)
        {
            int[] years = samples.Select(sample => sample.Year).ToArray();
            List<IReadOnlyList<int>> layers = samples.Select(sample => (IReadOnlyList<int>)sample.RealmCounts).ToList();
            Color[] colors = Enumerable.Range(0, Realms.Count).Select(Palette.Realm).ToArray();
            _composition.Set(layers, colors, years);
            _members.Set(new[]
            {
                new ChartSeries("청운종", Palette.Orthodox,
                    samples.Select(sample => (double)sample.OrthodoxMembers).ToArray()),
                new ChartSeries("천마문", Palette.Demonic,
                    samples.Select(sample => (double)sample.DemonicMembers).ToArray()),
                new ChartSeries("산수", Palette.Rogue, samples.Select(sample => (double)sample.Rogues).ToArray()),
            }, years, Fmt.Short);
            _power.Set(new[]
            {
                new ChartSeries("청운종", Palette.Orthodox, samples.Select(sample => sample.OrthodoxPower).ToArray()),
                new ChartSeries("천마문", Palette.Demonic, samples.Select(sample => sample.DemonicPower).ToArray()),
            }, years, Fmt.Short);
            _treasury.Set(new[]
            {
                new ChartSeries("청운종", Palette.Orthodox,
                    samples.Select(sample => (double)sample.OrthodoxTreasury).ToArray()),
                new ChartSeries("천마문", Palette.Demonic,
                    samples.Select(sample => (double)sample.DemonicTreasury).ToArray()),
            }, years, Fmt.Short);
        }
    }
}
