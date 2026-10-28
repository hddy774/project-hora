// 하단 탭 화면과 상세 시트의 공통 틀. 화면은 처음에 한 번 만들고, 갱신 때는 세계를 읽어 글자만 바꾼다.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Xianxia.App.Controls;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>하단 탭 화면.</summary>
    internal abstract class PageBase
    {
        /// <summary>화면 틀을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        protected PageBase(INavigator navigator)
        {
            Navigator = navigator;
            Body = new StackPanel { Spacing = 8, Margin = new Thickness(Ui.Gutter, 8, Ui.Gutter, 16) };
            View = new ScrollViewer
            {
                Content = Body,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
        }

        /// <summary>화면 요소(세로 스크롤).</summary>
        internal ScrollViewer View { get; }

        /// <summary>탭 이름.</summary>
        internal abstract string Name { get; }

        /// <summary>탭 표식 한자.</summary>
        internal abstract string Glyph { get; }

        /// <summary>내용 묶음.</summary>
        protected StackPanel Body { get; }

        /// <summary>화면 이동.</summary>
        protected INavigator Navigator { get; }

        /// <summary>세계를 읽어 화면을 고친다(실행기 잠금 안에서 호출).</summary>
        /// <param name="world">세계.</param>
        internal abstract void Refresh(World world);
    }

    /// <summary>상세 시트(수선자·종문·지역 등). 시트 위에 다른 시트를 쌓을 수 있다.</summary>
    internal abstract class SheetBase
    {
        /// <summary>시트 틀을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">이 시트의 연결.</param>
        protected SheetBase(INavigator navigator, SheetLink link)
        {
            Navigator = navigator;
            Link = link;
            Body = new StackPanel { Spacing = 8 };
        }

        /// <summary>이 시트의 연결.</summary>
        internal SheetLink Link { get; }

        /// <summary>제목(갱신 때 바뀔 수 있음).</summary>
        internal string Title { get; set; } = string.Empty;

        /// <summary>하위 탭 막대(없으면 null).</summary>
        internal Control? Tabs { get; set; }

        /// <summary>내용 묶음.</summary>
        internal StackPanel Body { get; }

        /// <summary>화면 이동.</summary>
        protected INavigator Navigator { get; }

        /// <summary>세계를 읽어 시트를 고친다(실행기 잠금 안에서 호출).</summary>
        /// <param name="world">세계.</param>
        internal abstract void Refresh(World world);
    }
}
