using System.Diagnostics;

namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // Split a catch-up into bounded work on the native UI thread. Unprocessed
    // hours persist with the same state; each completed hour still records once.
    public int AdvanceFrame(double seconds,int speed)
    {
        if(!double.IsFinite(seconds) || seconds<0 || seconds>3600 || !Speeds.Contains(speed))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        var r=Rules.Performance;
        double minutes=State.MinuteProgress+seconds*speed*60/SecondsPerGameHour;
        int due=checked(State.PendingClockMinutes+(int)Math.Floor(minutes+1e-9));
        State.MinuteProgress=Math.Clamp(minutes-Math.Floor(minutes+1e-9),0,.999999999999);
        State.HourProgress=State.MinuteProgress/60; State.PendingClockMinutes=due;
        long start=Stopwatch.GetTimestamp(),before=State.CompletedHours; int count=0;
        while(State.PendingClockMinutes>0 && count<r.MaximumCatchupHours*60 && (count==0 || Stopwatch.GetElapsedTime(start).TotalMilliseconds<r.TickBudgetMilliseconds))
        { AdvanceMinute(); State.PendingClockMinutes--; count++; }
        State.PendingClockHours=State.PendingClockMinutes/60;
        return checked((int)(State.CompletedHours-before));
    }
}
