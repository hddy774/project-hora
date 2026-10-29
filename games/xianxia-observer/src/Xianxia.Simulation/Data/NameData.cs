// 수선자 이름·도호·비경 이름에 쓰는 음절 표. 원본 음절(NA·NB)을 포함해 중복 이름이 거의 없도록 넓혔다.
using System.Collections.Generic;

namespace Xianxia.Simulation.Data
{
    /// <summary>이름 생성용 음절 표.</summary>
    public static class NameData
    {
        /// <summary>한 글자 성.</summary>
        public static IReadOnlyList<string> Surnames { get; } = new[]
        {
            "청", "운", "진", "목", "하", "월", "현", "염", "설", "린", "풍", "연", "도", "백", "흑", "금",
            "옥", "소", "류", "명", "천", "혁", "비", "무", "신", "한", "송", "엽", "강", "임", "주", "단",
        };

        /// <summary>두 글자 복성. 일부 수선자만 쓴다.</summary>
        public static IReadOnlyList<string> CompoundSurnames { get; } = new[]
        {
            "남궁", "제갈", "독고", "모용", "황보", "사마", "상관", "구양", "동방", "서문",
        };

        /// <summary>이름 앞 음절(원본 NA 계승).</summary>
        public static IReadOnlyList<string> GivenFirst { get; } = new[]
        {
            "운", "하", "진", "월", "검", "영", "화", "천", "린", "명", "아", "호", "야", "도", "성", "우",
            "설", "풍", "란", "휘", "혁", "봉", "랑", "백", "기", "청", "현", "소", "무", "연", "향", "수",
        };

        /// <summary>이름 뒤 음절(원본 NB 계승).</summary>
        public static IReadOnlyList<string> GivenSecond { get; } = new[]
        {
            "운", "하", "진", "월", "영", "화", "천", "린", "명", "아", "호", "도", "성", "우", "설", "풍",
            "란", "휘", "혁", "봉", "랑", "기", "결", "한", "온", "율", "겸", "담", "원", "경", "인", "후",
        };

        /// <summary>도호 앞 글자(도사 이름 느낌).</summary>
        public static IReadOnlyList<string> DaoFirst { get; } = new[]
        {
            "청", "허", "현", "태", "무", "자", "옥", "운", "적", "송", "학", "명", "은", "선", "공", "정",
        };

        /// <summary>도호 둘째 글자.</summary>
        public static IReadOnlyList<string> DaoSecond { get; } = new[]
        {
            "허", "진", "양", "현", "명", "원", "광", "운", "하", "일", "정", "소", "심", "곡", "봉", "천",
        };

        /// <summary>비경 이름 앞부분.</summary>
        public static IReadOnlyList<string> SecretRealmPrefixes { get; } = new[]
        {
            "태허", "천독", "상고", "구유", "만검", "현빙", "적염", "청명", "무량", "혼돈", "낙성", "용궁",
        };

        /// <summary>비경 이름 뒷부분.</summary>
        public static IReadOnlyList<string> SecretRealmSuffixes { get; } = new[]
        {
            "비경", "고적", "전장", "동천", "선부", "밀림", "지궁", "검총",
        };

        /// <summary>수조를 이끄는 영수 이름.</summary>
        public static IReadOnlyList<string> BeastNames { get; } = new[]
        {
            "적염랑왕", "현빙교룡", "만독지주", "금시대붕", "혈안웅사", "구두사왕", "뇌운표범", "흑린망",
        };
    }
}
