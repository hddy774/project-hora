// 선택적 측정 누계. 세계 규칙·난수·저장 바이트와 분리하며 보고 도구만 켠다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>영기를 실제로 증가시키는 다섯 경로.</summary>
    public enum QiSource
    {
        /// <summary>일반 수련.</summary>
        Cultivation,
        /// <summary>자연 기연의 비율 보상.</summary>
        Fortune,
        /// <summary>비경 영천.</summary>
        SecretRealm,
        /// <summary>관찰자의 천기 기연.</summary>
        HeavenlyFavor,
        /// <summary>스승의 지도.</summary>
        Teaching,
    }

    /// <summary>경지 체류 구간과 영기 출처. 켜고 꺼도 세계를 변경하지 않는다.</summary>
    public sealed class WorldDiagnostics
    {
        private readonly Dictionary<int, Visit> _visits = new();
        private readonly long[] _years = new long[Realms.Count];
        private readonly long[] _samples = new long[Realms.Count];
        private readonly long[] _downward = new long[Realms.Count];
        private readonly double[,] _qi = new double[Realms.Count, Enum.GetValues<QiSource>().Length];

        /// <summary>현재 세계에서 측정을 시작한다. 과거 체류를 추정하지 않는다.</summary>
        /// <param name="world">측정할 세계.</param>
        public WorldDiagnostics(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Cultivator person in world.Cultivators)
            {
                BeginLife(world.Year, person);
            }
        }

        /// <summary>새 생 또는 새 영혼의 첫 체류를 시작한다.</summary>
        /// <param name="year">현재 해.</param>
        /// <param name="person">인물.</param>
        public void BeginLife(int year, Cultivator person) => _visits[person.Id] = new Visit(person.Realm, year);

        /// <summary>상승·하락 시 이전 체류를 닫고 새 경지를 기록한다.</summary>
        /// <param name="year">현재 해.</param>
        /// <param name="person">경지가 바뀐 인물.</param>
        public void RealmChanged(int year, Cultivator person)
        {
            if (_visits.TryGetValue(person.Id, out Visit previous))
            {
                if (previous.Realm == person.Realm)
                {
                    return;
                }

                Close(year, previous);
                if (person.Realm < previous.Realm)
                {
                    _downward[previous.Realm]++;
                }
            }

            BeginLife(year, person);
        }

        /// <summary>좌화·비승으로 종료한 마지막 체류를 센다.</summary>
        /// <param name="year">종료 해.</param>
        /// <param name="person">종료 인물.</param>
        public void EndLife(int year, Cultivator person)
        {
            if (_visits.Remove(person.Id, out Visit previous))
            {
                Close(year, previous);
            }
        }

        /// <summary>실제 증가량을 출처와 당시 경지에 누적한다. RNG는 사용하지 않는다.</summary>
        /// <param name="person">수혜 인물.</param>
        /// <param name="source">출처.</param>
        /// <param name="amount">실제 증가량.</param>
        public void AddQi(Cultivator person, QiSource source, double amount)
        {
            if (amount > 0 && double.IsFinite(amount))
            {
                _qi[person.Realm, (int)source] += amount;
            }
        }

        /// <summary>종료된 경지 체류의 총 연수.</summary>
        /// <param name="realm">경지.</param>
        /// <returns>총 연수.</returns>
        public long DwellYears(int realm) => _years[realm];

        /// <summary>종료된 경지 체류 표본 수. 생존 구간은 제외한다.</summary>
        /// <param name="realm">경지.</param>
        /// <returns>표본 수.</returns>
        public long DwellSamples(int realm) => _samples[realm];

        /// <summary>체류 종료 중 경지 하락으로 끝난 수.</summary>
        /// <param name="realm">경지.</param>
        /// <returns>하락 구간 수.</returns>
        public long DownwardExits(int realm) => _downward[realm];

        /// <summary>출처별 실제 영기 증가량.</summary>
        /// <param name="realm">당시 경지.</param>
        /// <param name="source">출처.</param>
        /// <returns>영기.</returns>
        public double QiGained(int realm, QiSource source) => _qi[realm, (int)source];

        private void Close(int year, Visit visit)
        {
            _years[visit.Realm] += Math.Max(0, year - visit.EnteredYear);
            _samples[visit.Realm]++;
        }

        /// <summary>진단을 켠 이후의 한 경지 체류 구간.</summary>
        /// <param name="Realm">경지.</param>
        /// <param name="EnteredYear">진입 해.</param>
        private readonly record struct Visit(int Realm, int EnteredYear);
    }
}
