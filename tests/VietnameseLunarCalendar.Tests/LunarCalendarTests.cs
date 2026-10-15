using VietnameseLunarCalendar.Domain.Models;
using VietnameseLunarCalendar.Infrastructure.LunarCalendar;
using Xunit;

namespace VietnameseLunarCalendar.Tests;

public sealed class LunarCalendarTests
{
	private readonly VietnameseLunarCalendarService _calendar = new();

	public static TheoryData<DateOnly, int, int, int> ReferenceVectors => new()
	{
		{ new(2024, 2, 10), 1, 1, 2024 },
		{ new(2024, 9, 17), 15, 8, 2024 },
		{ new(2025, 1, 29), 1, 1, 2025 },
		{ new(2025, 2, 28), 1, 2, 2025 },
		{ new(2025, 3, 29), 1, 3, 2025 },
		{ new(2025, 4, 28), 1, 4, 2025 },
		{ new(2025, 10, 6), 15, 8, 2025 },
		{ new(2026, 2, 17), 1, 1, 2026 },
		{ new(2026, 10, 15), 6, 9, 2026 }
	};

	[Theory]
	[MemberData(nameof(ReferenceVectors))]
	public void Gregorian_to_lunar_matches_verified_vietnamese_vectors(DateOnly gregorian, int day, int month, int year)
	{
		var lunar = _calendar.GregorianToLunar(gregorian);
		Assert.Equal((day, month, year, false), (lunar.Day, lunar.Month, lunar.Year, lunar.IsLeapMonth));
		Assert.Equal(gregorian, _calendar.LunarToGregorian(day, month, year));
	}

	[Theory]
	[InlineData(2024, 2, 9, 30, 12, 2023)]
	[InlineData(2024, 2, 10, 1, 1, 2024)]
	[InlineData(2025, 1, 28, 29, 12, 2024)]
	[InlineData(2025, 1, 29, 1, 1, 2025)]
	public void Tet_boundaries_are_assigned_to_the_correct_lunar_year(int y, int m, int d, int ld, int lm, int ly)
	{
		var lunar = _calendar.GregorianToLunar(new DateOnly(y, m, d));
		Assert.Equal((ld, lm, ly), (lunar.Day, lunar.Month, lunar.Year));
	}

	[Fact]
	public void Leap_month_is_distinct_from_regular_month()
	{
		Assert.True(_calendar.IsLeapMonth(2, 2023));
		Assert.Equal(new DateOnly(2023, 2, 20), _calendar.LunarToGregorian(1, 2, 2023));
		Assert.Equal(new DateOnly(2023, 3, 22), _calendar.LunarToGregorian(1, 2, 2023, true));
		Assert.Equal((1, 2, 2023, true), ToTuple(_calendar.GregorianToLunar(new DateOnly(2023, 3, 22))));
		Assert.False(_calendar.IsLeapMonth(2, 2024));
		Assert.True(_calendar.IsLeapMonth(6, 2025));
		Assert.Equal(new DateOnly(2025, 7, 25), _calendar.LunarToGregorian(1, 6, 2025, true));
	}

	[Fact]
	public void Rejects_nonexistent_leap_month_and_day_30_in_a_short_month()
	{
		Assert.Throws<ArgumentException>(() => _calendar.LunarToGregorian(1, 2, 2024, true));
		Assert.Throws<ArgumentOutOfRangeException>(() => _calendar.LunarToGregorian(30, 2, 2025));
	}

	[Fact]
	public void Returns_both_29_and_30_day_months()
	{
		var sizes = Enumerable.Range(1900, 201).SelectMany(year => _calendar.GetLunarMonths(year)).Select(m => m.DaysInMonth).ToHashSet();
		Assert.Contains(29, sizes);
		Assert.Contains(30, sizes);
	}

	[Fact]
	public void Every_supported_lunar_year_is_contiguous_and_round_trips_at_month_edges()
	{
		for (var year = 1900; year <= 2100; year++)
		{
			var months = _calendar.GetLunarMonths(year);
			Assert.Contains(months.Count, new[] { 12, 13 });
			Assert.All(months, m => Assert.Contains(m.DaysInMonth, new[] { 29, 30 }));
			Assert.Equal(months.Count == 13, months.Count(m => m.IsLeapMonth) == 1);
			Assert.Equal(months.Count, months.Select(m => (m.LunarMonth, m.IsLeapMonth)).Distinct().Count());
			for (var i = 1; i < months.Count; i++)
				Assert.Equal(months[i - 1].StartGregorianDate.AddDays(months[i - 1].DaysInMonth), months[i].StartGregorianDate);

			foreach (var month in months)
			{
				var start = _calendar.LunarToGregorian(1, month.LunarMonth, year, month.IsLeapMonth);
				Assert.Equal(month.StartGregorianDate, start);
				var first = _calendar.GregorianToLunar(start);
				Assert.Equal((1, month.LunarMonth, year, month.IsLeapMonth), ToTuple(first));
				var lastDate = start.AddDays(month.DaysInMonth - 1);
				var last = _calendar.GregorianToLunar(lastDate);
				Assert.Equal((month.DaysInMonth, month.LunarMonth, year, month.IsLeapMonth), ToTuple(last));
			}
		}
	}

	[Fact]
	public void Every_gregorian_date_in_supported_range_round_trips()
	{
		var date = new DateOnly(1900, 1, 1);
		var end = new DateOnly(2100, 12, 31);
		while (date <= end)
		{
			var lunar = _calendar.GregorianToLunar(date);
			Assert.Equal(date, _calendar.LunarToGregorian(lunar.Day, lunar.Month, lunar.Year, lunar.IsLeapMonth));
			date = date.AddDays(1);
		}
	}

	[Fact]
	public void Gregorian_range_is_enforced()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => _calendar.GregorianToLunar(new DateOnly(1899, 12, 31)));
		Assert.Throws<ArgumentOutOfRangeException>(() => _calendar.GregorianToLunar(new DateOnly(2101, 3, 1)));
	}

	private static (int Day, int Month, int Year, bool Leap) ToTuple(LunarDate lunar) => (lunar.Day, lunar.Month, lunar.Year, lunar.IsLeapMonth);
}
