// 글자만 시스템 배율로 확대한다. 화면 전체나 48 터치 영역을 축소하지 않는다.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace Xianxia.App.Theme
{
    /// <summary>정적 역할 크기와 기기 글자 배율을 연결하는 동적 자원.</summary>
    internal static class Typography
    {
        private static readonly HashSet<double> s_sizes = new();
        private static double s_scale = 1;

        /// <summary>현재 시스템 글자 배율.</summary>
        internal static double Scale => s_scale;

        /// <summary>글자가 아닌 그래프·약도도 다시 배치할 시점.</summary>
        internal static event Action? Changed;

        /// <summary>앱 초기 자원을 마련한다. XAML 입력 컨트롤도 같은 배율을 쓴다.</summary>
        internal static void Initialize()
        {
            Ensure(13);
            Ensure(15);
            Ensure(20);
            SetScale(s_scale);
        }

        /// <summary>열린 화면에도 배율을 즉시 반영한다.</summary>
        /// <param name="scale">Android 기기 설정 또는 시험 배율.</param>
        internal static void SetScale(double scale)
        {
            double previous = s_scale;
            s_scale = double.IsFinite(scale) ? Math.Clamp(scale, 0.8, 3) : 1;
            if (Application.Current is not { } application)
            {
                return;
            }

            foreach (double size in s_sizes)
            {
                application.Resources[Key(size)] = size * s_scale;
            }

            if (previous != s_scale)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>기존 화면과 나중에 만들어질 행이 같은 동적 글자 자원을 쓴다.</summary>
        /// <param name="control">글자 또는 입력 컨트롤.</param>
        /// <param name="property">대상의 글자 크기 속성.</param>
        /// <param name="size">기본 글자 크기.</param>
        internal static void Bind(Control control, StyledProperty<double> property, double size)
        {
            double basis = Math.Max(13, size);
            Ensure(basis);
            control.Bind(property, new DynamicResourceExtension(Key(basis)));
        }

        /// <summary>새 역할을 최초 한 번만 등록해 행 갱신의 자원 변경 전파를 줄인다.</summary>
        private static void Ensure(double size)
        {
            if (s_sizes.Add(size) && Application.Current is { } application)
            {
                application.Resources[Key(size)] = size * s_scale;
            }
        }

        private static string Key(double size) => "BookFont" + size.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
