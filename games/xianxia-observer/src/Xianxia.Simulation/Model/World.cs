// 세계 전체 상태. 이 객체 하나를 저장하면 같은 시드의 진행을 그대로 이어 갈 수 있다.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Core;

namespace Xianxia.Simulation.Model
{
    /// <summary>새 세계 설정.</summary>
    public sealed class WorldSettings
    {
        /// <summary>시드.</summary>
        public ulong Seed { get; set; } = 20261028;

        /// <summary>인구(영혼 수).</summary>
        public int Population { get; set; } = Data.Balance.DefaultPopulation;

        /// <summary>세계 이름.</summary>
        public string Name { get; set; } = "선협 천하";
    }

    /// <summary>세계 누적 집계.</summary>
    public sealed class WorldTotals
    {
        /// <summary>수명이 다해 좌화한 수.</summary>
        public long NaturalDeaths { get; set; }

        /// <summary>경지 하락으로 수명이 무너진 수.</summary>
        public long CollapseDeaths { get; set; }

        /// <summary>결투(전쟁·숙적·비경 포함) 수.</summary>
        public long Battles { get; set; }

        /// <summary>비무(친선·대비무) 수.</summary>
        public long Spars { get; set; }

        /// <summary>종문 간 대비무(휴전 중) 수.</summary>
        public long CrossSectSpars { get; set; }

        /// <summary>대경지 돌파 수.</summary>
        public long MajorBreakthroughs { get; set; }

        /// <summary>천겁 수.</summary>
        public long Tribulations { get; set; }

        /// <summary>비승 수.</summary>
        public long Ascensions { get; set; }

        /// <summary>배신·귀순 수.</summary>
        public long Defections { get; set; }
    }

    /// <summary>세계.</summary>
    public sealed class World
    {
        /// <summary>로그 표본 전용 난수 스트림. 세계 규칙 난수와 분리한다.</summary>
        public const ulong LogRandomStream = 0x4C4F47;

        private readonly Dictionary<int, Cultivator> _byId = new();
        private readonly Dictionary<int, SoulRecord> _soulsById = new();
        private readonly HashSet<int> _followed = new();
        private int _followedMinimum = int.MaxValue;
        private int _followedMaximum = int.MinValue;

        /// <summary>저장 형식 버전.</summary>
        public int FormatVersion { get; set; } = 1;

        /// <summary>설정.</summary>
        public WorldSettings Settings { get; set; } = new();

        /// <summary>현재 연도(1틱 = 1년).</summary>
        public int Year { get; set; }

        /// <summary>난수 상태.</summary>
        public ulong RandomState { get; set; }

        /// <summary>난수 증분.</summary>
        public ulong RandomIncrement { get; set; }

        /// <summary>로그 난수 상태. 상태값 0도 유효하다.</summary>
        public ulong LogRandomState { get; set; }

        /// <summary>로그 난수 증분. 0이면 로그 난수가 없던 구 저장이다.</summary>
        public ulong LogRandomIncrement { get; set; }

        /// <summary>수선자(인덱스 = 자리, 비승하면 같은 자리에 새 영혼).</summary>
        public List<Cultivator> Cultivators { get; set; } = new();

        /// <summary>종문(인덱스 0 = 청운종, 1 = 천마문).</summary>
        public List<Sect> Sects { get; set; } = new();

        /// <summary>지역(인덱스 = 지역 번호).</summary>
        public List<Region> Regions { get; set; } = new();

        /// <summary>시대.</summary>
        public EraState Era { get; set; } = new();

        /// <summary>전쟁 상태.</summary>
        public WarState War { get; set; } = new();

        /// <summary>끝난 전쟁.</summary>
        public List<WarRecord> Wars { get; set; } = new();

        /// <summary>대비무 대회 기록.</summary>
        public List<TournamentRecord> Tournaments { get; set; } = new();

        /// <summary>열린 비경.</summary>
        public List<SecretRealmState> SecretRealms { get; set; } = new();

        /// <summary>진행 중인 수조.</summary>
        public List<BeastTideState> BeastTides { get; set; } = new();

        /// <summary>경매 기록(최근 30회).</summary>
        public List<AuctionRecord> Auctions { get; set; } = new();

        /// <summary>비승 명부.</summary>
        public List<ImmortalRecord> Immortals { get; set; } = new();

        /// <summary>영혼별 업적.</summary>
        public List<SoulRecord> Souls { get; set; } = new();

        /// <summary>사건 기록.</summary>
        public EventLog Log { get; set; } = new();

        /// <summary>연대기.</summary>
        public Chronicle Chronicle { get; set; } = new();

        /// <summary>세계 기록.</summary>
        public WorldRecords Records { get; set; } = new();

        /// <summary>통계 이력.</summary>
        public StatHistory Statistics { get; set; } = new();

        /// <summary>영석 장부.</summary>
        public StoneLedger Ledger { get; set; } = new();

        /// <summary>누적 집계.</summary>
        public WorldTotals Totals { get; set; } = new();

        /// <summary>관심 인물(영혼 번호).</summary>
        public List<int> Followed { get; set; } = new();

        /// <summary>천기(개입 점수).</summary>
        public int HeavenlyFavor { get; set; } = 3;

        /// <summary>다음 새 영혼 번호.</summary>
        public int NextSoulId { get; set; } = 1;

        /// <summary>세계 난수. 불러온 뒤 <see cref="RebuildIndexes"/>에서 상태로 복원한다.</summary>
        [JsonIgnore]
        public Pcg32 Random { get; set; } = new(1, 1);

