// 아직 구현하지 않은 탭 자리표시. 각 탭은 docs/tasks/XO-*.md 명세대로 별도 파일에서 구현하고 이 파일에서 지운다.
using Xianxia.App.Controls;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>종문 탭: 두 종문 상세 카드·시설·직위 정원·산수 요약·사망 원인 (명세 XO-01, 자리표시).</summary>
    internal sealed class SectsPage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal SectsPage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("종문 (준비 중)", Ui.Muted("종문 탭: 두 종문 상세 카드·시설·직위 정원·산수 요약·사망 원인.", 12),
                Ui.Muted("명세 XO-01로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "종문";

        /// <inheritdoc />
        internal override string Glyph => "門";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>인물 탭: 정렬 8종·소속 필터·이름 검색·20명 쪽 넘김 (명세 XO-01, 자리표시).</summary>
    internal sealed class PeoplePage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal PeoplePage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("인물 (준비 중)", Ui.Muted("인물 탭: 정렬 8종·소속 필터·이름 검색·20명 쪽 넘김.", 12),
                Ui.Muted("명세 XO-01로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "인물";

        /// <inheritdoc />
        internal override string Glyph => "人";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>사기 탭: 연대기·세계 기록·전쟁사·대비무·비승록·경매 (명세 XO-02, 자리표시).</summary>
    internal sealed class HistoryPage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal HistoryPage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("사기 (준비 중)", Ui.Muted("사기 탭: 연대기·세계 기록·전쟁사·대비무·비승록·경매.", 12),
                Ui.Muted("명세 XO-02로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "사기";

        /// <inheritdoc />
        internal override string Glyph => "史";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>업적 탭: 역대 최고·5생 합계·종합 순위 구간·호칭 도감 (명세 XO-02, 자리표시).</summary>
    internal sealed class LegacyPage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal LegacyPage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("업적 (준비 중)", Ui.Muted("업적 탭: 역대 최고·5생 합계·종합 순위 구간·호칭 도감.", 12),
                Ui.Muted("명세 XO-02로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "업적";

        /// <inheritdoc />
        internal override string Glyph => "業";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>현황 탭: 경지 분포·추이 그래프·영석 장부·사건 통계 (명세 XO-03, 자리표시).</summary>
    internal sealed class StatsPage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal StatsPage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("현황 (준비 중)", Ui.Muted("현황 탭: 경지 분포·추이 그래프·영석 장부·사건 통계.", 12),
                Ui.Muted("명세 XO-03로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "현황";

        /// <inheritdoc />
        internal override string Glyph => "統";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>기록 탭: 분류 필터·관심 인물만·40건 쪽 넘김 (명세 XO-03, 자리표시).</summary>
    internal sealed class LogPage : PageBase
    {
        /// <summary>자리표시 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal LogPage(INavigator navigator)
            : base(navigator)
        {
            Body.Children.Add(Ui.Section("기록 (준비 중)", Ui.Muted("기록 탭: 분류 필터·관심 인물만·40건 쪽 넘김.", 12),
                Ui.Muted("명세 XO-03로 구현 예정입니다.", 11)));
        }

        /// <inheritdoc />
        internal override string Name => "기록";

        /// <inheritdoc />
        internal override string Glyph => "錄";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }
}
