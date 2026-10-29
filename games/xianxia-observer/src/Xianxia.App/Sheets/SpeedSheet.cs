// 작은 숫자 버튼 여섯 개를 3열×2행 선택 패널로 옮긴다.
using Avalonia.Automation;
using Avalonia.Controls;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Sheets
{
    /// <summary>실행기 속도 표와 동일한 선택지를 제공한다.</summary>
    internal sealed class SpeedSheet : SheetBase
    {
        /// <summary>선택 즉시 적용하고 이전 화면으로 돌아간다.</summary>
        /// <param name="navigator">화면 이동.</param>
        /// <param name="link">속도 선택 연결.</param>
        internal SpeedSheet(INavigator navigator, SheetLink link) : base(navigator, link)
        {
            Title = "관찰 속도";
            Body.Children.Add(Ui.Muted("현재 선택: " + TransportBar.SpeedText(navigator.Session.Runner.SpeedIndex)));
            Button[] choices = new Button[SimulationRunner.Speeds.Length];
            for (int index = 0; index < choices.Length; index++)
            {
                int selected = index;
                choices[index] = Ui.Button(TransportBar.SpeedText(index), () =>
                {
                    navigator.Session.Runner.SetSpeed(selected);
                    navigator.CloseSheet();
                });
                choices[index].Classes.Set("active", index == navigator.Session.Runner.SpeedIndex);
                AutomationProperties.SetAutomationId(choices[index], "speed-" + index);
            }

            Body.Children.Add(Ui.Cells(3, 8, choices));
            Body.Children.Add(Ui.Muted("1은 1초에 1년입니다. 최대는 기기에서 가능한 속도로 진행합니다."));
        }

        /// <inheritdoc/>
        internal override void Refresh(World world)
        {
            // 선택은 난수나 세계 값을 변경하지 않는다.
        }
    }
}
