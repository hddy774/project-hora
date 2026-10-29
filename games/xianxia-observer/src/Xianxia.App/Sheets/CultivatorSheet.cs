// 수선자 상세: 개요·수행·인연·생애. 기존 7분류 정보와 연결을 모두 보존한다.
using System.Collections.Generic;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Sheets
{
    /// <summary>현생 수선자와 비승·퇴장한 영혼의 상세 시트.</summary>
    internal sealed class CultivatorSheet : SheetBase
    {
        private static readonly string[] s_tabs = { "개요", "수행", "인연", "생애" };
        private readonly KeyValueList _facts = new();
        private readonly RowList _rows;
        private readonly Button _follow;
        private readonly Button _fortune;
        private readonly Button _sect;
        private readonly Button _region;
        private readonly Button _combat;
        private bool _showCombat;
        private int _tab;
        private int _sectId;
        private int _regionId;

        /// <summary>상세 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">영혼 번호.</param>
        internal CultivatorSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            _rows = new RowList(navigator.Open, "이 항목의 기록이 없습니다.");
            _follow = Ui.Button("관심", ToggleFollow, "small");
            _fortune = Ui.Button("기연 하사 · 천기 2", BestowFortune, "small");
            _sect = Ui.Button("소속", () => navigator.Open(SheetLink.ToSect(_sectId)), "small");
            _region = Ui.Button("지역", () => navigator.Open(SheetLink.ToRegion(_regionId)), "small");
            _combat = Ui.Button("전투력 상세 ▾", () =>
            {
                _showCombat = !_showCombat;
                navigator.Invalidate();
            }, "small");
            Tabs = new ChipBar(s_tabs,
                index => { _tab = index; navigator.Invalidate(); }).View;
            Body.Children.Add(Ui.Row(5, _follow, _fortune));
            Body.Children.Add(Ui.Row(5, _sect, _region));
            Body.Children.Add(_combat);
            Body.Children.Add(_facts.View);
            Body.Children.Add(_rows.View);
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            Cultivator? person = world.Find(Link.Id);
            SoulRecord? soul = world.FindSoul(Link.Id);
            Title = person?.DisplayName ?? soul?.Name ?? "존재하지 않는 영혼";
            _follow.Content = world.IsFollowed(Link.Id) ? "★ 관심 해제" : "☆ 관심 표시";
            _follow.IsEnabled = person is not null || soul is not null;
            _fortune.IsEnabled = person is not null && InterventionSystem.CanBestowFortune(world, person);
            _combat.IsVisible = person is not null && _tab == 0;
            _combat.Content = _showCombat ? "전투력 상세 ▴" : "전투력 상세 ▾";
            _sect.IsVisible = person is not null;
            _region.IsVisible = person is not null;
            _facts.Begin();
            List<RowModel> rows = new();
            if (person is not null)
            {
                _sectId = person.SectId;
                _regionId = person.RegionId;
                _sect.Content = Fmt.Affiliation(world, person);
                _sect.IsEnabled = !person.IsRogue;
                _region.Content = world.Regions[person.RegionId].Name + " ›";
                CultivatorDetails.Fill(_facts, world, person, _tab, _showCombat);
                FillRows(world, person, rows);
            }
            else
            {
                AddSoul(soul);
            }

            _facts.End();
            _rows.Update(rows);
        }

        private void FillRows(World world, Cultivator person, List<RowModel> rows)
        {
            switch (_tab)
            {
                case 2:
                    AddRelations(world, person, rows);
                    break;
                case 3:
                    AddBiography(world, person, rows);
                    AddPastLives(world, person, rows);
                    break;
            }
        }

        private static void AddRelations(World world, Cultivator person, List<RowModel> rows)
        {
            AddRelation(world, rows, person.MasterId, "스승");
            AddRelation(world, rows, person.CompanionId, "도반");
            foreach (int id in person.DiscipleIds)
            {
                AddRelation(world, rows, id, "제자");
            }

            foreach (int id in person.RivalIds)
            {
                AddRelation(world, rows, id, "숙적");
            }
        }

        private static void AddBiography(World world, Cultivator person, List<RowModel> rows)
        {
            foreach (LifeEvent entry in person.Biography)
            {
                rows.Add(new RowModel
                {
                    Title = entry.Text,
                    Value = Fmt.Years(entry.Year),
                    Caption = entry.Age + "세"
                });
            }

            rows.Reverse();
        }

        private void AddPastLives(World world, Cultivator person, List<RowModel> rows)
        {
            AddSoul(world.FindSoul(person.Id));
            List<RowModel> pastRows = new();
            foreach (PastLife life in person.PastLives)
            {
                pastRows.Add(new RowModel
                {
                    Title = life.LifeNumber + "번째 생 · " + life.Ending,
                    Subtitle = Realms.FullName(life.Realm, life.Stage) + " · " + life.Age + "세 · "
                        + "입세 " + Fmt.Years(life.BornYear) + "~" + Fmt.Years(life.DeathYear),
                    Value = Fmt.Number(life.Score),
                    Caption = "업적 점수"
                });
            }

            pastRows.Reverse();
            rows.AddRange(pastRows);
        }

        private void AddSoul(SoulRecord? soul)
        {
            if (soul is null)
            {
                _facts.Add("영혼 기록", "아직 완료된 생이 없습니다.");
                return;
            }

            _facts.Add("완료한 생 / 마지막 끝", soul.Lives + "생 / " + soul.LastEnding);
            _facts.Add("최고 경지", Realms.FullName(soul.BestRealm, soul.BestStage));
            _facts.Add("최고 점수 / 5생 합", soul.BestScore + " / " + soul.TopFiveSum);
            _facts.Add("누적 승리 / 돌파 / 천겁", soul.TotalWins + " / " + soul.TotalMajorBreakthroughs
                + " / " + soul.TotalTribulations);
            _facts.Add("비승", soul.Ascended ? "비승한 선인" : "아직 비승하지 않음");
        }

        private static void AddRelation(World world, List<RowModel> rows, int id, string relation)
        {
            if (id != 0)
            {
                rows.Add(new RowModel
                {
                    Title = relation + ": " + Queries.NameOf(world, id),
                    Link = SheetLink.ToCultivator(id)
                });
            }
        }

        private void ToggleFollow()
        {
            Navigator.Session.Runner.Write(world =>
            {
                world.SetFollowed(Link.Id, !world.IsFollowed(Link.Id));

                return true;
            });
            Navigator.Invalidate();
        }

        private void BestowFortune()
        {
            bool success = Navigator.Session.Runner.Write(world => world.Find(Link.Id) is { } person
                && InterventionSystem.BestowFortune(world, person));
            Navigator.Toast(success ? "기연을 내렸습니다. 천기 2점을 사용했습니다." : "대상이 없거나 천기가 부족합니다.");
            Navigator.Invalidate();
        }
    }
}
