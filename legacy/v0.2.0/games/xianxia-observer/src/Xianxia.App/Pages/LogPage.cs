// 기록 탭: 사건 종류와 관심 인물 필터. 최신 기록부터 40건씩 보인다.
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>보존된 사건 기록의 분류와 관심 필터.</summary>
    internal sealed class LogPage : PageBase
    {
        private readonly RowList _rows;
        private readonly Pager _pager;
        private readonly CheckBox _followed = new() { Content = "관심 인물만" };
        private int _kind;

        /// <summary>기록 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal LogPage(INavigator navigator) : base(navigator)
        {
            _rows = new RowList(navigator.Open, "조건에 맞는 기록이 없습니다.");
            _pager = new Pager(40, navigator.Invalidate);
            List<string> labels = new() { "전체" };
            foreach (LogKind kind in Enum.GetValues<LogKind>())
            {
                labels.Add(Labels.Of(kind));
            }

            ChipBar filters = new(labels, index => { _kind = index; Reset(); });
            _followed.IsCheckedChanged += (_, _) => Reset();
            Body.Children.Add(Ui.Section("사건 기록", filters.View, _followed, _pager.View, _rows.View));
        }

        /// <inheritdoc />
        internal override string Name => "기록";

        /// <inheritdoc />
        internal override string Glyph => "錄";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            List<LogEntry> entries = world.Log.CopyNewestFirst(int.MaxValue, entry =>
                (_kind == 0 || (int)entry.Kind == _kind - 1) && (_followed.IsChecked != true
                    || world.IsFollowed(entry.SubjectId) || world.IsFollowed(entry.OtherId)));
            _pager.SetTotal(entries.Count, "건");
            List<RowModel> rows = new();
            for (int index = _pager.Start; index < entries.Count && rows.Count < _pager.PageSize; index++)
            {
                rows.Add(Rows.Log(world, entries[index]));
            }

            _rows.Update(rows);
        }

        private void Reset()
        {
            _pager.Reset();
            Navigator.Invalidate();
        }
    }
}
