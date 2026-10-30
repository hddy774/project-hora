// 호칭(칭호) 정의. 수선자가 생애 중 업적으로 얻으며 업적 점수와 화면 표시에 쓰인다.
using System.Collections.Generic;

namespace Xianxia.Simulation.Data
{
    /// <summary>호칭 번호. 저장 파일에 정수로 기록되므로 끝에만 추가한다.</summary>
    public enum TitleId
    {
        /// <summary>불패: 10연승.</summary>
        Undefeated = 0,

        /// <summary>백전노장: 한 생에서 100전.</summary>
        Veteran = 1,

        /// <summary>역천자: 대경지가 더 높은 상대를 이김.</summary>
        Defier = 2,

        /// <summary>뇌겁을 넘은 자: 천겁 2회 통과.</summary>
        ThunderSurvivor = 3,

        /// <summary>대비무 우승.</summary>
        TournamentChampion = 4,

        /// <summary>대비무 준우승.</summary>
        TournamentRunnerUp = 5,

        /// <summary>전쟁 영웅: 전쟁 공훈 1위.</summary>
        WarHero = 6,

        /// <summary>대표 결투 승자.</summary>
        Champion = 7,

        /// <summary>비경의 주인: 비경 최대 수혜자.</summary>
        RealmConqueror = 8,

        /// <summary>수조 사냥꾼: 수조 격퇴 공훈 1위.</summary>
        BeastSlayer = 9,

        /// <summary>기예 종사: 기예 숙련 90 이상.</summary>
        GrandMaster = 10,

        /// <summary>기연아: 기연 3회.</summary>
        Fortunate = 11,

        /// <summary>전생 각성자.</summary>
        Awakened = 12,

        /// <summary>배신자: 소속을 버리고 적 종문으로.</summary>
        Traitor = 13,

        /// <summary>신동: 50세 전에 금단경.</summary>
        Prodigy = 14,

        /// <summary>일문지주: 종주에 오름.</summary>
        SectMaster = 15,

        /// <summary>장생자: 한 생에서 만 년 이상 삶.</summary>
        LongLived = 16,

        /// <summary>천명을 받은 자: 천기 개입(기연 하사)을 받음.</summary>
        Chosen = 17,
    }

    /// <summary>호칭 정의.</summary>
    /// <param name="Id">번호.</param>
    /// <param name="Name">이름.</param>
    /// <param name="Description">얻는 조건.</param>
    /// <param name="Score">업적 점수.</param>
    public sealed record TitleDefinition(TitleId Id, string Name, string Description, int Score);

    /// <summary>호칭 목록.</summary>
    public static class TitleCatalog
    {
        private static readonly TitleDefinition[] s_all =
        {
            new(TitleId.Undefeated, "불패", "10연승을 거두었다", 150),
            new(TitleId.Veteran, "백전노장", "한 생에서 100번 싸웠다", 120),
            new(TitleId.Defier, "역천자", "대경지가 더 높은 상대를 쓰러뜨렸다", 250),
            new(TitleId.ThunderSurvivor, "뇌겁을 넘은 자", "천겁을 두 번 넘었다", 300),
            new(TitleId.TournamentChampion, "대비무 우승자", "천하 대비무에서 우승했다", 500),
            new(TitleId.TournamentRunnerUp, "대비무 준우승자", "천하 대비무 결승에 올랐다", 250),
            new(TitleId.WarHero, "전쟁 영웅", "전쟁에서 가장 큰 공을 세웠다", 400),
            new(TitleId.Champion, "대표 결투 승자", "종문을 대표한 결투에서 이겼다", 300),
            new(TitleId.RealmConqueror, "비경의 주인", "비경에서 가장 많은 보물을 얻었다", 200),
            new(TitleId.BeastSlayer, "수조 사냥꾼", "수조를 막는 데 가장 큰 공을 세웠다", 250),
            new(TitleId.GrandMaster, "기예 종사", "기예 숙련이 경지에 이르렀다", 200),
            new(TitleId.Fortunate, "기연아", "세 번 기연을 만났다", 150),
            new(TitleId.Awakened, "전생 각성자", "전생의 기억을 되찾았다", 100),
            new(TitleId.Traitor, "배신자", "종문을 버리고 적에게 갔다", 0),
            new(TitleId.Prodigy, "신동", "쉰 살 전에 금단경에 올랐다", 200),
            new(TitleId.SectMaster, "일문지주", "종주에 올랐다", 400),
            new(TitleId.LongLived, "장생자", "한 생에서 만 년을 살았다", 150),
            new(TitleId.Chosen, "천명을 받은 자", "하늘이 기연을 내렸다", 50),
        };

        /// <summary>모든 호칭.</summary>
        public static IReadOnlyList<TitleDefinition> All => s_all;

        /// <summary>번호로 정의를 찾는다.</summary>
        /// <param name="id">번호.</param>
        /// <returns>정의.</returns>
        public static TitleDefinition Get(TitleId id)
        {
            return s_all[(int)id];
        }
    }
}
