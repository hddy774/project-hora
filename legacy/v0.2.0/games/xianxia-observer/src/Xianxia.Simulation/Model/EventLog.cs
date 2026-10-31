// 사건 기록(최신 600건 링 버퍼)과 분류별 누적 건수.
// 원본은 unshift/push를 섞고 앞에서 잘라 내 최신 돌파·전투 기록이 사라졌다. 여기서는 순서를 하나로 고정한다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>
    /// 사건 기록. [성능] 고정 크기 배열 링 버퍼라 기록 추가가 O(1)이다.
    /// 화면은 <see cref="CopyNewestFirst"/>로 최신순 사본을 받아 그린다.
    /// </summary>
    public sealed class EventLog
    {
        private LogEntry?[] _buffer = new LogEntry?[Balance.LogCapacity];
        private int _start;
        private int _count;

        /// <summary>다음 일련번호.</summary>
        public long NextSequence { get; set; } = 1;

        /// <summary>분류별 누적 건수(잘려 나간 기록 포함). 인덱스는 <see cref="LogKind"/>.</summary>
        public List<long> Counts { get; set; } = new(new long[Labels.LogKindCount]);

        /// <summary>저장용: 오래된 것부터 나열한 현재 기록. 설정하면 버퍼를 다시 채운다.</summary>
        public List<LogEntry> Entries
        {
            get
            {
                List<LogEntry> list = new(_count);
                for (int index = 0; index < _count; index++)
                {
                    list.Add(_buffer[(_start + index) % _buffer.Length]!);
                }

                return list;
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _buffer = new LogEntry?[Balance.LogCapacity];
                _start = 0;
                _count = 0;
                int skip = Math.Max(0, value.Count - _buffer.Length);
                for (int index = skip; index < value.Count; index++)
                {
                    Push(value[index]);
                }
            }
        }

        /// <summary>현재 보관 중인 기록 수.</summary>
        public int Count => _count;

        /// <summary>기록을 추가하고 분류 건수를 올린다.</summary>
        /// <param name="year">연도.</param>
        /// <param name="kind">분류.</param>
        /// <param name="text">내용.</param>
        /// <param name="subjectId">주인공.</param>
        /// <param name="otherId">상대.</param>
        /// <returns>추가된 기록.</returns>
        public LogEntry Add(int year, LogKind kind, string text, int subjectId = 0, int otherId = 0)
        {
            LogEntry entry = new(NextSequence, year, kind, text, subjectId, otherId);
            NextSequence++;
            Counts[(int)kind]++;
            Push(entry);
            return entry;
        }

        /// <summary>빈번한 비무 문장은 읽거나 저장할 때 만든다. 이름은 사건 당시 값을 보존한다.</summary>
        /// <param name="year">연도.</param>
        /// <param name="winner">승자.</param>
        /// <param name="loser">패자.</param>
        /// <param name="crossSect">종문 간 비무인가.</param>
        /// <returns>추가된 기록.</returns>
        public LogEntry AddFriendly(int year, Cultivator winner, Cultivator loser, bool crossSect)
        {
            LogEntry entry = Add(year, LogKind.Spar, string.Empty, winner.Id, loser.Id);
            entry.DeferFriendly(winner.Name, loser.Name, crossSect);
            return entry;
        }

        /// <summary>기록 없이 건수만 올린다(잦은 사건을 표본만 남길 때).</summary>
        /// <param name="kind">분류.</param>
        public void CountOnly(LogKind kind)
        {
            Counts[(int)kind]++;
        }

        /// <summary>최신순 사본을 만든다.</summary>
        /// <param name="limit">최대 개수.</param>
        /// <param name="filter">조건(없으면 전체).</param>
        /// <returns>최신 기록이 앞에 오는 목록.</returns>
        public List<LogEntry> CopyNewestFirst(int limit, Func<LogEntry, bool>? filter = null)
        {
            List<LogEntry> list = new(Math.Min(limit, _count));
            for (int offset = _count - 1; offset >= 0 && list.Count < limit; offset--)
            {
                LogEntry entry = _buffer[(_start + offset) % _buffer.Length]!;
                if (filter is null || filter(entry))
                {
                    list.Add(entry);
                }
            }

            return list;
        }

        private void Push(LogEntry entry)
        {
            if (_count < _buffer.Length)
            {
                _buffer[(_start + _count) % _buffer.Length] = entry;
                _count++;
                return;
            }

            // 가득 차면 가장 오래된 칸을 덮어쓰고 시작점을 한 칸 민다.
            _buffer[_start] = entry;
            _start = (_start + 1) % _buffer.Length;
        }
    }
}
