// 목판 표제 뒤의 수묵 산수. 고정 벡터만 사용해 세계 난수·외부 이미지·추가 글꼴을 필요로 하지 않는다.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Xianxia.App.Theme;

namespace Xianxia.App.Controls
{
    /// <summary>읽기를 방해하지 않는 옅은 산수 장식.</summary>
    internal sealed class InkLandscape : Control
    {
        private static readonly StreamGeometry s_far = StreamGeometry.Parse(
            "M0,78 L12,51 26,61 43,28 55,45 69,16 83,41 99,29 121,58 144,48 170,73 "
            + "L235,75 257,55 276,64 302,27 317,45 333,15 350,46 367,31 391,61 400,55 L400,90 L0,90 Z");
        private static readonly StreamGeometry s_near = StreamGeometry.Parse(
            "M0,83 L24,69 45,77 71,49 86,62 98,55 122,79 151,69 176,85 "
            + "L239,84 262,71 285,77 308,54 320,64 335,51 361,78 380,68 400,82 L400,90 L0,90 Z");
        private static readonly StreamGeometry s_ridges = StreamGeometry.Parse(
            "M43,28 L42,56 55,45 M69,16 L63,47 74,39 83,57 M302,27 L299,53 313,47 "
            + "M333,15 L329,46 340,39 350,61 M71,49 L66,74 M335,51 L330,77");
        private static readonly Pen s_ridgePen = new(Palette.Tint(Palette.Ink, 0x18), 0.7);

        /// <summary>장식은 입력을 가로채지 않는다.</summary>
        internal InkLandscape() => IsHitTestVisible = false;

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 400, Bounds.Height / 90)))
            {
                context.DrawGeometry(Palette.Tint(Palette.Ink, 0x0A), null, s_far);
                context.DrawGeometry(Palette.Tint(Palette.Ink, 0x10), null, s_near);
                context.DrawGeometry(null, s_ridgePen, s_ridges);
            }
        }
    }
}
