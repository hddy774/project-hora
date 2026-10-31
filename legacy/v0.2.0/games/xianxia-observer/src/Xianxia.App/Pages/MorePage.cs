// 자주 쓰는 네 화면은 하단에 두고 세계 기록과 설정은 책갈피에서 찾는다.
using Avalonia.Automation;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>기존 업적·현황·기록·도감·설정을 보존하는 책갈피.</summary>
    internal sealed class MorePage : PageBase
    {
        /// <summary>이동 카드를 한 번 만들고 복귀할 때 그대로 재사용한다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal MorePage(INavigator navigator) : base(navigator)
        {
            Body.Children.Add(Ui.Title("책갈피"));
            Body.Children.Add(Ui.Muted("오래 이어진 삶과 천하의 흐름을 살펴보세요."));
            AddDestination("업적", "영혼별 최고 생 · 상위 다섯 생 · 종합 순위", Destinations.Legacy);
            AddDestination("현황", "경지 분포 · 종문 인구와 전력의 변화", Destinations.Stats);
            AddDestination("기록", "최근 사건과 분류별 기록", Destinations.Log);
            Body.Children.Add(Ui.Section("관찰 안내",
                Ui.Button("도감 ›", () => navigator.Open(new SheetLink(LinkKind.Help, 0))),
                Ui.Button("설정 · 저장 ›", () => navigator.Open(new SheetLink(LinkKind.Settings, 0)))));
        }

        /// <inheritdoc/>
        internal override string Name => "더보기";

        /// <inheritdoc/>
        internal override string Glyph => "…";

        /// <inheritdoc/>
        internal override void Refresh(World world)
        {
            // 이동 연결은 세계를 읽거나 변경할 필요가 없다.
        }

        private void AddDestination(string title, string description, string destination)
        {
            Button button = Ui.Button(Ui.Column(4, Ui.Title(title + " ›"), Ui.Muted(description)),
                () => Navigator.Navigate(destination), "card");
            AutomationProperties.SetAutomationId(button, "more-" + destination);
            AutomationProperties.SetName(button, title);
            Body.Children.Add(button);
        }
    }
}
