using Android.Content;
using Android.Graphics;
using Android.Views;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    bool HasModal => help || confirmNew || portraitZoom >= 0 || selectedStock >= 0 || selectedTrader >= 0 || showResult;

    void ResetGesture()
    {
        dragging = gestureActive = modalGesture = false;
        gestureModal = "";
        downX = downY = lastY = 0;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (!lifecycle.IsRunning) return false;
        lock (simulationGate) return TouchLocked(e);
    }
    bool TouchLocked(MotionEvent? e)
    {
        if (e is null) return false;
        float x = e.GetX() / scale, y = e.GetY() / scale;
        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                downX = x; downY = lastY = y; dragging = false; gestureActive = true;
                modalGesture = HasModal;
                gestureModal = modalScroll.Key;
                return true;
            case MotionEventActions.Move:
                if (!gestureActive) return true;
                if (Math.Abs(y - downY) > 7 || Math.Abs(x - downX) > 7) dragging = true;
                if (dragging)
                {
                    if (modalGesture && HasModal && gestureModal.Length > 0 && gestureModal == modalScroll.Key && modalScroll.Viewport.ContainsBody(downX, downY))
                    { modalScroll.Drag(lastY - y); Invalidate(); }
                    else if (!modalGesture && !HasModal && !lobby && downY > 102 && downY < h - 145)
                    { scroll = Math.Clamp(scroll + lastY - y, 0, maxScroll); Invalidate(); }
                }
                lastY = y; return true;
            case MotionEventActions.Up:
                bool click = gestureActive && !dragging && modalGesture == HasModal && Math.Abs(x - downX) < 18 && Math.Abs(y - downY) < 18;
                ResetGesture();
                if (click)
                {
                    for (int i = targets.Count - 1; i >= 0; i--)
                        if (x >= targets[i].Left && x < targets[i].Right && y >= targets[i].Top && y < targets[i].Bottom)
                        {
                            var action = targets[i].Action;
                            // Do not let a second tap reuse a dismissed modal's targets
                            // before Android draws the next frame.
                            targets.Clear();
                            PerformHapticFeedback(FeedbackConstants.VirtualKey); action(); Invalidate(); break;
                        }
                    PerformClick();
                }
                return true;
            case MotionEventActions.PointerDown:
            case MotionEventActions.Cancel: ResetGesture(); return true;
        }
        return true;
    }
    public override bool PerformClick() { base.PerformClick(); return true; }
}
