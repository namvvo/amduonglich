using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Domain.Interfaces;

/// <summary>Converts civil Gregorian dates to and from the Vietnamese lunisolar calendar.</summary>
public interface ILunarCalendarService
{
	/// <summary>Converts a Gregorian civil date using Vietnam's UTC+7 calendar.</summary>
	LunarDate GregorianToLunar(DateOnly date);

	/// <summary>Converts a lunar date to its Gregorian civil date.</summary>
	DateOnly LunarToGregorian(int day, int month, int year, bool isLeapMonth = false);

	/// <summary>Returns whether the specified numbered month is intercalary.</summary>
	bool IsLeapMonth(int month, int year);

	/// <summary>Returns the length of a regular or leap lunar month.</summary>
	int GetDaysInMonth(int month, int year, bool isLeapMonth = false);

	/// <summary>Returns the lunar months belonging to a lunar year, in order.</summary>
	IReadOnlyList<LunarMonthInfo> GetLunarMonths(int year);
}
