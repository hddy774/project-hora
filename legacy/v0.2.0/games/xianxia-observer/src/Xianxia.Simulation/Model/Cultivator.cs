// 수선자(영혼 하나)의 상태. 영혼 번호는 윤회해도 유지되고, 현생 상태는 환생 때 초기화된다.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>
    /// 수선자 한 명. 저장 파일에 그대로 직렬화되므로 공개 속성은 모두 상태다.
    /// 다른 수선자와의 관계는 객체 참조가 아니라 영혼 번호로 가리킨다.
    /// </summary>
    public sealed partial class Cultivator
    {
        /// <summary>영혼 번호. 윤회해도 같고, 비승하면 새 영혼이 새 번호로 자리를 잇는다.</summary>
        public int Id { get; set; }

        /// <summary>영혼 이름(윤회해도 유지).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>도호. 원영경 이상에서 얻는다.</summary>
        public string? DaoName { get; set; }

        /// <summary>윤회 횟수(첫 생은 0).</summary>
        public int Reincarnations { get; set; }

        /// <summary>16세로 현생에 입세한 해(출생 연도가 아님).</summary>
        public int BornYear { get; set; }

        /// <summary>나이.</summary>
        public int Age { get; set; }

        /// <summary>수명(이 나이에 이르면 좌화한다).</summary>
        public int Lifespan { get; set; }

        /// <summary>경지(0 연기경 ~ 8 반선경).</summary>
        public int Realm { get; set; }

        /// <summary>단계(0 초기 ~ 3 대원만).</summary>
        public int Stage { get; set; }

        /// <summary>현재 단계에 쌓인 영기.</summary>
        public double Qi { get; set; }

        /// <summary>깨달음(0~1000). 대경지 돌파 확률을 높이고 돌파 뒤 소모된다.</summary>
        public double Insight { get; set; }

        /// <summary>도심(0~100). 낮으면 수련이 더디고 주화입마·배신 위험이 커진다.</summary>
        public double DaoHeart { get; set; } = 70;

        /// <summary>누적 부상. 최대 체력에 이르면 중상으로 단계가 떨어진다.</summary>
        public double Injury { get; set; }

        /// <summary>내공. 천겁과 전투력에 쓰인다(원본 cb).</summary>
        public double InnerPower { get; set; }

        /// <summary>강제 요양 남은 해(중상 후).</summary>
        public int RecoveryYears { get; set; }

        /// <summary>오성: 깨달음·돌파.</summary>
        public int Comprehension { get; set; }

        /// <summary>체질: 체력·수명·천겁 저항.</summary>
        public int Constitution { get; set; }

        /// <summary>근골: 전투력.</summary>
        public int Bone { get; set; }

        /// <summary>영근: 영기 흡수.</summary>
        public int Root { get; set; }

        /// <summary>영근 속성.</summary>
        public Element Element { get; set; }

        /// <summary>성격.</summary>
        public Personality Personality { get; set; }

        /// <summary>야망(0~1). 비경 진입·결투에 영향.</summary>
        public double Ambition { get; set; }

        /// <summary>신중함(0~1). 위험 회피.</summary>
        public double Caution { get; set; }

        /// <summary>매력(0~1). 인연·거래·기연.</summary>
        public double Charisma { get; set; }

        /// <summary>소속 종문 번호(0이면 떠돌이 산수).</summary>
        public int SectId { get; set; }

        /// <summary>종문 직위.</summary>
        public SectRank Rank { get; set; }

        /// <summary>현생 누적 공헌(직위 재편 기준).</summary>
        public double Merit { get; set; }

        /// <summary>사용 가능한 공헌점(장경각·연단방).</summary>
        public double ContributionPoints { get; set; }

        /// <summary>현재 머무는 지역 번호.</summary>
        public int RegionId { get; set; }

        /// <summary>전쟁 중 접경지 파견 여부.</summary>
        public bool OnWarDuty { get; set; }

        /// <summary>공법 등급(0~19).</summary>
        public int TechniqueGrade { get; set; }

        /// <summary>공법 속성. 영근과 같으면 수련 +10%.</summary>
        public Element TechniqueElement { get; set; }

        /// <summary>기예.</summary>
        public ArtKind Art { get; set; }

        /// <summary>기예 숙련(0~100).</summary>
        public double ArtSkill { get; set; }

        /// <summary>법보 등급(0 없음 ~ 5 천품).</summary>
        public int ArtifactGrade { get; set; }

        /// <summary>영석.</summary>
        public long SpiritStones { get; set; }

        /// <summary>영초.</summary>
        public int Herbs { get; set; }

        /// <summary>영철.</summary>
        public int Ore { get; set; }

        /// <summary>요단(영수 내단).</summary>
        public int BeastCores { get; set; }

        /// <summary>단약 보유량. 인덱스는 <see cref="PillKind"/>.</summary>
        public List<int> Pills { get; set; } = new() { 0, 0, 0, 0 };

        /// <summary>부적 보유량. 인덱스는 <see cref="TalismanKind"/>.</summary>
        public List<int> Talismans { get; set; } = new() { 0, 0 };

        /// <summary>현생에 복용한 연수단 수(최대 3).</summary>
        public int LongevityPillsTaken { get; set; }

        /// <summary>마지막 행동.</summary>
        public CultivatorAction LastAction { get; set; }

        /// <summary>현생에서 전생 기억을 되찾았는가.</summary>
        public bool MemoryAwakened { get; set; }

        /// <summary>마지막 천겁을 겪은 해.</summary>
        public int LastTribulationYear { get; set; }

        /// <summary>현재 단계 돌파에 필요한 영기.</summary>
        [JsonIgnore]
        public long QiRequirement => Realms.Requirement(Realm, Stage);

        /// <summary>재능 합계(오성+체질+근골+영근).</summary>
        [JsonIgnore]
        public int TalentTotal => Comprehension + Constitution + Bone + Root;

        /// <summary>재능 등급(1 범재 ~ 5 절세).</summary>
        [JsonIgnore]
        public int TalentGrade => Traits.TalentGrade(TalentTotal);

        /// <summary>떠돌이(산수) 여부.</summary>
        [JsonIgnore]
        public bool IsRogue => SectId == 0;

        /// <summary>최대 체력.</summary>
        [JsonIgnore]
        public double MaxHealth => 100 + (Constitution / 5.0) + (Realm * 40) + (Stage * 8);

        /// <summary>남은 체력 비율(0~1).</summary>
        [JsonIgnore]
        public double HealthRatio => System.Math.Clamp(1 - (Injury / MaxHealth), 0, 1);

        /// <summary>표시용 경지 이름(예: 금단경 후기).</summary>
        [JsonIgnore]
        public string RealmText => Realms.FullName(Realm, Stage);

        /// <summary>도호가 있으면 "이름(도호)", 없으면 이름.</summary>
        [JsonIgnore]
        public string DisplayName => DaoName is null ? Name : Name + "(" + DaoName + ")";

        /// <summary>몇 번째 생인가(1부터).</summary>
        [JsonIgnore]
        public int LifeNumber => Reincarnations + 1;
    }
}
