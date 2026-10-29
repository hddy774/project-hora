// Android 첫 화면(Activity). 세로 화면 고정, 화면 회전·크기 변경 시 Activity를 다시 만들지 않는다.
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using Avalonia.Android;

namespace Xianxia.Android
{
    /// <summary>선로만상록 프로젝트의 유일한 Activity.</summary>
    [Activity(
        Name = "org.hora.xianxiaobserver.MainActivity",
        Label = "선로만상록 프로젝트",
        Theme = "@style/SeonhyeopTheme",
        Icon = "@mipmap/ic_launcher",
        RoundIcon = "@mipmap/ic_launcher_round",
        MainLauncher = true,
        Exported = true,
        ScreenOrientation = ScreenOrientation.Portrait,
        WindowSoftInputMode = SoftInput.AdjustResize,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode
            | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density
            | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.FontScale)]
    public sealed class MainActivity : AvaloniaMainActivity
    {
        /// <inheritdoc/>
        public override bool DispatchTouchEvent(MotionEvent? ev)
        {
            if (ev is null || !ev.IsFromSource(InputSourceType.Touchscreen))
            {
                return base.DispatchTouchEvent(ev);
            }

            using MotionEvent? normalized = NormalizeUndefinedTouch(ev);
            return base.DispatchTouchEvent(normalized ?? ev);
        }

        // Avalonia12.1.4는 Unknown을 TouchDevice로 보내면서 LeftButtonDown/Up으로 분류해 무시한다.
        // 터치스크린에서만 미지정 도구를 Finger로 보정한다. 실제 손가락·펜·마우스는 그대로 전달한다.
        private static MotionEvent? NormalizeUndefinedTouch(MotionEvent source)
        {
            if (!HasUndefinedTool(source))
            {
                return null;
            }

            MotionEvent.PointerProperties[] properties = new MotionEvent.PointerProperties[source.PointerCount];
            MotionEvent.PointerCoords[] coordinates = new MotionEvent.PointerCoords[source.PointerCount];
            try
            {
                CopyPointers(source, properties, coordinates);

                return MotionEvent.Obtain(source.DownTime, source.EventTime, source.Action, source.PointerCount,
                    properties, coordinates, source.MetaState, source.ButtonState, source.XPrecision,
                    source.YPrecision, source.DeviceId, source.EdgeFlags, source.Source, source.Flags);
            }
            finally
            {
                for (int index = 0; index < properties.Length; index++)
                {
                    properties[index]?.Dispose();
                    coordinates[index]?.Dispose();
                }
            }
        }

        private static bool HasUndefinedTool(MotionEvent source)
        {
            for (int index = 0; index < source.PointerCount; index++)
            {
                if (source.GetToolType(index) == MotionEventToolType.Unknown)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CopyPointers(MotionEvent source, MotionEvent.PointerProperties[] properties,
            MotionEvent.PointerCoords[] coordinates)
        {
            for (int index = 0; index < source.PointerCount; index++)
            {
                properties[index] = new MotionEvent.PointerProperties();
                coordinates[index] = new MotionEvent.PointerCoords();
                source.GetPointerProperties(index, properties[index]);
                source.GetPointerCoords(index, coordinates[index]);
                if (properties[index].ToolType == MotionEventToolType.Unknown)
                {
                    properties[index].ToolType = MotionEventToolType.Finger;
                }
            }
        }

        /// <inheritdoc/>
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            global::Xianxia.App.App.SetDeviceFontScale(Resources?.Configuration?.FontScale ?? 1);
            base.OnCreate(savedInstanceState);
        }

        /// <inheritdoc/>
        public override void OnConfigurationChanged(Configuration newConfig)
        {
            base.OnConfigurationChanged(newConfig);
            global::Xianxia.App.App.SetDeviceFontScale(newConfig.FontScale);
        }
    }
}
