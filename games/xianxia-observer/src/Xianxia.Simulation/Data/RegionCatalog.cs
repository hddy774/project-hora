// 13개 지역의 고정 정의(원본 RGD 계승 + 위험도·영초·영철 풍부도·설명 추가).
using System.Collections.Generic;

namespace Xianxia.Simulation.Data
{
    /// <summary>지역 정의. 변하지 않는 값만 담는다(지배 세력·전투 수 등은 <c>Model.Region</c>).</summary>
    /// <param name="Id">지역 번호(0~12).</param>
    /// <param name="Name">이름.</param>
    /// <param name="Kind">종류.</param>
    /// <param name="HomeSectId">본래 소유 종문(접경지는 0).</param>
    /// <param name="Aura">영기 농도 배율. 원본 au/125.</param>
    /// <param name="Danger">탐색 중 부상 위험(0~1).</param>
    /// <param name="Herbs">영초 풍부도.</param>
    /// <param name="Ore">영철 풍부도.</param>
    /// <param name="Description">짧은 설명.</param>
    /// <param name="NearSectId">접경지에서 보급선이 가까운 종문(전쟁 지형 보정, 0이면 없음).</param>
    public sealed record RegionDefinition(
        int Id,
        string Name,
        RegionKind Kind,
        int HomeSectId,
        double Aura,
        double Danger,
        double Herbs,
        double Ore,
        string Description,
        int NearSectId = 0);

    /// <summary>지역 목록.</summary>
    public static class RegionCatalog
    {
        /// <summary>청운종 번호.</summary>
        public const int OrthodoxSectId = 1;

        /// <summary>천마문 번호.</summary>
        public const int DemonicSectId = 2;

        /// <summary>경매가 열리는 중립 시장(중원 평원).</summary>
        public const int MarketRegionId = 8;

        /// <summary>모든 지역. 인덱스 = 지역 번호.</summary>
        public static IReadOnlyList<RegionDefinition> All { get; } = new[]
        {
            new RegionDefinition(0, "청운 본산", RegionKind.Center, 1, 1.60, 0.03, 0.8, 0.6, "청운종 산문과 영맥이 모인 봉우리"),
            new RegionDefinition(1, "청운 동원", RegionKind.Periphery, 1, 1.34, 0.08, 1.2, 0.7, "약초밭과 영천이 흐르는 동쪽 정원"),
            new RegionDefinition(2, "청운 남림", RegionKind.Periphery, 1, 1.30, 0.10, 1.3, 0.8, "고목 숲. 영수가 드물게 출몰한다"),
            new RegionDefinition(3, "청운 북봉", RegionKind.Periphery, 1, 1.26, 0.12, 0.7, 1.2, "만년설 봉우리. 영철 광맥이 있다"),
            new RegionDefinition(4, "천마 본관", RegionKind.Center, 2, 1.56, 0.04, 0.6, 0.8, "천마문 본전. 혈지(血池)가 끓는다"),
            new RegionDefinition(5, "천마 서원", RegionKind.Periphery, 2, 1.32, 0.10, 1.0, 1.0, "독초가 자라는 서쪽 원림"),
            new RegionDefinition(6, "천마 남협", RegionKind.Periphery, 2, 1.28, 0.14, 0.8, 1.3, "검은 바위 협곡"),
            new RegionDefinition(7, "천마 북호", RegionKind.Periphery, 2, 1.24, 0.12, 1.1, 0.8, "안개 낀 북쪽 호수"),
            new RegionDefinition(8, "중원 평원", RegionKind.Border, 0, 0.76, 0.20, 1.0, 1.0, "상회와 경매장이 있는 교역로"),
            new RegionDefinition(9, "혈천 협곡", RegionKind.Border, 0, 0.70, 0.45, 0.7, 1.6, "옛 전장의 피가 스민 협곡", 2),
            new RegionDefinition(10, "만령 산맥", RegionKind.Border, 0, 0.68, 0.40, 1.4, 1.4, "영수가 들끓는 산맥", 1),
            new RegionDefinition(11, "황사 사막", RegionKind.Border, 0, 0.66, 0.35, 0.5, 1.5, "모래 아래 고대 유적이 잠든 사막", 2),
            new RegionDefinition(12, "옥룡 호수", RegionKind.Border, 0, 0.72, 0.25, 1.6, 0.6, "용의 전설이 깃든 호수", 1),
        };

        /// <summary>접경지 번호 목록.</summary>
        public static IReadOnlyList<int> BorderIds { get; } = new[] { 8, 9, 10, 11, 12 };

        /// <summary>비경·수조가 일어날 수 있는 지역(종문 본산 제외).</summary>
        public static IReadOnlyList<int> EventRegionIds { get; } = new[] { 1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12 };

        /// <summary>종문의 본산 지역 번호.</summary>
        /// <param name="sectId">종문 번호(1 또는 2).</param>
        /// <returns>본산 지역 번호.</returns>
        public static int HomeOf(int sectId)
        {
            return sectId == DemonicSectId ? 4 : 0;
        }

        /// <summary>종문 영지(본산 포함) 번호 목록.</summary>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>영지 번호.</returns>
        public static IReadOnlyList<int> TerritoryOf(int sectId)
        {
            return sectId == DemonicSectId ? new[] { 4, 5, 6, 7 } : new[] { 0, 1, 2, 3 };
        }
    }
}
