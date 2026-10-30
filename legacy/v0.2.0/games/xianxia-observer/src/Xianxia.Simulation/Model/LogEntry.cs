// 비무의 문자열 조립을 표시·저장 시점까지 미룬다. 버려지는 로그의 문자열 할당을 줄이며 저장 형식은 유지한다.
using System;
namespace Xianxia.Simulation.Model
{
    /// <summary>사건 한 줄. 지연 문장의 이름도 사건 당시 문자열을 사용한다.</summary>
    /// <param name="Sequence">전체 일련번호.</param>
    /// <param name="Year">연도.</param>
    /// <param name="Kind">분류.</param>
    /// <param name="Text">내용.</param>
    /// <param name="SubjectId">주인공.</param>
    /// <param name="OtherId">상대.</param>
    public sealed record LogEntry(long Sequence, int Year, Data.LogKind Kind, string Text, int SubjectId, int OtherId)
    {
        private string? _text = Text;
        private string? _winnerName;
        private string? _loserName;
        private bool _crossSect;

        /// <summary>표시·저장할 사건 문장. 최초 접근 시 한 번 조립한다.</summary>
        public string Text
        {
            get => _text ??= (_crossSect ? "[대비무] " : "[비무] ") + _winnerName + "이(가) "
                + _loserName + "에게 이겼다.";
            init => _text = value;
        }

        /// <summary>지연 캐시의 생성 여부가 아닌 공개 사건 값으로 비교한다.</summary>
        /// <param name="other">다른 사건.</param>
        /// <returns>같은 사건 값이면 true.</returns>
        public bool Equals(LogEntry? other)
        {
            return other is not null && Sequence == other.Sequence && Year == other.Year && Kind == other.Kind
                && Text == other.Text && SubjectId == other.SubjectId && OtherId == other.OtherId;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Sequence, Year, Kind, Text, SubjectId, OtherId);
        }

        /// <summary>비무 이름 스냅샷을 지정한다. 객체 참조나 난수를 보관하지 않는다.</summary>
        /// <param name="winnerName">승자 이름.</param>
        /// <param name="loserName">패자 이름.</param>
        /// <param name="crossSect">종문 간 비무인가.</param>
        internal void DeferFriendly(string winnerName, string loserName, bool crossSect)
        {
            _winnerName = winnerName;
            _loserName = loserName;
            _crossSect = crossSect;
            _text = null;
        }
    }
}
