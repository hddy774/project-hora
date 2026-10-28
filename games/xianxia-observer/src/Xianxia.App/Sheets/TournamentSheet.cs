// 천하 대비무 상세: 보존된 모든 경기, 라운드, 승패와 이변 여부를 20경기씩 표시한다.
using System;
using System.Collections.Generic;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>대비무 기록의 대진표 시트.</summary>
    internal sealed class TournamentSheet : SheetBase
    {
        private readonly KeyValueList _facts = new();
        private readonly RowList _matches;
        private readonly Pager _pager;

        /// <summary>대회 기록 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">대회 회차.</param>
        internal TournamentSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            _matches = new RowList(navigator.Open);
            _pager = new Pager(20, navigator.Invalidate);
            Body.Children.Add(_facts.View);
            Body.Children.Add(_pager.View);
            Body.Children.Add(_matches.View);
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            TournamentRecord? record = world.Tournaments.Find(item => item.Number == Link.Id);
            Title = "제" + Link.Id + "회 천하 대비무";
            _facts.Begin();
            if (record is null)
            {
                _facts.Add("기록", "보존된 대회 기록이 없습니다.");
                _facts.End();
                _matches.Update(Array.Empty<RowModel>());
                _pager.SetTotal(0, "경기");
                return;
            }

            _facts.Add("연도 / 참가자", Fmt.Years(record.Year) + " / " + record.Entrants + "명");
            _facts.Add("우승", record.ChampionName + " · " + record.ChampionAffiliation);
            _facts.Add("준우승 / 상금", record.RunnerUpName + " / " + Fmt.Number(record.Prize) + " 영석");
            _facts.End();
            _pager.SetTotal(record.Matches.Count, "경기");
            List<RowModel> rows = new();
            int end = Math.Min(record.Matches.Count, _pager.Start + _pager.PageSize);
            for (int index = _pager.Start; index < end; index++)
            {
                TournamentMatch match = record.Matches[index];
                rows.Add(new RowModel
                {
                    Title = match.WinnerName + " 승 · " + match.LoserName + " 패",
                    Subtitle = match.Round + "라운드" + (match.Upset ? " · 이변! 낮은 전투력으로 승리" : string.Empty),
                    Link = SheetLink.ToCultivator(match.WinnerId)
                });
                rows.Add(new RowModel
                {
                    Title = "상대: " + match.LoserName,
                    Link = SheetLink.ToCultivator(match.LoserId)
                });
            }

            _matches.Update(rows);
        }
    }
}
