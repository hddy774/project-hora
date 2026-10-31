namespace Chronicles.Core;

public readonly record struct GameDate(long Year, int Month, int Day);

public static class GameCalendar
{
    public const long TicksPerYear = 2_000;
    public const int MonthsPerYear = 12;
    public const int DaysPerMonth = 30;
    public const int DaysPerYear = MonthsPerYear * DaysPerMonth;
    public const int RealMillisecondsPerYearAtNormalSpeed = 10_000;

    public static long GetYear(SimulationTick tick) => tick.Value / TicksPerYear;

    public static GameDate GetDate(SimulationTick tick)
    {
        var year = tick.Value / TicksPerYear;
        var tickWithinYear = tick.Value % TicksPerYear;
        var dayOfYear = checked((int)(tickWithinYear * DaysPerYear / TicksPerYear));
        return new GameDate(
            year,
            (dayOfYear / DaysPerMonth) + 1,
            (dayOfYear % DaysPerMonth) + 1);
    }

    public static long GetMonthIndex(SimulationTick tick)
    {
        var year = tick.Value / TicksPerYear;
        var tickWithinYear = tick.Value % TicksPerYear;
        var monthWithinYear = tickWithinYear * MonthsPerYear / TicksPerYear;
        return checked((year * MonthsPerYear) + monthWithinYear);
    }

    public static SimulationTick GetMonthStart(long monthIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(monthIndex);
        var year = monthIndex / MonthsPerYear;
        var monthWithinYear = monthIndex % MonthsPerYear;
        var tickWithinYear = checked(
            ((monthWithinYear * TicksPerYear) + MonthsPerYear - 1) / MonthsPerYear);
        return new SimulationTick(checked((year * TicksPerYear) + tickWithinYear));
    }

    public static SimulationTick GetNextMonthStart(SimulationTick tick) =>
        GetMonthStart(checked(GetMonthIndex(tick) + 1));

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
