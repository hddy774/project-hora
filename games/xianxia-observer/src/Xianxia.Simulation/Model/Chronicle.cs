// 연대기(세계의 주요 사건)와 화면 알림 대기열.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>연대기 항목.</summary>
    /// <param name="Sequence">일련번호.</param>
    /// <param name="Year">연도.</param>
    /// <param name="Kind">분류.</param>
    /// <param name="Text">내용.</param>
    /// <param name="SubjectId">주인공 영혼 번호(0이면 없음).</param>
    public sealed record ChronicleEntry(long Sequence, int Year, ChronicleKind Kind, string Text, int SubjectId);

    /// <summary>화면 알림(토스트). 저장하지 않는다.</summary>
    /// <param name="Sequence">일련번호.</param>
    /// <param name="Year">연도.</param>
    /// <param name="Text">내용.</param>
    /// <param name="SubjectId">관련 영혼 번호.</param>
    /// <param name="Major">연대기급 사건이면 true, 관심 인물 사건이면 false.</param>
    public sealed record Notice(long Sequence, int Year, string Text, int SubjectId, bool Major);

    /// <summary>연대기. 드문 사건만 담아 세계의 역사를 읽을 수 있게 한다.</summary>
    public sealed class Chronicle
    {
        private readonly List<Notice> _notices = new();

        /// <summary>항목(오래된 것부터).</summary>
        public List<ChronicleEntry> Entries { get; set; } = new();

        /// <summary>다음 일련번호.</summary>
        public long NextSequence { get; set; } = 1;

        /// <summary>다음 알림 번호.</summary>
        [JsonIgnore]
        public long NextNoticeSequence { get; private set; } = 1;

        /// <summary>최근 알림(최대 40).</summary>
        [JsonIgnore]
        public IReadOnlyList<Notice> Notices => _notices;

        /// <summary>연대기 항목을 추가하고 화면 알림도 낸다.</summary>
        /// <param name="year">연도.</param>
        /// <param name="kind">분류.</param>
        /// <param name="text">내용.</param>
        /// <param name="subjectId">주인공.</param>
        public void Add(int year, ChronicleKind kind, string text, int subjectId = 0)
        {
            Entries.Add(new ChronicleEntry(NextSequence, year, kind, text, subjectId));
            NextSequence++;
            if (Entries.Count > Balance.ChronicleCapacity)
            {
                Entries.RemoveAt(0);
            }

            Notify(year, text, subjectId, true);
        }

        /// <summary>연대기에 남기지 않는 알림(관심 인물 소식)을 낸다.</summary>
        /// <param name="year">연도.</param>
        /// <param name="text">내용.</param>
        /// <param name="subjectId">관련 영혼.</param>
        /// <param name="major">중요 여부.</param>
        public void Notify(int year, string text, int subjectId, bool major)
        {
            _notices.Add(new Notice(NextNoticeSequence, year, text, subjectId, major));
            NextNoticeSequence++;
            if (_notices.Count > 40)
            {
                _notices.RemoveAt(0);
            }
        }
    }
}
