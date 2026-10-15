namespace VietnameseLunarCalendar.Domain.Models;

/// <summary>Describes one Vietnamese lunar month and its Gregorian start date.</summary>
public sealed record LunarMonthInfo(int LunarYear, int LunarMonth, bool IsLeapMonth, DateOnly StartGregorianDate, int DaysInMonth);
