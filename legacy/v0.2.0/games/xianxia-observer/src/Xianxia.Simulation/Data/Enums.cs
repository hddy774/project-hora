// 시뮬레이션 전역에서 쓰는 열거형. 정수 값은 저장 파일에 기록되므로 순서를 바꾸지 않고 끝에만 추가한다.
namespace Xianxia.Simulation.Data
{
    /// <summary>영근 속성. 앞의 다섯은 오행(상극 관계), 뒤의 셋은 드문 변이 영근이다.</summary>
    public enum Element
    {
        /// <summary>금(金).</summary>
        Metal = 0,

        /// <summary>목(木).</summary>
        Wood = 1,

        /// <summary>수(水).</summary>
        Water = 2,

        /// <summary>화(火).</summary>
        Fire = 3,

        /// <summary>토(土).</summary>
        Earth = 4,

        /// <summary>뇌(雷) 변이 영근.</summary>
        Thunder = 5,

        /// <summary>빙(氷) 변이 영근.</summary>
        Ice = 6,

        /// <summary>풍(風) 변이 영근.</summary>
        Wind = 7,
    }

    /// <summary>성격. 행동 선택 가중치와 종문 적합도에 쓰인다.</summary>
    public enum Personality
    {
        /// <summary>정파: 임무·수련 중시, 정도 종문에 잘 맞는다.</summary>
        Righteous = 0,

        /// <summary>마도: 싸움과 약탈을 즐기고 마도 종문에 잘 맞는다.</summary>
        Demonic = 1,

        /// <summary>중립: 거래·탐색을 즐긴다.</summary>
        Neutral = 2,

        /// <summary>은둔: 거의 수련만 하고 싸움을 피한다.</summary>
        Hermit = 3,

        /// <summary>패도: 힘을 숭상해 비무와 결투가 잦다.</summary>
        Domineering = 4,
    }

    /// <summary>기예(생산 기술).</summary>
    public enum ArtKind
    {
        /// <summary>기예 없음.</summary>
        None = 0,

        /// <summary>연단: 영초로 단약을 만든다.</summary>
        Alchemy = 1,

        /// <summary>연기: 영철로 법보를 만든다.</summary>
        Refining = 2,

        /// <summary>진법: 전쟁·수조 방어와 영맥진을 돕는다.</summary>
        Formation = 3,

        /// <summary>부록: 부적을 만든다.</summary>
        Talisman = 4,
    }

    /// <summary>종문 직위. 값이 클수록 높다.</summary>
    public enum SectRank
    {
        /// <summary>직위 없는 잡역 제자(명부 정원 밖).</summary>
        None = 0,

        /// <summary>외문제자.</summary>
        Outer = 1,

        /// <summary>내문제자.</summary>
        Inner = 2,

        /// <summary>직전제자.</summary>
        Direct = 3,

        /// <summary>장로.</summary>
        Elder = 4,

        /// <summary>대장로.</summary>
        GrandElder = 5,

        /// <summary>종주.</summary>
        Leader = 6,
    }

    /// <summary>지역 종류.</summary>
    public enum RegionKind
    {
        /// <summary>종문 본산(중심지).</summary>
        Center = 0,

        /// <summary>종문 주변 영지.</summary>
        Periphery = 1,

        /// <summary>두 종문 사이의 접경지(전쟁 구역).</summary>
        Border = 2,
    }

    /// <summary>천지 대세(시대). 세계 전체의 수련 속도와 사건 빈도를 바꾼다.</summary>
    public enum EraKind
    {
        /// <summary>평온.</summary>
        Calm = 0,

        /// <summary>영기 부흥: 수련이 빨라지고 비경이 잦다.</summary>
        Resurgence = 1,

        /// <summary>말법 시대: 수련과 돌파가 어렵다.</summary>
        Decline = 2,

        /// <summary>혈월: 마도가 강해지고 수조가 잦다.</summary>
        BloodMoon = 3,

        /// <summary>만수 난동: 수조가 매우 잦다.</summary>
        BeastFrenzy = 4,
    }

    /// <summary>한 해 동안 수선자가 하는 행동.</summary>
    public enum CultivatorAction
    {
        /// <summary>수련.</summary>
        Cultivate = 0,

