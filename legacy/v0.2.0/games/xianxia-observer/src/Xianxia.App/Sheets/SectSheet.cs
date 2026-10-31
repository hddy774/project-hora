// 종문 상세: 정보·강자·공헌·시설·역사를 기존 종문 상태와 연결한다.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>종문 하나의 상세와 소속 인물로 이동하는 시트.</summary>
    internal sealed class SectSheet : SheetBase
    {
        private static readonly string[] s_tabs = { "정보", "강자", "공헌", "시설", "역사" };
        private readonly SectDetails _details;
        private readonly RowList _rows;
        private readonly Pager _pager;
        private int _tab;

        /// <summary>종문 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">종문 번호.</param>
        internal SectSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            _details = new SectDetails(navigator);
            _rows = new RowList(navigator.Open);
            _pager = new Pager(20, navigator.Invalidate);
            Tabs = new ChipBar(s_tabs, index =>
            {
                _tab = index;
                _pager.Reset();
                navigator.Invalidate();
            }).View;
            Body.Children.AddRange(new Control[] { _details.View, _pager.View, _rows.View });
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            Sect? sect = world.FindSect(Link.Id);
            if (sect is null)
            {
                Title = "존재하지 않는 종문";
                return;
            }

            Title = sect.Name;
            _details.View.IsVisible = _tab is 0 or 3;
            _pager.View.IsVisible = _tab is 1 or 2 or 4;
            _rows.View.IsVisible = _pager.View.IsVisible;
            _details.Refresh(world, sect);
            List<RowModel> rows = _tab == 4 ? History(world, sect) : Members(world);
            _pager.SetTotal(rows.Count, "건");
            _rows.Update(rows.GetRange(_pager.Start, Math.Min(_pager.PageSize, rows.Count - _pager.Start)));
        }

        private List<RowModel> Members(World world)
        {
            List<RowModel> rows = new();
            IEnumerable<Cultivator> members = _tab == 2
                ? world.Cultivators.Where(person => person.SectId == Link.Id)
                    .OrderByDescending(person => person.ContributionPoints).ThenBy(person => person.Id)
                : Queries.Strongest(world, world.Cultivators.Count, Link.Id).Select(item => item.Cultivator);
            foreach (Cultivator person in members)
            {
                double value = _tab == 2 ? person.ContributionPoints : PeopleQuery.Value(world, person, 1);
                rows.Add(Rows.Cultivator(world, person, rows.Count + 1, Fmt.Short(value), _tab == 2 ? "공헌" : "전투력"));
            }

            return rows;
        }

        private static List<RowModel> History(World world, Sect sect)
        {
            List<RowModel> rows = new();
            for (int index = world.Chronicle.Entries.Count - 1; index >= 0; index--)
            {
                ChronicleEntry entry = world.Chronicle.Entries[index];
                if (entry.Text.Contains(sect.Name, StringComparison.Ordinal))
                {
                    rows.Add(Rows.Chronicle(entry));
                }
            }

            return rows;
        }
    }
}
