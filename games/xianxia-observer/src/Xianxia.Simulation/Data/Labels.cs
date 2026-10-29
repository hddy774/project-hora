// 열거형의 한국어 표시 이름. 화면과 로그 문장이 같은 이름을 쓰도록 한곳에 모은다.
namespace Xianxia.Simulation.Data
{
    /// <summary>열거형 → 한국어 이름 변환.</summary>
    public static class Labels
    {
        private static readonly string[] s_elements = { "금", "목", "수", "화", "토", "뇌", "빙", "풍" };
        private static readonly string[] s_personalities = { "정파", "마도", "중립", "은둔", "패도" };
        private static readonly string[] s_arts = { "없음", "연단", "연기", "진법", "부록" };
        private static readonly string[] s_ranks = { "잡역제자", "외문제자", "내문제자", "직전제자", "장로", "대장로", "종주" };
        private static readonly string[] s_regionKinds = { "중심지", "주변", "접경지" };
        private static readonly string[] s_eras = { "평온", "영기 부흥", "말법 시대", "혈월", "만수 난동" };

        private static readonly string[] s_actions =
        {
            "수련", "탐색", "거래", "기예", "비무", "임무", "이동", "지도", "요양",
        };

        private static readonly string[] s_pills = { "회복단", "파경단", "청심단", "연수단" };
        private static readonly string[] s_talismans = { "공격부", "피뢰부" };

        private static readonly string[] s_logKinds =
        {
            "돌파", "천겁", "결투", "비무", "부상", "종문", "인연", "기연", "기예", "거래",
            "전쟁", "대회", "비경", "수조", "윤회", "시대", "비승", "호칭", "천기",
        };

        private static readonly string[] s_chronicleKinds =
        {
            "세계", "시대", "경지", "비승", "종주", "전쟁", "대회", "비경", "수조", "경매",
            "배신", "기록", "좌화", "천기", "시설",
        };

        private static readonly string[] s_talentGrades = { "범재", "양호", "우수", "천재", "절세" };
        private static readonly string[] s_artifactGrades = { "없음", "범품", "황품", "현품", "지품", "천품" };

        /// <summary>영근 속성 이름.</summary>
        /// <param name="element">속성.</param>
        /// <returns>한 글자 이름.</returns>
        public static string Of(Element element)
        {
            return s_elements[(int)element];
        }

        /// <summary>성격 이름.</summary>
        /// <param name="personality">성격.</param>
        /// <returns>이름.</returns>
        public static string Of(Personality personality)
        {
            return s_personalities[(int)personality];
        }

        /// <summary>기예 이름.</summary>
        /// <param name="art">기예.</param>
        /// <returns>이름.</returns>
        public static string Of(ArtKind art)
        {
            return s_arts[(int)art];
        }

        /// <summary>직위 이름.</summary>
        /// <param name="rank">직위.</param>
        /// <returns>이름.</returns>
        public static string Of(SectRank rank)
        {
            return s_ranks[(int)rank];
        }

        /// <summary>지역 종류 이름.</summary>
        /// <param name="kind">종류.</param>
        /// <returns>이름.</returns>
        public static string Of(RegionKind kind)
        {
            return s_regionKinds[(int)kind];
        }

        /// <summary>시대 이름.</summary>
        /// <param name="era">시대.</param>
        /// <returns>이름.</returns>
        public static string Of(EraKind era)
        {
            return s_eras[(int)era];
        }

        /// <summary>행동 이름.</summary>
        /// <param name="action">행동.</param>
        /// <returns>이름.</returns>
        public static string Of(CultivatorAction action)
        {
            return s_actions[(int)action];
        }

        /// <summary>단약 이름.</summary>
        /// <param name="pill">단약.</param>
        /// <returns>이름.</returns>
        public static string Of(PillKind pill)
        {
            return s_pills[(int)pill];
        }

        /// <summary>부적 이름.</summary>
        /// <param name="talisman">부적.</param>
        /// <returns>이름.</returns>
        public static string Of(TalismanKind talisman)
        {
            return s_talismans[(int)talisman];
        }

        /// <summary>로그 분류 이름.</summary>
        /// <param name="kind">분류.</param>
        /// <returns>이름.</returns>
        public static string Of(LogKind kind)
        {
            return s_logKinds[(int)kind];
        }

        /// <summary>연대기 분류 이름.</summary>
        /// <param name="kind">분류.</param>
        /// <returns>이름.</returns>
        public static string Of(ChronicleKind kind)
        {
            return s_chronicleKinds[(int)kind];
        }

        /// <summary>사망 원인 설명.</summary>
        /// <param name="cause">원인.</param>
        /// <returns>설명.</returns>
        public static string Of(DeathCause cause)
        {
            return cause == DeathCause.Collapse ? "경지 하락으로 수명 붕괴" : "수명 다함";
        }

        /// <summary>재능 등급 이름(1~5).</summary>
        /// <param name="grade">등급.</param>
        /// <returns>이름.</returns>
        public static string TalentGrade(int grade)
        {
            return s_talentGrades[System.Math.Clamp(grade, 1, 5) - 1];
        }

        /// <summary>법보 등급 이름(0~5).</summary>
        /// <param name="grade">등급.</param>
        /// <returns>이름.</returns>
        public static string ArtifactGrade(int grade)
        {
            return s_artifactGrades[System.Math.Clamp(grade, 0, 5)];
        }

        /// <summary>로그 분류 개수.</summary>
        public static int LogKindCount => s_logKinds.Length;

        /// <summary>연대기 분류 개수.</summary>
        public static int ChronicleKindCount => s_chronicleKinds.Length;
    }
}
