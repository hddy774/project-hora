// 화면 사이 이동: 목록의 항목을 누르면 상세 시트(수선자·종문·지역·대회·전쟁)를 연다.
using Xianxia.App.Services;

namespace Xianxia.App.Views
{
    /// <summary>상세 시트 종류.</summary>
    internal enum LinkKind
    {
        /// <summary>이동하지 않음.</summary>
        None = 0,

        /// <summary>수선자(영혼 번호).</summary>
        Cultivator = 1,

        /// <summary>종문(종문 번호).</summary>
        Sect = 2,

        /// <summary>지역(지역 번호).</summary>
        Region = 3,

        /// <summary>대비무 대회(기록 번호).</summary>
        Tournament = 4,

        /// <summary>전쟁(전쟁 번호).</summary>
        War = 5,

        /// <summary>설정·저장.</summary>
        Settings = 6,

        /// <summary>도감(도움말).</summary>
        Help = 7,
    }

    /// <summary>상세 시트로 가는 연결.</summary>
    /// <param name="Kind">종류.</param>
    /// <param name="Id">대상 번호.</param>
    internal readonly record struct SheetLink(LinkKind Kind, int Id)
    {
        /// <summary>연결 없음.</summary>
        internal static SheetLink None => default;

        /// <summary>연결이 있는가.</summary>
        internal bool IsSet => Kind != LinkKind.None;

        /// <summary>수선자 시트.</summary>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>연결.</returns>
        internal static SheetLink ToCultivator(int soulId)
        {
            return soulId == 0 ? None : new SheetLink(LinkKind.Cultivator, soulId);
        }

        /// <summary>종문 시트.</summary>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>연결.</returns>
        internal static SheetLink ToSect(int sectId)
        {
            return sectId == 0 ? None : new SheetLink(LinkKind.Sect, sectId);
        }

        /// <summary>지역 시트.</summary>
        /// <param name="regionId">지역 번호.</param>
        /// <returns>연결.</returns>
        internal static SheetLink ToRegion(int regionId)
        {
            return new SheetLink(LinkKind.Region, regionId);
        }

        /// <summary>대회 시트.</summary>
        /// <param name="number">대회 회차.</param>
        /// <returns>연결.</returns>
        internal static SheetLink ToTournament(int number)
        {
            return new SheetLink(LinkKind.Tournament, number);
        }

        /// <summary>전쟁 시트.</summary>
        /// <param name="number">전쟁 회차.</param>
        /// <returns>연결.</returns>
        internal static SheetLink ToWar(int number)
        {
            return new SheetLink(LinkKind.War, number);
        }
    }

    /// <summary>화면 이동과 다시 그리기 요청을 받는 쪽(메인 화면).</summary>
    internal interface INavigator
    {
        /// <summary>게임 세션.</summary>
        public GameSession Session { get; }

        /// <summary>상세 시트를 연다(시트 위에서 열면 쌓인다).</summary>
        /// <param name="link">연결.</param>
        public void Open(SheetLink link);

        /// <summary>화면을 곧바로 다시 그린다(선택·정렬이 바뀐 뒤).</summary>
        public void Invalidate();

        /// <summary>짧은 안내를 띄운다.</summary>
        /// <param name="text">내용.</param>
        public void Toast(string text);
    }
}
