// 앱 공통 AppBuilder 설정(내장 한글 글꼴 등록). 데스크톱·Android·테스트가 같은 설정을 쓴다.
using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;

namespace Xianxia.App
{
    /// <summary>선협 관찰 앱이 공통으로 쓰는 <see cref="AppBuilder"/> 확장.</summary>
    public static class AppBuilderExtensions
    {
        /// <summary>내장 글꼴 모음의 이름(Avalonia fonts: 스킴).</summary>
        public const string FontCollectionKey = "fonts:Seonhyeop";

        /// <summary>기본 글꼴 이름. 내장 Noto Serif KR 부분집합을 가리킨다.</summary>
        public const string DefaultFontFamily = FontCollectionKey + "#Noto Serif KR";

        /// <summary>내장 Noto Serif KR 글꼴을 등록하고 기본 글꼴로 지정한다.</summary>
        /// <param name="builder">설정할 AppBuilder.</param>
        /// <returns>같은 AppBuilder.</returns>
        public static AppBuilder WithSeonhyeopFonts(this AppBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder
                .ConfigureFonts(fontManager => fontManager.AddFontCollection(new EmbeddedFontCollection(
                    new Uri(FontCollectionKey, UriKind.Absolute),
                    new Uri("avares://Xianxia.App/Assets/Fonts", UriKind.Absolute))))
                .With(new FontManagerOptions { DefaultFamilyName = DefaultFontFamily });
        }
    }
}
