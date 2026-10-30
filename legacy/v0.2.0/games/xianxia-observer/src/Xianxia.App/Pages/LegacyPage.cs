// 업적 탭: 역대 최고·상위 5생 합·종합 순위 및 호칭 조건 도감.
using System;
using System.Collections.Generic;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>영혼의 확정된 생애 업적을 탐색한다.</summary>
    internal sealed class LegacyPage : PageBase
    {
        private static readonly string[] s_tabs = { "역대 최고", "5생 합", "종합 순위", "호칭 도감" };
        private readonly RowList _rows;
        private readonly Pager _pager;
        private int _tab;

        /// <summary>업적 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal LegacyPage(INavigator navigator) : base(navigator)
        {
            _rows = new RowList(navigator.Open, "첫 생이 끝나면 업적이 기록됩니다.");
            _pager = new Pager(20, navigator.Invalidate);
            ChipBar tabs = new(s_tabs, index =>
            {
                _tab = index;
                _pager.Reset();
                navigator.Invalidate();
            });
            Body.Children.Add(Ui.Section("윤회의 업적", tabs.View,
                Ui.Muted("종합 순위 = 최고 점수 순위 + 5생 합 순위 (합이 작을수록 앞). 현생 점수는 미포함."),
                _pager.View, _rows.View));
        }

        /// <inheritdoc />
        internal override string Name => "업적";

        /// <inheritdoc />
        internal override string Glyph => "業";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            List<RowModel> rows = new();
            if (_tab == 3)
            {
                foreach (TitleDefinition title in TitleCatalog.All)
                {
                    rows.Add(new RowModel
                    {
                        Title = title.Name,
                        Subtitle = title.Description,
                        Value = title.Score + "점"
                    });
                }
            }
            else
            {
                AddRanking(world, rows);
            }

            _pager.SetTotal(rows.Count, "건");
            _rows.Update(rows.GetRange(_pager.Start, Math.Min(_pager.PageSize, rows.Count - _pager.Start)));
        }
        private void AddRanking(World world, List<RowModel> rows)
        {
            foreach ((SoulRecord soul, int bestRank, int fiveRank) in LegacyQuery.Select(world, _tab))
            {
                int place = rows.Count + 1;
                string tier = place <= 100 ? "1~100위" : place <= 300 ? "101~300위"
                    : place <= 500 ? "301~500위" : place <= 700 ? "501~700위" : "701위 이하";
                rows.Add(new RowModel
                {
                    Title = soul.Name,
                    Lead = place.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                    Subtitle = Realms.FullName(soul.BestRealm, soul.BestStage) + " · " + soul.Lives
                        + "생 · " + (_tab == 2 ? tier + " · 최고 " + bestRank + "위 / 5생 " + fiveRank + "위"
                            : soul.LastEnding),
                    Value = Fmt.Number(_tab == 0 ? soul.BestScore
                        : _tab == 1 ? soul.TopFiveSum : bestRank + fiveRank),
                    Caption = _tab == 2 ? "순위 합" : "점수",
                    Link = SheetLink.ToCultivator(soul.SoulId)
                });
            }
        }

    }
}
