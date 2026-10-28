// 재사용 줄 목록과 쪽 넘김(원본 PgCtrl: "< 1/10 >"). 목록 길이가 바뀌어도 줄 객체를 다시 만들지 않는다.
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Xianxia.App.Views;

namespace Xianxia.App.Controls
{
    /// <summary>줄 목록.</summary>
    internal sealed class RowList
    {
        private readonly StackPanel _panel = new() { Spacing = 4 };
        private readonly List<InfoRow> _rows = new();
        private readonly TextBlock _empty;
        private readonly Action<SheetLink> _open;

        /// <summary>목록을 만든다.</summary>
        /// <param name="open">항목을 눌렀을 때 시트를 여는 동작.</param>
        /// <param name="emptyText">비었을 때 안내.</param>
        internal RowList(Action<SheetLink> open, string emptyText = "아직 기록이 없습니다.")
        {
            _open = open;
            _empty = Ui.Muted(emptyText, 12);
            _empty.HorizontalAlignment = HorizontalAlignment.Center;
            _empty.Margin = new Avalonia.Thickness(0, 18);
            _panel.Children.Add(_empty);
        }

        /// <summary>화면 요소.</summary>
        internal Control View => _panel;

        /// <summary>줄 내용을 바꾼다.</summary>
        /// <param name="models">줄들.</param>
        internal void Update(IReadOnlyList<RowModel> models)
        {
            while (_rows.Count < models.Count)
            {
                InfoRow row = new(_open);
                _rows.Add(row);
                _panel.Children.Add(row.View);
            }

            for (int index = 0; index < _rows.Count; index++)
            {
                bool visible = index < models.Count;
                _rows[index].View.IsVisible = visible;
                if (visible)
                {
                    _rows[index].Update(models[index]);
                }
            }

            _empty.IsVisible = models.Count == 0;
        }
    }

    /// <summary>쪽 넘김 막대.</summary>
    internal sealed class Pager
    {
        private readonly Button _previous;
        private readonly Button _next;
        private readonly TextBlock _label;
        private readonly TextBlock _total;
        private readonly Action _changed;
        private int _pageCount = 1;

        /// <summary>쪽 넘김을 만든다.</summary>
        /// <param name="pageSize">한 쪽 항목 수.</param>
        /// <param name="changed">쪽이 바뀌었을 때.</param>
        internal Pager(int pageSize, Action changed)
        {
            PageSize = pageSize;
            _changed = changed;
            _previous = Ui.Button("‹ 이전", () => Move(-1), "small");
            _next = Ui.Button("다음 ›", () => Move(1), "small");
            _label = Ui.Muted(string.Empty, 11);
            _label.VerticalAlignment = VerticalAlignment.Center;
            _total = Ui.Muted(string.Empty, 11);
            _total.VerticalAlignment = VerticalAlignment.Center;
            StackPanel buttons = Ui.Row(6, _previous, _label, _next);
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            View = Ui.Columns("*,Auto", _total, buttons);
        }

        /// <summary>화면 요소.</summary>
        internal Control View { get; }

        /// <summary>현재 쪽(0부터).</summary>
        internal int Page { get; private set; }

        /// <summary>한 쪽 항목 수.</summary>
        internal int PageSize { get; }

        /// <summary>현재 쪽의 첫 항목 위치.</summary>
        internal int Start => Page * PageSize;

        /// <summary>전체 항목 수를 알려 주고 쪽 표시를 고친다.</summary>
        /// <param name="total">전체 항목 수.</param>
        /// <param name="unit">개수 단위(예: "명").</param>
        internal void SetTotal(int total, string unit)
        {
            _pageCount = Math.Max(1, (total + PageSize - 1) / PageSize);
            Page = Math.Clamp(Page, 0, _pageCount - 1);
            _label.Text = (Page + 1) + " / " + _pageCount;
            _total.Text = total.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) + unit;
            _previous.IsEnabled = Page > 0;
            _next.IsEnabled = Page < _pageCount - 1;
        }

        /// <summary>첫 쪽으로 돌아간다(정렬·필터가 바뀌었을 때).</summary>
        internal void Reset()
        {
            Page = 0;
        }

        private void Move(int delta)
        {
            int next = Math.Clamp(Page + delta, 0, _pageCount - 1);
            if (next == Page)
            {
                return;
            }

            Page = next;
            _changed();
        }
    }
}
