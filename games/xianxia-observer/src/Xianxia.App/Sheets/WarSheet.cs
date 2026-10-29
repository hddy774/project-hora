// 정마대전 상세: 종료된 전쟁 기록과 진행 중인 전쟁을 구분해 전적·점령·배상·영웅·주요 장면을 표시한다.
using System.Collections.Generic;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>전쟁 회차별 기록 시트.</summary>
    internal sealed class WarSheet : SheetBase
    {
        private readonly KeyValueList _facts = new();
        private readonly RowList _highlights;

        /// <summary>전쟁 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">전쟁 회차.</param>
        internal WarSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            _highlights = new RowList(navigator.Open);
            Body.Children.Add(_facts.View);
            Body.Children.Add(_highlights.View);
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            Title = "제" + Link.Id + "차 정마대전";
            WarRecord? record = world.Wars.Find(war => war.Number == Link.Id);
            List<RowModel> rows = new();
            _facts.Begin();
            if (record is not null)
            {
                _facts.Add("기간", Fmt.Years(record.StartYear) + "~" + Fmt.Years(record.EndYear));
                _facts.Add("승자", world.FindSect(record.VictorSectId)?.Name ?? "무승부");
                _facts.Add("청운종 / 천마문 승수", record.OrthodoxWins + " / " + record.DemonicWins);
                _facts.Add("접경지 청운종 / 천마문", record.OrthodoxRegions + " / " + record.DemonicRegions);
                _facts.Add("사상자 / 배상 영석", record.Casualties + " / " + Fmt.Number(record.Tribute));
                _facts.Add("대표 결투", record.ChampionDuel);
                rows.Add(new RowModel
                {
                    Title = "전쟁 영웅: " + record.HeroName,
                    Link = SheetLink.ToCultivator(record.HeroId)
                });
                AddHighlights(rows, record.Highlights);
            }
            else if (world.War.AtWar && world.War.WarNumber == Link.Id)
            {
                _facts.Add("상태", "전쟁 진행 중 · " + Fmt.Years(world.War.StartYear) + " 개전");
                _facts.Add("청운종 / 천마문 승수", world.War.OrthodoxWins + " / " + world.War.DemonicWins);
                _facts.Add("사상자", Fmt.Number(world.War.Casualties));
                AddHighlights(rows, world.War.Highlights);
            }
            else
            {
                _facts.Add("기록", "보존된 전쟁 기록이 없습니다.");
            }

            _facts.End();
            _highlights.Update(rows);
        }

        private static void AddHighlights(List<RowModel> rows, List<string> highlights)
        {
            foreach (string text in highlights)
            {
                rows.Add(new RowModel { Title = text });
            }
        }
    }
}
