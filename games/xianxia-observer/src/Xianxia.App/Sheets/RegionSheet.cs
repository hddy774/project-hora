// 지역 상세: 환경·현재 비경·수조·시장 재고·거주자, 천기 개입을 표시한다.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Sheets
{
    /// <summary>지역을 관찰하고 천기 3점으로 비경 또는 수조를 여는 상세 시트.</summary>
    internal sealed class RegionSheet : SheetBase
    {
        private static readonly string[] s_tabs = { "지역 정보", "시장", "거주자" };
        private readonly KeyValueList _facts = new();
        private readonly TextBlock _description = Ui.Muted();
        private readonly TextBlock _favor = Ui.Muted();
        private readonly RowList _residents;
        private readonly Pager _pager;
        private readonly Button _secret;
        private readonly Button _tide;
        private int _tab;

        /// <summary>지역 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">지역 번호。</param>
        internal RegionSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            _residents = new RowList(navigator.Open, "거주자가 없습니다.");
            _pager = new Pager(20, navigator.Invalidate);
            _secret = Ui.Button("비경 개방 · 천기 3", () => Intervene(false), "small");
            _tide = Ui.Button("수조 소환 · 천기 3", () => Intervene(true), "small");
            Tabs = new ChipBar(s_tabs, index =>
            {
                _tab = index;
                _pager.Reset();
                navigator.Invalidate();
            }).View;
            Body.Children.AddRange(new Control[]
            {
                _description, _favor, Ui.Row(5, _secret, _tide), _facts.View, _pager.View, _residents.View,
            });
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            if (Link.Id < 0 || Link.Id >= world.Regions.Count)
            {
                Title = "존재하지 않는 지역";
                _secret.IsEnabled = false;
                _tide.IsEnabled = false;
                return;
            }

            Region region = world.Regions[Link.Id];
            Title = region.Name;
            _description.Text = region.Definition.Description;
            _favor.Text = "보유 천기 " + world.HeavenlyFavor + " / " + Balance.MaxHeavenlyFavor;
            if (Link.Id is 0 or 4)
            {
                _favor.Text += " · 본산에는 천기가 닿지 않습니다.";
            }

            _secret.IsEnabled = InterventionSystem.CanOpenSecretRealm(world, Link.Id);
            _tide.IsEnabled = InterventionSystem.CanSummonBeastTide(world, Link.Id);
            _facts.Begin();
            if (_tab == 0)
            {
                Information(world, region);
            }
            else if (_tab == 1)
            {
                Market(region);
            }

            _facts.End();
            _pager.View.IsVisible = _tab == 2;
            _residents.View.IsVisible = _tab == 2;
            if (_tab == 2)
            {
                RefreshResidents(world, region);
            }
        }

        private void Information(World world, Region region)
        {
            _facts.Add("종류 / 지배", Labels.Of(region.Kind) + " / "
                + (world.FindSect(region.ControllerSectId)?.Name ?? "중립"));
            _facts.Add("영기 / 위험", Fmt.Factor(region.Aura) + " / " + Fmt.Percent(region.Definition.Danger));
            _facts.Add("영초 / 영철 풍부도", Fmt.Factor(region.Definition.Herbs) + " / "
                + Fmt.Factor(region.Definition.Ore));
            _facts.Add("거주자 / 전투", region.Residents.Count + "명 / " + region.Battles + "회");
            _facts.Add("상태", region.Status);
            SecretRealmState? secret = Queries.SecretRealmAt(world, Link.Id);
            _facts.Add("비경", secret is null ? "없음" : secret.Name + " · 보물 " + secret.Treasures + "개 · "
                + Fmt.Years(Math.Max(0, secret.CloseYear - world.Year)) + " 남음");
            BeastTideState? tide = Queries.BeastTideAt(world, Link.Id);
            _facts.Add("수조", tide is null ? "없음" : tide.BeastName + " · "
                + Fmt.Percent(tide.Strength / Math.Max(1, tide.InitialStrength)) + " 남음");
        }

        private void Market(Region region)
        {
            foreach (PillKind kind in Enum.GetValues<PillKind>())
            {
                _facts.Add(Labels.Of(kind), region.MarketPills[(int)kind] + "개");
            }

            foreach (TalismanKind kind in Enum.GetValues<TalismanKind>())
            {
                _facts.Add(Labels.Of(kind), region.MarketTalismans[(int)kind] + "개");
            }
        }

        private void RefreshResidents(World world, Region region)
        {
            Cultivator[] people = region.Residents.OrderByDescending(person => person.Realm)
                .ThenByDescending(person => person.Stage).ThenBy(person => person.Id).ToArray();
            _pager.SetTotal(people.Length, "명");
            List<RowModel> rows = new();
            for (int index = _pager.Start; index < people.Length && rows.Count < _pager.PageSize; index++)
            {
                rows.Add(Rows.Cultivator(world, people[index], index + 1, people[index].RealmText, "경지"));
            }

            _residents.Update(rows);
        }

        private void Intervene(bool tide)
        {
            bool success = Navigator.Session.Runner.Write(world =>
            {
                return tide ? InterventionSystem.SummonBeastTide(world, Link.Id)
                    : InterventionSystem.OpenSecretRealm(world, Link.Id);
            });
            Navigator.Toast(success ? "천기 3점을 사용했습니다." : "천기가 부족하거나 같은 사건이 진행 중입니다.");
            Navigator.Invalidate();
        }
    }
}
