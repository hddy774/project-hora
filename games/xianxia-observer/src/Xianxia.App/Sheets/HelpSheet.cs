// 게임 도감: 실제 경지·시설·호칭 규칙과 관찰·저장 조작을 설명한다.
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>오프라인으로 읽는 게임 규칙과 조작 설명.</summary>
    internal sealed class HelpSheet : SheetBase
    {
        /// <summary>도감을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">도감 연결.</param>
        internal HelpSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            Title = "선로만상록 프로젝트 · 도감";
            Body.Children.Add(Ui.Section("관찰하는 천하", Ui.Text(
                "수선자들은 수련·탐색·거래·기예·비무를 스스로 선택합니다. 상단에서 진행/정지와 속도를 바꾸세요. "
                + "인물·종문·지도·역사 항목을 누르면 자세한 내용을 볼 수 있습니다. 뒤로 키는 상세 시트부터 닫습니다.")));
            Body.Children.Add(Ui.Section("종문과 전쟁", Ui.Text(
                "청운종은 기초와 고경지, 천마문은 빠른 성장과 혈전에 강합니다. 공헌에 따라 직위와 녹봉이 달라집니다. "
                + "첫 전쟁은 " + Fmt.Years(Balance.FirstWarYear) + "에 시작하며 천 년 전쟁과 만 년 휴전이 이어집니다. "
                + "접경지 점령·금고·시설·파견 인원이 전쟁에 영향을 줍니다.")));
            Body.Children.Add(Ui.Section("사건과 기예", Ui.Text(
                "휴전기에는 천하 대비무가 열립니다. 비경에서 보물을 찾고, 수조를 막고, 경매에서 희귀한 공법을 구합니다. "
                + "연단·연기·진법·부록은 단약·법보·방어·부적에 영향을 줍니다. 현황에서 자원과 사건의 추이를 볼 수 있습니다.")));
            Body.Children.Add(Ui.Section("천기 개입", Ui.Text(
                "천기는 " + Balance.FavorRegenYears + "년마다 1점, 최대 " + Balance.MaxHeavenlyFavor
                + "점입니다. 인물 상세에서 2점으로 기연을 내리고, 지역 상세에서 3점으로 비경이나 수조를 엽니다. "
                + "기연은 깨달음·영기·도심을 늘리고 부상을 회복합니다. 같은 지역의 같은 사건은 중복 소환할 수 없습니다.")));
            Body.Children.Add(Ui.Section("윤회와 업적", Ui.Text(
                "생이 끝나면 같은 영혼이 새 인생을 시작합니다. 전생의 업적과 기억은 다음 생에 영향을 줍니다. "
                + "업적 탭은 완료한 생의 최고 점수·상위 5생 합·두 순위 합을 비교합니다. 현생의 점수는 아직 확정되지 않았습니다.")));
            Body.Children.Add(Ui.Section("저장", Ui.Text(
                "저장은 기기 안에만 보관됩니다. 90초 자동 저장, 백그라운드 저장과 설정의 수동 저장을 지원합니다. "
                + "불러오기와 새 세계는 현재 진행을 교체합니다. 앱 삭제 시 저장도 삭제됩니다.")));
            AddRealms();
            foreach (TitleDefinition title in TitleCatalog.All)
            {
                Body.Children.Add(Ui.Section(title.Name + " · " + title.Score + "점", Ui.Muted(title.Description)));
            }
        }

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            // 도감은 고정 규칙만 사용한다. 세계 진행으로 내용이 바뀌지 않는다.
        }

        private void AddRealms()
        {
            KeyValueList realms = new();
            realms.Begin();
            for (int index = 0; index < Realms.Count; index++)
            {
                realms.Add(Realms.Name(index), "기본 수명 " + Fmt.Years(Realms.BaseLifespan(index)));
            }

            realms.End();
            Body.Children.Add(Ui.Section("경지 · 초기→중기→후기→대원만", realms.View,
                Ui.Muted("영기를 채운 뒤 돌파를 시도합니다. 공법·기초·도심·단약이 확률에 영향을 줍니다. "
                    + "고경지에는 천겁이 따르며, 반선경 끝에는 비승을 시도합니다.")));
        }
    }
}
