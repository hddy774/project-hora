// 고서 목판의 종이·먹·목색과 대비를 확보한 종문·경지·기록 분류색. 붓은 재사용한다.
using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Xianxia.Simulation.Data;

namespace Xianxia.App.Theme
{
    /// <summary>색상과 붓 캐시.</summary>
    internal static class Palette
    {
        internal static readonly Color Parchment = Color.Parse("#F3E8CC");
        internal static readonly Color HeaderBack = Color.Parse("#EFE2C4");
        internal static readonly Color PanelBack = Color.Parse("#FAF3DF");
        internal static readonly Color CardBack = Color.Parse("#FAF3DF");
        internal static readonly Color Ink = Color.Parse("#202A2B");
        internal static readonly Color Muted = Color.Parse("#655A46");
        internal static readonly Color Accent = Color.Parse("#805B25");
        internal static readonly Color OnAccent = Color.Parse("#FAF3DF");
        internal static readonly Color Line = Color.Parse("#B5A47A");
        internal static readonly Color Track = Color.Parse("#DDD0AF");
        internal static readonly Color Orthodox = Color.Parse("#2F5B79");
        internal static readonly Color Demonic = Color.Parse("#914740");
        internal static readonly Color Rogue = Color.Parse("#565656");
        internal static readonly Color War = Color.Parse("#8B4400");
        internal static readonly Color Peace = Color.Parse("#16613C");
        internal static readonly Color Gold = Color.Parse("#714E00");
        internal static readonly Color Silver = Color.Parse("#505960");
        internal static readonly Color Bronze = Color.Parse("#7A4A2B");
        internal static readonly Color Danger = Color.Parse("#9E3416");
        internal static readonly Color Favor = Color.Parse("#5A3FA0");
        internal static readonly Color Backdrop = Color.Parse("#C9202A2B");

        // 원본 RC(연기경~반선경) 색을 그대로 쓴다.
        private static readonly Color[] s_realms =
        {
            Color.Parse("#386038"), Color.Parse("#116144"), Color.Parse("#1155AA"), Color.Parse("#7700BB"),
            Color.Parse("#794C00"), Color.Parse("#8F4018"), Color.Parse("#9B3333"), Color.Parse("#8130A1"),
            Color.Parse("#6D5100"),
        };

        // LogKind 순서: 돌파, 천겁, 결투, 비무, 부상, 종문, 인연, 기연, 기예, 거래, 전쟁, 대회, 비경, 수조, 윤회, 시대, 비승, 호칭, 천기.
        private static readonly Color[] s_logKinds =
        {
            Color.Parse("#7700BB"), Color.Parse("#615500"), Color.Parse("#725212"), Color.Parse("#306000"),
            Color.Parse("#9B3325"), Color.Parse("#7A4C00"), Color.Parse("#903965"), Color.Parse("#116144"),
            Color.Parse("#664400"), Color.Parse("#6C5136"), Color.Parse("#8B4400"), Color.Parse("#1B614D"),
            Color.Parse("#175980"), Color.Parse("#8F4018"), Color.Parse("#8130A1"), Color.Parse("#555548"),
            Color.Parse("#714E00"), Color.Parse("#814C00"), Color.Parse("#5A3FA0"),
        };

        // ChronicleKind 순서: 세계, 시대, 경지, 비승, 종주, 전쟁, 대회, 비경, 수조, 경매, 배신, 기록, 좌화, 천기, 시설.
        private static readonly Color[] s_chronicleKinds =
        {
            Color.Parse("#7A4800"), Color.Parse("#555548"), Color.Parse("#7700BB"), Color.Parse("#714E00"),
            Color.Parse("#7A4C00"), Color.Parse("#8B4400"), Color.Parse("#1B614D"), Color.Parse("#175980"),
            Color.Parse("#8F4018"), Color.Parse("#6C5136"), Color.Parse("#9E3416"), Color.Parse("#814C00"),
            Color.Parse("#565656"), Color.Parse("#5A3FA0"), Color.Parse("#3D6121"),
        };

