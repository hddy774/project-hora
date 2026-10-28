// 인물 탭: 8종 정렬, 소속·관심 필터, 이름 검색과 20명 단위 쪽 넘김을 제공한다.
using System.Collections.Generic;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>현생 수선자 목록. 검색 입력은 갱신 때 다시 만들지 않아 키보드 포커스를 유지한다.</summary>
    internal sealed class PeoplePage : PageBase
    {
        private static readonly string[] s_affiliations = { "전체", "청운종", "천마문", "산수", "관심 ★" };
        private static readonly string[] s_sorts = { "경지", "전투력", "나이", "재능", "승리", "영석", "윤회", "업적" };
        private readonly RowList _rows;
        private readonly Pager _pager;
        private readonly TextBox _search = new() { PlaceholderText = "이름·도호 검색" };
        private int _sort;
        private int _affiliation;

        /// <summary>인물 탐색 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal PeoplePage(INavigator navigator) : base(navigator)
        {
            _rows = new RowList(navigator.Open, "조건에 맞는 인물이 없습니다.");
            _pager = new Pager(20, navigator.Invalidate);
            ChipBar sorts = new(s_sorts, index => { _sort = index; Reset(); });
            ChipBar affiliations = new(s_affiliations,
                index => { _affiliation = index; Reset(); });
            _search.TextChanged += (_, _) => Reset();
            Body.Children.AddRange(new Control[]
            {
                Ui.Section("인물 탐색", _search, sorts.View, affiliations.View), _pager.View, _rows.View,
            });
        }

        /// <inheritdoc />
        internal override string Name => "인물";

        /// <inheritdoc />
        internal override string Glyph => "人";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            List<Cultivator> people = PeopleQuery.Select(world, _sort, _affiliation, _search.Text ?? string.Empty);
            _pager.SetTotal(people.Count, "명");
            List<RowModel> rows = new();
            for (int index = _pager.Start; index < people.Count && rows.Count < _pager.PageSize; index++)
            {
                Cultivator person = people[index];
                string value = _sort == 0 ? person.RealmText : Fmt.Short(PeopleQuery.Value(world, person, _sort));
                rows.Add(Rows.Cultivator(world, person, index + 1, value, s_sorts[_sort]));
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