        /// <summary>표시용 로그 난수. 세계의 행동·성장·전투에는 사용하지 않는다.</summary>
        [JsonIgnore]
        public Pcg32 LogRandom { get; set; } = new(1, LogRandomStream);

        /// <summary>영혼 번호로 수선자를 찾는다.</summary>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>수선자 또는 null.</returns>
        public Cultivator? Find(int soulId)
        {
            return soulId != 0 && _byId.TryGetValue(soulId, out Cultivator? cultivator) ? cultivator : null;
        }

        /// <summary>종문 번호(1·2)로 종문을 찾는다.</summary>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>종문 또는 null(산수).</returns>
        public Sect? FindSect(int sectId)
        {
            return sectId >= 1 && sectId <= Sects.Count ? Sects[sectId - 1] : null;
        }

        /// <summary>영혼 업적을 찾는다.</summary>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>업적 또는 null(아직 끝난 생이 없음).</returns>
        public SoulRecord? FindSoul(int soulId)
        {
            return _soulsById.TryGetValue(soulId, out SoulRecord? record) ? record : null;
        }

        /// <summary>새 영혼 업적을 등록한다.</summary>
        /// <param name="record">업적.</param>
        public void AddSoul(SoulRecord record)
        {
            Souls.Add(record);
            _soulsById[record.SoulId] = record;
        }

        /// <summary>수선자를 번호 색인에 넣는다(탄생·비승 교체 때).</summary>
        /// <param name="cultivator">수선자.</param>
        public void Register(Cultivator cultivator)
        {
            _byId[cultivator.Id] = cultivator;
        }

        /// <summary>번호 색인에서 뺀다(비승 때).</summary>
        /// <param name="soulId">영혼 번호.</param>
        public void Unregister(int soulId)
        {
            _byId.Remove(soulId);
        }

        /// <summary>관심 인물 여부.</summary>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>관심 인물이면 true.</returns>
        public bool IsFollowed(int soulId)
        {
            if (soulId < _followedMinimum || soulId > _followedMaximum)
            {
                return false;
            }

            // 전원·연속 번호 관심은 범위만으로 답한다. 드문 빈칸은 HashSet으로 확인한다.
            return _followed.Count == (long)_followedMaximum - _followedMinimum + 1 || _followed.Contains(soulId);
        }

        /// <summary>관심 목록의 조회 색인을 다시 만든다(로드·외부 목록 교체 뒤).</summary>
        public void RebuildFollowIndex()
        {
            _followed.Clear();
            _followedMinimum = int.MaxValue;
            _followedMaximum = int.MinValue;
            foreach (int id in Followed)
            {
                _followed.Add(id);
                _followedMinimum = System.Math.Min(_followedMinimum, id);
                _followedMaximum = System.Math.Max(_followedMaximum, id);
            }
        }

        /// <summary>관심 목록과 조회 색인을 함께 바꾼다. 존재하지 않는 인물은 추가하지 않는다.</summary>
        /// <param name="soulId">영혼 번호.</param>
        /// <param name="followed">관심 여부.</param>
        public void SetFollowed(int soulId, bool followed)
        {
            if (followed && Find(soulId) is not null && _followed.Add(soulId))
            {
                Followed.Add(soulId);
                _followedMinimum = System.Math.Min(_followedMinimum, soulId);
                _followedMaximum = System.Math.Max(_followedMaximum, soulId);
            }
            else if (!followed)
            {
                _followed.Remove(soulId);
                Followed.Remove(soulId);
                if (soulId == _followedMinimum || soulId == _followedMaximum)
                {
                    RebuildFollowIndex();
                }
            }
        }

        /// <summary>
        /// 불러온 뒤 저장하지 않는 색인(번호 사전·지역 거주자·난수)을 다시 만든다.
        /// </summary>
        public void RebuildIndexes()
        {
            _byId.Clear();
            foreach (Cultivator cultivator in Cultivators)
            {
                _byId[cultivator.Id] = cultivator;
            }

            foreach (Region region in Regions)
            {
                region.Residents.Clear();
                List<int>? saved = region.TakeSavedResidentIds();
                if (saved is null)
                {
                    continue;
                }

                foreach (int id in saved)
                {
                    if (_byId.TryGetValue(id, out Cultivator? resident) && resident.RegionId == region.Id)
                    {
                        region.Residents.Add(resident);
                    }
                }
            }

            // 저장된 순서가 없거나 어긋난 수선자는 자리 순서로 채운다.
            foreach (Cultivator cultivator in Cultivators)
            {
                List<Cultivator> residents = Regions[cultivator.RegionId].Residents;
                if (!residents.Contains(cultivator))
                {
                    residents.Add(cultivator);
                }
            }

            _soulsById.Clear();
            foreach (SoulRecord soul in Souls)
            {
                _soulsById[soul.SoulId] = soul;
            }

            Random = Pcg32.FromState(RandomState, RandomIncrement);
            LogRandom = LogRandomIncrement == 0 ? new Pcg32(Settings.Seed, LogRandomStream)
                : Pcg32.FromState(LogRandomState, LogRandomIncrement);
            RebuildFollowIndex();
        }

        /// <summary>저장 직전에 난수 상태를 속성으로 옮긴다.</summary>
        public void CaptureRandomState()
        {
            RandomState = Random.State;
            RandomIncrement = Random.Increment;
            LogRandomState = LogRandom.State;
            LogRandomIncrement = LogRandom.Increment;
        }
    }
}