        // 원본 GC(재능 등급 1~5) 색.
        private static readonly Color[] s_talents =
        {
            Color.Parse("#565656"), Color.Parse("#116144"), Color.Parse("#1155AA"), Color.Parse("#794C00"),
            Color.Parse("#725212"),
        };

        private static readonly Color[] s_elements =
        {
            Color.Parse("#5E5430"), Color.Parse("#2A632A"), Color.Parse("#21598E"), Color.Parse("#99331F"),
            Color.Parse("#69502B"), Color.Parse("#5F42AC"), Color.Parse("#205C6B"), Color.Parse("#2B5E54"),
        };

        private static readonly Dictionary<uint, IBrush> s_brushes = new();

        /// <summary>경지 색.</summary>
        /// <param name="realm">경지(0~8).</param>
        /// <returns>색.</returns>
        internal static Color Realm(int realm)
        {
            return s_realms[Math.Clamp(realm, 0, s_realms.Length - 1)];
        }

        /// <summary>종문 색(산수는 회색).</summary>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>색.</returns>
        internal static Color Sect(int sectId)
        {
            return sectId switch
            {
                RegionCatalog.OrthodoxSectId => Orthodox,
                RegionCatalog.DemonicSectId => Demonic,
                _ => Rogue,
            };
        }

        /// <summary>사건 기록 분류 색.</summary>
        /// <param name="kind">분류.</param>
        /// <returns>색.</returns>
        internal static Color Of(LogKind kind)
        {
            return s_logKinds[Math.Clamp((int)kind, 0, s_logKinds.Length - 1)];
        }

        /// <summary>연대기 분류 색.</summary>
        /// <param name="kind">분류.</param>
        /// <returns>색.</returns>
        internal static Color Of(ChronicleKind kind)
        {
            return s_chronicleKinds[Math.Clamp((int)kind, 0, s_chronicleKinds.Length - 1)];
        }

        /// <summary>영근 속성 색.</summary>
        /// <param name="element">속성.</param>
        /// <returns>색.</returns>
        internal static Color Of(Element element)
        {
            return s_elements[Math.Clamp((int)element, 0, s_elements.Length - 1)];
        }

        /// <summary>재능 등급 색.</summary>
        /// <param name="grade">등급(1~5).</param>
        /// <returns>색.</returns>
        internal static Color Talent(int grade)
        {
            return s_talents[Math.Clamp(grade, 1, s_talents.Length) - 1];
        }

        /// <summary>순위 표시 색(1·2·3위는 금·은·동).</summary>
        /// <param name="rank">순위(1부터).</param>
        /// <returns>색.</returns>
        internal static Color Place(int rank)
        {
            return rank switch
            {
                1 => Gold,
                2 => Silver,
                3 => Bronze,
                _ => Muted,
            };
        }

        /// <summary>투명도를 바꾼 색.</summary>
        /// <param name="color">원래 색.</param>
        /// <param name="alpha">투명도(0~255).</param>
        /// <returns>색.</returns>
        internal static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        /// <summary>색에 맞는 붓. 같은 색이면 같은 객체를 돌려준다.</summary>
        /// <param name="color">색.</param>
        /// <returns>불변 붓.</returns>
        internal static IBrush Brush(Color color)
        {
            uint key = color.ToUInt32();
            if (!s_brushes.TryGetValue(key, out IBrush? brush))
            {
                brush = new ImmutableSolidColorBrush(color);
                s_brushes[key] = brush;
            }

            return brush;
        }

        /// <summary>반투명 붓(배지 배경·테두리용).</summary>
        /// <param name="color">색.</param>
        /// <param name="alpha">투명도.</param>
        /// <returns>붓.</returns>
        internal static IBrush Tint(Color color, byte alpha)
        {
            return Brush(WithAlpha(color, alpha));
        }
    }
}
