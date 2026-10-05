using System.Diagnostics;

namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // Accept clock time independently from execution. The native UI only enqueues;
    // its worker processes one minute under the state lock, then releases it.
    public void QueueTime(double seconds,int speed)
    {
        if(!double.IsFinite(seconds) || seconds<0 || seconds>3600 || !Speeds.Contains(speed))
            throw new ArgumentOutOfRangeException(nameof(seconds));
        double minutes=State.MinuteProgress+seconds*speed*60/SecondsPerGameHour;
        int due=checked(State.PendingClockMinutes+(int)Math.Floor(minutes+1e-9));
        State.MinuteProgress=Math.Clamp(minutes-Math.Floor(minutes+1e-9),0,.999999999999);
        State.HourProgress=State.MinuteProgress/60; State.PendingClockMinutes=due;
        State.PendingClockHours=due/60;
    }
    public bool AdvanceQueuedMinute()
    {
        if(State.PendingClockMinutes==0) return false;
        AdvanceMinute(); State.PendingClockMinutes--; State.PendingClockHours=State.PendingClockMinutes/60;
        return true;
    }
    public int AdvanceFrame(double seconds,int speed)
    {
        QueueTime(seconds,speed);
        var r=Rules.Performance;
        long start=Stopwatch.GetTimestamp(),before=State.CompletedHours; int count=0;
        while(State.PendingClockMinutes>0 && count<r.MaximumCatchupHours*60 && (count==0 || Stopwatch.GetElapsedTime(start).TotalMilliseconds<r.TickBudgetMilliseconds))
        { AdvanceQueuedMinute(); count++; }
        State.PendingClockHours=State.PendingClockMinutes/60;
        return checked((int)(State.CompletedHours-before));
    }
}
