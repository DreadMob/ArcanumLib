using Vintagestory.API.MathTools;

namespace ArcanumLib.Blocks.Clock;

/// <summary>Game-calendar hour → chime count and hand angles (radians, clockwise as seen from the dial's front).</summary>
public static class ClockTime
{
    public static int GetChimeCount(long totalHour)
    {
        int hour = (int)((totalHour % 24 + 24) % 24);
        int count = hour % 12;
        return count == 0 ? 12 : count;
    }

    public static float GetHourAngle(double totalHours)
    {
        decimal hour = (decimal)totalHours % 12m;
        if (hour < 0) hour += 12m;
        return (float)(-(double)hour / 12d * GameMath.TWOPI);
    }

    public static float GetMinuteAngle(double totalHours)
    {
        decimal minute = ((decimal)totalHours % 1m) * 60m;
        if (minute < 0) minute += 60m;
        return (float)(-(double)minute / 60d * GameMath.TWOPI);
    }
}
