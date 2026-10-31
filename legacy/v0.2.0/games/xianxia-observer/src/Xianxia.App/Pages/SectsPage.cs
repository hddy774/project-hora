// 종문 탭: 두 종문 상세 현황과 산수·사망 원인 요약을 표시한다.
using System.Linq;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.App.Views;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Pages
{
    /// <summary>두 종문의 시설·직위와 세계의 사망 원인을 비교하는 화면.</summary>
    internal sealed class SectsPage : PageBase
    {
        private readonly SectDetails[] _sects;
        private readonly KeyValueList _summary = new();

        /// <summary>종문 화면을 만든다.</summary>
        /// <param name="navigator">화면 이동.</param>
        internal SectsPage(INavigator navigator) : base(navigator)
        {
            _sects = new[] { new SectDetails(navigator), new SectDetails(navigator) };
            Body.Children.Add(Ui.Section("청운종", _sects[0].View,
                Ui.Button("청운종 강자·공헌·역사 ›", () => navigator.Open(SheetLink.ToSect(1)), "small")));
            Body.Children.Add(Ui.Section("천마문", _sects[1].View,
                Ui.Button("천마문 강자·공헌·역사 ›", () => navigator.Open(SheetLink.ToSect(2)), "small")));
            Body.Children.Add(Ui.Section("산수와 사망 원인", _summary.View));
        }

        /// <inheritdoc />
        internal override string Name => "종문";

        /// <inheritdoc />
        internal override string Glyph => "門";

        /// <inheritdoc />
        internal override void Refresh(World world)
        {
            for (int index = 0; index < _sects.Length; index++)
            {
                if (world.FindSect(index + 1) is { } sect)
                {
                    _sects[index].Refresh(world, sect);
                }
            }

            SectSummary rogues = Queries.Summarize(world, 0);
            _summary.Begin();
            _summary.Add("산수 인원 / 전투력", rogues.Members + "명 / " + Fmt.Short(rogues.Power));
            _summary.Add("수명 좌화", Fmt.Number(world.Totals.NaturalDeaths));
            _summary.Add("경지 붕괴", Fmt.Number(world.Totals.CollapseDeaths));
            _summary.Add("전쟁 경지 하락", Fmt.Number(world.Wars.Sum(war => (long)war.Casualties)
                + (world.War.AtWar ? world.War.Casualties : 0)));
            _summary.End();
        }
    }
}
