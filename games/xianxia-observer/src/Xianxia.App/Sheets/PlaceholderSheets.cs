// 아직 구현하지 않은 상세 시트 자리표시. 각 시트는 docs/tasks/XO-*.md 명세대로 별도 파일에서 구현하고 이 파일에서 지운다.
using Xianxia.App.Controls;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>수선자 시트: 개요·전투력·수행·소지·인연·생애·윤회 7개 하위 탭과 관심 표시·기연 하사 (명세 XO-04, 자리표시).</summary>
    internal sealed class CultivatorSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal CultivatorSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "수선자";
            Body.Children.Add(Ui.Muted("개요·전투력·수행·소지·인연·생애·윤회 7개 하위 탭과 관심 표시·기연 하사.", 12));
            Body.Children.Add(Ui.Muted("명세 XO-04로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>종문 시트: 정보·강자·공헌·시설·역사 하위 탭 (명세 XO-05, 자리표시).</summary>
    internal sealed class SectSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal SectSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "종문";
            Body.Children.Add(Ui.Muted("정보·강자·공헌·시설·역사 하위 탭.", 12));
            Body.Children.Add(Ui.Muted("명세 XO-05로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>지역 시트: 지역 정보·시장·거주자·비경 개방/수조 소환 개입 (명세 XO-05, 자리표시).</summary>
    internal sealed class RegionSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal RegionSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "지역";
            Body.Children.Add(Ui.Muted("지역 정보·시장·거주자·비경 개방/수조 소환 개입.", 12));
            Body.Children.Add(Ui.Muted("명세 XO-05로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>천하 대비무 시트: 대진표(라운드별 경기·이변 표시) (명세 XO-06, 자리표시).</summary>
    internal sealed class TournamentSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal TournamentSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "천하 대비무";
            Body.Children.Add(Ui.Muted("대진표(라운드별 경기·이변 표시).", 12));
            Body.Children.Add(Ui.Muted("명세 XO-06로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>정마대전 시트: 전쟁 기록(전적·점령·배상·영웅·대표 결투·주요 장면) (명세 XO-06, 자리표시).</summary>
    internal sealed class WarSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal WarSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "정마대전";
            Body.Children.Add(Ui.Muted("전쟁 기록(전적·점령·배상·영웅·대표 결투·주요 장면).", 12));
            Body.Children.Add(Ui.Muted("명세 XO-06로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>설정 시트: 저장·불러오기·새 세계(시드·인구)·정보 (명세 XO-06, 자리표시).</summary>
    internal sealed class SettingsSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal SettingsSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "설정";
            Body.Children.Add(Ui.Muted("저장·불러오기·새 세계(시드·인구)·정보.", 12));
            Body.Children.Add(Ui.Muted("명세 XO-06로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }

    /// <summary>도감 시트: 경지·종문·전쟁·사건·기예·호칭·천기 설명 (명세 XO-06, 자리표시).</summary>
    internal sealed class HelpSheet : SheetBase
    {
        /// <summary>자리표시 시트를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">연결.</param>
        internal HelpSheet(INavigator navigator, SheetLink link)
            : base(navigator, link)
        {
            Title = "도감";
            Body.Children.Add(Ui.Muted("경지·종문·전쟁·사건·기예·호칭·천기 설명.", 12));
            Body.Children.Add(Ui.Muted("명세 XO-06로 구현 예정입니다.", 11));
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
        }
    }
}
