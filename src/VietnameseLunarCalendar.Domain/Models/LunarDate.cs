namespace VietnameseLunarCalendar.Domain.Models;

/// <summary>A Vietnamese lunisolar civil date.</summary>
public sealed record LunarDate
{
	/// <summary>Creates a validated lunar date. Month length is validated by the calendar service.</summary>
	public LunarDate(int day, int month, int year, bool isLeapMonth = false)
	{
		if (day is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(day), "Lunar day must be between 1 and 30.");
		if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month), "Lunar month must be between 1 and 12.");
		if (year is < 1899 or > 2101) throw new ArgumentOutOfRangeException(nameof(year), "Lunar year is outside the supported conversion envelope (1899–2101).");
		Day = day;
		Month = month;
		Year = year;
		IsLeapMonth = isLeapMonth;
	}

	/// <summary>Day of the lunar month, from 1 to 30.</summary>
	public int Day { get; }
	/// <summary>Lunar month number, from 1 to 12.</summary>
	public int Month { get; }
	/// <summary>Lunar year number.</summary>
	public int Year { get; }
	/// <summary>Whether this is the intercalary repetition of the month.</summary>
	public bool IsLeapMonth { get; }
}
