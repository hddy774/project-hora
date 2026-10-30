// 연대기(세계의 주요 사건)와 화면 알림 대기열.
using System;
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
    public sealed record Notice(long Sequence, int Year, string Text, int SubjectId, bool Major)
    {
        private string _text = Text;
        private LogEntry? _entry;

        /// <summary>알림 본문. 빈번한 비무는 공유된 로그 문장을 읽을 때 만든다.</summary>
        public string Text
        {
            get => _entry?.Text ?? _text;
            init
            {
                _text = value;
                _entry = null;
            }
        }

        /// <summary>알림 캐시와 무관하게 공개 값으로 비교한다.</summary>
        /// <param name="other">다른 알림.</param>
        /// <returns>같은 값이면 true.</returns>
        public bool Equals(Notice? other)
        {
            return other is not null && Sequence == other.Sequence && Year == other.Year && Text == other.Text
                && SubjectId == other.SubjectId && Major == other.Major;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Sequence, Year, Text, SubjectId, Major);
        }

        /// <summary>지연 생성할 로그를 연결한다(저장하지 않음).</summary>
        /// <param name="entry">사건 스냅샷.</param>
        internal void UseLog(LogEntry entry)
        {
            _entry = entry;
        }
    }

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
            PushNotice(new Notice(NextNoticeSequence, year, text, subjectId, major));
        }

        /// <summary>문장 조립 없이 비무 알림을 보존한다.</summary>
        /// <param name="entry">로그.</param>
        /// <param name="subjectId">관심 영혼.</param>
        internal void NotifyLog(LogEntry entry, int subjectId)
        {
            Notice notice = new(NextNoticeSequence, entry.Year, string.Empty, subjectId, false);
            notice.UseLog(entry);
            PushNotice(notice);
        }

        private void PushNotice(Notice notice)
        {
            _notices.Add(notice);
            NextNoticeSequence++;
            if (_notices.Count > 40)
            {
                _notices.RemoveAt(0);
            }
        }
    }
}
