namespace Chronicles.Core;

public static class GameCalendar
{
    public const long TicksPerYear = 2_000;
    public const int RealMillisecondsPerYearAtNormalSpeed = 10_000;

    public static long GetYear(SimulationTick tick) => tick.Value / TicksPerYear;

    public static long GetAgeYears(SimulationTick birthTick, SimulationTick currentTick)
    {
        if (currentTick.Value < birthTick.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick));
        }

        return (currentTick.Value - birthTick.Value) / TicksPerYear;
    }

    public static SimulationTick FromYears(long years)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(years);
        return new SimulationTick(checked(years * TicksPerYear));
    }

    public static double GetRealSecondsPerGameYear(int speed)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(speed);
        return (RealMillisecondsPerYearAtNormalSpeed / 1_000d) / speed;
    }
}
