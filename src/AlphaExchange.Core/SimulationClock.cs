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
        double hours=State.HourProgress+seconds*speed/SecondsPerGameHour;
        int due=Math.Min(r.MaximumCatchupHours,checked(State.PendingClockHours+(int)Math.Floor(hours+1e-9)));
        State.HourProgress=Math.Clamp(hours-Math.Floor(hours+1e-9),0,.999999999999);
        State.PendingClockHours=due;
        long start=Stopwatch.GetTimestamp(); int count=0;
        while(State.PendingClockHours>0 && (count==0 || Stopwatch.GetElapsedTime(start).TotalMilliseconds<r.TickBudgetMilliseconds))
        { AdvanceHour(); State.PendingClockHours--; count++; }
        return count;
    }
}
