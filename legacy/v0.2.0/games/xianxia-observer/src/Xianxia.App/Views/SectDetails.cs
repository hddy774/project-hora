// 종문 탭과 상세 시트가 함께 쓰는 종문 현황. 시설·정원 값은 시뮬레이션 규칙표를 그대로 읽는다.
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Views
{
    /// <summary>종문 요약·시설·직위 정원 표시.</summary>
    internal sealed class SectDetails
    {
        private readonly KeyValueList _summary = new();
        private readonly KeyValueList _facilities = new();
        private readonly KeyValueList _ranks = new();
        private readonly TextBlock _ideology = Ui.Muted();
        private readonly Button _leader;
        private int _leaderId;

        /// <summary>재사용 표시 요소를 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal SectDetails(INavigator navigator)
        {
            _leader = Ui.Button("종주", () => navigator.Open(SheetLink.ToCultivator(_leaderId)), "small");
            View = Ui.Column(8, _ideology, _leader, _summary.View,
                Ui.Section("시설", _facilities.View), Ui.Section("직위 정원 · 100년 녹봉", _ranks.View));
        }

        /// <summary>화면 요소.</summary>
        internal Control View { get; }

        /// <summary>현재 종문을 읽어 표시한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="sect">대상 종문.</param>
        internal void Refresh(World world, Sect sect)
        {
            SectSummary summary = Queries.Summarize(world, sect.Id);
            _ideology.Text = sect.IdeologyName + " · " + sect.IdeologyDescription;
            _leaderId = sect.LeaderId;
            _leader.Content = "종주: " + (_leaderId == 0 ? "공석" : Queries.NameOf(world, _leaderId));
            _leader.IsEnabled = _leaderId != 0;
            _summary.Begin();
            _summary.Add("구성원 / 파견", summary.Members + "명 / " + summary.OnWarDuty + "명");
            _summary.Add("총 전투력 / 평균 경지", Fmt.Short(summary.Power) + " / " + Fmt.Decimal(summary.AverageRealm));
            _summary.Add("금고 / 누적 공헌", Fmt.Number(sect.Treasury) + " / " + Fmt.Number(sect.TotalContribution));
            _summary.Add("접경지 / 전적", summary.Borders + "곳 / " + sect.WarsWon + "승 " + sect.WarsLost + "패");
            _summary.Add("대비무 우승 / 비승", sect.TournamentWins + "회 / " + sect.Ascensions + "명");
            _summary.End();
            RefreshRules(sect);
        }

        private void RefreshRules(Sect sect)
        {
            _facilities.Begin();
            for (int index = 0; index < SectRules.FacilityCount; index++)
            {
                FacilityKind kind = (FacilityKind)index;
                _facilities.Add(SectRules.FacilityName(kind) + " " + sect.Level(kind) + "단계",
                    SectRules.FacilityEffect(kind));
            }

            _facilities.End();
            _ranks.Begin();
            for (int index = 0; index < sect.RankCounts.Count; index++)
            {
                SectRank rank = (SectRank)index;
                string capacity = index == 0 ? "제한 없음" : SectRules.Capacity(rank).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                _ranks.Add(Labels.Of(rank), sect.RankCounts[index] + " / " + capacity
                    + " · " + SectRules.Stipend(rank) + " 영석");
            }

            _ranks.End();
        }
    }
}
