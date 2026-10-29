// 지역의 변하는 상태(지배 세력·세력 점수·전투 수·일시적 영기 변화)와 거주자 색인.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Xianxia.Simulation.Data;

namespace Xianxia.Simulation.Model
{
    /// <summary>지역 하나. 고정 정보는 <see cref="Definition"/>에 있다.</summary>
    public sealed class Region
    {
        private List<int>? _savedResidentIds;

        /// <summary>지역 번호(0~12).</summary>
        public int Id { get; set; }

        /// <summary>지배 종문 번호(0이면 중립).</summary>
        public int ControllerSectId { get; set; }

        /// <summary>세력 점수(-10~10). +4 이상 청운종, -4 이하 천마문 지배(원본 규칙).</summary>
        public int ControlPoints { get; set; }

        /// <summary>누적 전투 수.</summary>
        public int Battles { get; set; }

        /// <summary>일시 영기 배율(수조 중 감소, 비경 중 증가).</summary>
        public double AuraModifier { get; set; } = 1.0;

        /// <summary>최근 상황 한 줄.</summary>
        public string Status { get; set; } = "평온";

        /// <summary>지역 시장의 단약 재고(기예가가 만든 것). 인덱스는 <see cref="PillKind"/>.</summary>
        public List<int> MarketPills { get; set; } = new() { 0, 0, 0, 0 };

        /// <summary>지역 시장의 부적 재고. 인덱스는 <see cref="TalismanKind"/>.</summary>
        public List<int> MarketTalismans { get; set; } = new() { 0, 0 };

        /// <summary>고정 정의.</summary>
        [JsonIgnore]
        public RegionDefinition Definition => RegionCatalog.All[Id];

        /// <summary>이름.</summary>
        [JsonIgnore]
        public string Name => Definition.Name;

        /// <summary>종류.</summary>
        [JsonIgnore]
        public RegionKind Kind => Definition.Kind;

        /// <summary>현재 영기 농도(정의 × 일시 배율).</summary>
        [JsonIgnore]
        public double Aura => Definition.Aura * AuraModifier;

        /// <summary>
        /// 현재 거주자. [성능] 매 틱 전체 목록을 걸러 상대를 찾지 않도록 지역별로 따로 유지한다.
        /// 저장하지 않고 불러온 뒤 다시 만든다.
        /// </summary>
        [JsonIgnore]
        public List<Cultivator> Residents { get; } = new();

        /// <summary>
        /// 저장용 거주자 번호(순서 포함). [결정론] 상대 고르기가 거주자 순서에 의존하므로 순서까지 저장해야
        /// 불러온 뒤에도 같은 역사가 이어진다.
        /// </summary>
        public List<int> ResidentIds
        {
            get => Residents.ConvertAll(resident => resident.Id);
            set => _savedResidentIds = value;
        }

        /// <summary>불러온 거주자 번호를 꺼내고 비운다(색인 재구성용).</summary>
        /// <returns>저장된 번호 또는 null.</returns>
        internal List<int>? TakeSavedResidentIds()
        {
            List<int>? ids = _savedResidentIds;
            _savedResidentIds = null;
            return ids;
        }

        /// <summary>검증용 주민 번호. 읽어도 저장된 순서를 소비하지 않는다.</summary>
        [JsonIgnore]
        internal IReadOnlyList<int>? SavedResidentIds => _savedResidentIds;
    }
}