        /// <summary>탐색(자원·기연).</summary>
        Explore = 1,

        /// <summary>거래.</summary>
        Trade = 2,

        /// <summary>기예 제작.</summary>
        Craft = 3,

        /// <summary>비무·결투.</summary>
        Spar = 4,

        /// <summary>종문 임무.</summary>
        Mission = 5,

        /// <summary>지역 이동.</summary>
        Travel = 6,

        /// <summary>제자 지도.</summary>
        Teach = 7,

        /// <summary>요양(부상 회복).</summary>
        Recover = 8,
    }

    /// <summary>사망 원인.</summary>
    public enum DeathCause
    {
        /// <summary>수명이 다함.</summary>
        Lifespan = 0,

        /// <summary>경지가 떨어져 남은 수명이 사라짐.</summary>
        Collapse = 1,
    }

    /// <summary>단약 종류.</summary>
    public enum PillKind
    {
        /// <summary>회복단: 부상 치료.</summary>
        Healing = 0,

        /// <summary>파경단: 대경지 돌파 보조.</summary>
        Breakthrough = 1,

        /// <summary>청심단: 도심 회복.</summary>
        Clarity = 2,

        /// <summary>연수단: 수명 연장.</summary>
        Longevity = 3,
    }

    /// <summary>부적 종류.</summary>
    public enum TalismanKind
    {
        /// <summary>공격부: 다음 전투 전투력 증가.</summary>
        Attack = 0,

        /// <summary>피뢰부: 천겁 통과 확률 증가.</summary>
        Thunder = 1,
    }

    /// <summary>사건 기록(로그) 분류.</summary>
    public enum LogKind
    {
        /// <summary>돌파.</summary>
        Breakthrough = 0,

        /// <summary>천겁.</summary>
        Tribulation = 1,

        /// <summary>결투.</summary>
        Battle = 2,

        /// <summary>비무.</summary>
        Spar = 3,

        /// <summary>부상·경지 하락.</summary>
        Injury = 4,

        /// <summary>종문.</summary>
        Sect = 5,

        /// <summary>인연(사제·도려·숙적).</summary>
        Relation = 6,

        /// <summary>기연.</summary>
        Fortune = 7,

        /// <summary>기예.</summary>
        Craft = 8,

        /// <summary>거래·경매.</summary>
        Trade = 9,

        /// <summary>전쟁.</summary>
        War = 10,

        /// <summary>대비무 대회.</summary>
        Tournament = 11,

        /// <summary>비경.</summary>
        SecretRealm = 12,

        /// <summary>수조.</summary>
        BeastTide = 13,

        /// <summary>윤회·좌화.</summary>
        Rebirth = 14,

        /// <summary>시대 변화.</summary>
        Era = 15,

        /// <summary>비승.</summary>
        Ascension = 16,

        /// <summary>호칭 획득.</summary>
        Title = 17,

        /// <summary>천기 개입.</summary>
        Intervention = 18,
    }

    /// <summary>연대기(주요 사건) 분류.</summary>
    public enum ChronicleKind
    {
        /// <summary>세계 개벽 등 세계 사건.</summary>
        World = 0,

        /// <summary>시대 변화.</summary>
        Era = 1,

        /// <summary>경지 최초 도달.</summary>
        FirstRealm = 2,

        /// <summary>비승.</summary>
        Ascension = 3,

        /// <summary>종주 교체.</summary>
        Leader = 4,

        /// <summary>전쟁.</summary>
        War = 5,

        /// <summary>대비무 대회.</summary>
        Tournament = 6,

        /// <summary>비경.</summary>
        SecretRealm = 7,

        /// <summary>수조.</summary>
        BeastTide = 8,

        /// <summary>경매.</summary>
        Auction = 9,

        /// <summary>배신·귀순.</summary>
        Defection = 10,

        /// <summary>세계 기록 경신.</summary>
        Record = 11,

        /// <summary>고수의 좌화(사망).</summary>
        Death = 12,

        /// <summary>천기 개입.</summary>
        Intervention = 13,

        /// <summary>종문 시설.</summary>
        Facility = 14,
    }
}
