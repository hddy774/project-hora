// 화면 색상표. 원본의 양피지 색조(배경 #F5EDE0, 먹색 글자, 갈색 강조)를 계승하고
// 경지·기록 분류·종문 색을 한곳에 모은다. 붓(brush)은 불변 객체로 캐시해 매 갱신마다 새로 만들지 않는다.
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
        internal static readonly Color Parchment = Color.Parse("#F5EDE0");
        internal static readonly Color HeaderBack = Color.Parse("#E8D8B8");
        internal static readonly Color PanelBack = Color.Parse("#EDE3CC");
        internal static readonly Color CardBack = Color.Parse("#E4D8BC");
        internal static readonly Color Ink = Color.Parse("#1E0E00");
        internal static readonly Color Muted = Color.Parse("#8A6840");
        internal static readonly Color Accent = Color.Parse("#7A4800");
        internal static readonly Color OnAccent = Color.Parse("#FDF0D8");
        internal static readonly Color Line = Color.Parse("#38644114");
        internal static readonly Color Track = Color.Parse("#1F644114");
        internal static readonly Color Orthodox = Color.Parse("#1155AA");
        internal static readonly Color Demonic = Color.Parse("#CC4444");
        internal static readonly Color Rogue = Color.Parse("#7F7F7F");
        internal static readonly Color War = Color.Parse("#CC6600");
        internal static readonly Color Peace = Color.Parse("#228855");
        internal static readonly Color Gold = Color.Parse("#B88200");
        internal static readonly Color Silver = Color.Parse("#7D8A96");
        internal static readonly Color Bronze = Color.Parse("#A0623A");
        internal static readonly Color Danger = Color.Parse("#B03A1A");
        internal static readonly Color Favor = Color.Parse("#5A3FA0");
        internal static readonly Color Backdrop = Color.Parse("#D9281405");

        // 원본 RC(연기경~반선경) 색을 그대로 쓴다.
        private static readonly Color[] s_realms =
        {
            Color.Parse("#5E9E5E"), Color.Parse("#22AA77"), Color.Parse("#1155AA"), Color.Parse("#7700BB"),
            Color.Parse("#BB7700"), Color.Parse("#BB5522"), Color.Parse("#CC4444"), Color.Parse("#8833AA"),
            Color.Parse("#886600"),
        };

        // LogKind 순서: 돌파, 천겁, 결투, 비무, 부상, 종문, 인연, 기연, 기예, 거래, 전쟁, 대회, 비경, 수조, 윤회, 시대, 비승, 호칭, 천기.
        private static readonly Color[] s_logKinds =
        {
            Color.Parse("#7700BB"), Color.Parse("#887700"), Color.Parse("#BB8822"), Color.Parse("#336600"),
            Color.Parse("#CC4433"), Color.Parse("#885500"), Color.Parse("#AA4477"), Color.Parse("#22AA77"),
            Color.Parse("#664400"), Color.Parse("#806040"), Color.Parse("#CC6600"), Color.Parse("#33AA88"),
            Color.Parse("#1F6F9F"), Color.Parse("#BB5522"), Color.Parse("#8833AA"), Color.Parse("#777766"),
            Color.Parse("#B88200"), Color.Parse("#AA6600"), Color.Parse("#5A3FA0"),
        };

        // ChronicleKind 순서: 세계, 시대, 경지, 비승, 종주, 전쟁, 대회, 비경, 수조, 경매, 배신, 기록, 좌화, 천기, 시설.
        private static readonly Color[] s_chronicleKinds =
        {
            Color.Parse("#7A4800"), Color.Parse("#777766"), Color.Parse("#7700BB"), Color.Parse("#B88200"),
            Color.Parse("#885500"), Color.Parse("#CC6600"), Color.Parse("#33AA88"), Color.Parse("#1F6F9F"),
            Color.Parse("#BB5522"), Color.Parse("#806040"), Color.Parse("#B03A1A"), Color.Parse("#AA6600"),
            Color.Parse("#666666"), Color.Parse("#5A3FA0"), Color.Parse("#4D7A2A"),
        };

        // 원본 GC(재능 등급 1~5) 색.
        private static readonly Color[] s_talents =
        {
            Color.Parse("#7F7F7F"), Color.Parse("#22AA77"), Color.Parse("#1155AA"), Color.Parse("#BB7700"),
            Color.Parse("#BB8822"),
        };

        private static readonly Color[] s_elements =
        {
            Color.Parse("#9A8A50"), Color.Parse("#3C8A3C"), Color.Parse("#2A6FB0"), Color.Parse("#C8452A"),
            Color.Parse("#8A6A3A"), Color.Parse("#6A4AC0"), Color.Parse("#3AA0B8"), Color.Parse("#4A9A8A"),
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
