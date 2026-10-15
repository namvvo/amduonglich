using System.Collections.Concurrent;
using System.Diagnostics;
using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Infrastructure.LunarCalendar;

/// <summary>
/// Meeus-based astronomical Vietnamese lunisolar calendar. Civil day boundaries and
/// solar-term month assignment use a fixed UTC+7 offset, independent of host timezone.
/// </summary>
public sealed class VietnameseLunarCalendarService : ILunarCalendarService
{
	private const double TimeZoneHours = 7.0;
	private const double SynodicMonth = 29.530588853;
	private const double LunarEpoch = 2451550.09766;
	private static readonly ConcurrentDictionary<int, IReadOnlyList<LunarMonthInfo>> YearCache = new();

	/// <inheritdoc />
	public LunarDate GregorianToLunar(DateOnly date)
	{
		var timer = Stopwatch.StartNew();
		var cacheHit = YearCache.ContainsKey(date.Year);
		EnsureGregorianRange(date);
		var lunarYear = date.Year;
		var yearMonths = GetLunarMonthsForEnvelope(lunarYear);
		if (date < yearMonths[0].StartGregorianDate)
		{
			lunarYear--;
			yearMonths = GetLunarMonthsForEnvelope(lunarYear);
		}
		var monthInfo = yearMonths.Last(m => m.StartGregorianDate <= date);
		var result = new LunarDate(date.DayNumber - monthInfo.StartGregorianDate.DayNumber + 1,
			monthInfo.LunarMonth, lunarYear, monthInfo.IsLeapMonth);
		timer.Stop();
		Debug.WriteLine($"GregorianToLunar {date}: {timer.Elapsed.TotalMilliseconds:F3} ms ({(cacheHit ? "year cache hit" : "year cache miss/initialization")}).");
		return result;
	}

	/// <inheritdoc />
	public DateOnly LunarToGregorian(int day, int month, int year, bool isLeapMonth = false)
	{
		var lunar = new LunarDate(day, month, year, isLeapMonth);
		if (year is < 1899 or > 2101) throw new ArgumentOutOfRangeException(nameof(year), "Supported lunar conversion envelope is 1899–2101.");
		var months = GetLunarMonthsForEnvelope(year);
		var info = months.SingleOrDefault(m => m.LunarMonth == lunar.Month && m.IsLeapMonth == lunar.IsLeapMonth)
			?? throw new ArgumentException($"Lunar month {month}{(isLeapMonth ? " (leap)" : string.Empty)} does not exist in {year}.", nameof(month));
		if (day > info.DaysInMonth) throw new ArgumentOutOfRangeException(nameof(day), $"Month {month} in {year} has {info.DaysInMonth} days.");
		var result = info.StartGregorianDate.AddDays(day - 1);
		EnsureGregorianRange(result);
		return result;
	}

	/// <inheritdoc />
	public bool IsLeapMonth(int month, int year)
	{
		if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
		return GetLunarMonths(year).Any(m => m.LunarMonth == month && m.IsLeapMonth);
	}

	/// <inheritdoc />
	public int GetDaysInMonth(int month, int year, bool isLeapMonth = false)
	{
		if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
		var info = GetLunarMonths(year).SingleOrDefault(m => m.LunarMonth == month && m.IsLeapMonth == isLeapMonth)
			?? throw new ArgumentException($"Lunar month {month}{(isLeapMonth ? " (leap)" : string.Empty)} does not exist in {year}.", nameof(month));
		return info.DaysInMonth;
	}

	/// <inheritdoc />
	public IReadOnlyList<LunarMonthInfo> GetLunarMonths(int year)
	{
		if (year is < 1900 or > 2100) throw new ArgumentOutOfRangeException(nameof(year), "Supported lunar years are 1900–2100.");
		return GetLunarMonthsForEnvelope(year);
	}

	private static IReadOnlyList<LunarMonthInfo> GetLunarMonthsForEnvelope(int year) => YearCache.GetOrAdd(year, CreateLunarYear);

	private static IReadOnlyList<LunarMonthInfo> CreateLunarYear(int year)
	{
		var timer = Stopwatch.StartNew();
		var month11Before = GetLunarMonth11(year - 1);
		var month11After = GetLunarMonth11(year);
		var kBefore = (int)Math.Round((month11Before - LunarEpoch) / SynodicMonth);
		var kAfter = (int)Math.Round((month11After - LunarEpoch) / SynodicMonth);
		var monthCount = kAfter - kBefore;
		if (monthCount is not (12 or 13)) throw new InvalidOperationException($"Astronomical month count for lunar year {year} was {monthCount}.");

		var firstMonthStart = NewMoonDay(kBefore + 2); // Skip months 11 and 12; begin at lunar month 1.
		var firstMonthStartDate = FromJulianDayNumber(firstMonthStart);
		var yearEndDate = FromJulianDayNumber(NewMoonDay(kBefore + 2 + monthCount));
		var principalTerms = GetPrincipalSolarTermDates(firstMonthStartDate, yearEndDate);
		var leapCandidates = monthCount == 13
			? Enumerable.Range(0, monthCount).Where(index => !principalTerms.Any(term =>
				term >= FromJulianDayNumber(NewMoonDay(kBefore + 2 + index)) &&
				term < FromJulianDayNumber(NewMoonDay(kBefore + 3 + index)))).ToArray()
			: Array.Empty<int>();
		if (monthCount == 13 && leapCandidates.Length == 0)
			throw new InvalidOperationException($"No month without a principal solar term was found in lunar year {year}.");
		// If two civil months both lack a principal term because two terms land
		// in one long month, the first such month after month 11 is intercalary.
		var leapIndex = leapCandidates.Length > 0 ? leapCandidates[0] : -1;
		var months = new LunarMonthInfo[monthCount];
		for (var index = 0; index < monthCount; index++)
		{
			var current = NewMoonDay(kBefore + 2 + index);
			var next = NewMoonDay(kBefore + 3 + index);
			var number = leapIndex >= 0 && index >= leapIndex ? index : index + 1;
			var isLeap = index == leapIndex;
			var startDate = FromJulianDayNumber(current);
			months[index] = new LunarMonthInfo(year, number, isLeap, startDate, next - current);
		}
		var result = Array.AsReadOnly(months);
		timer.Stop();
		Debug.WriteLine($"CreateLunarYear {year}: {timer.Elapsed.TotalMilliseconds:F3} ms (astronomical initialization).");
		return result;
	}

	private static int GetLunarMonth11(int gregorianYear)
	{
		var solstice = FindSolarTermDate(gregorianYear, 270);
		var k = (int)Math.Floor((ToJulianDayNumber(solstice) - LunarEpoch) / SynodicMonth);
		while (NewMoonDay(k + 1) <= ToJulianDayNumber(solstice)) k++;
		while (NewMoonDay(k) > ToJulianDayNumber(solstice)) k--;
		return NewMoonDay(k);
	}

	private static DateOnly FindSolarTermDate(int year, int targetLongitude)
	{
		var events = FindSolarTermEvents(new DateOnly(year, 12, 15), new DateOnly(year, 12, 25));
		return events.Single(e => e.Longitude == targetLongitude).Date;
	}

	private static HashSet<DateOnly> GetPrincipalSolarTermDates(DateOnly start, DateOnly end) =>
		FindSolarTermEvents(start, end).Where(e => e.Longitude % 30 == 0).Select(e => e.Date).ToHashSet();

	private static List<(int Longitude, DateOnly Date)> FindSolarTermEvents(DateOnly start, DateOnly end)
	{
		var events = new List<(int Longitude, DateOnly Date)>();
		var firstDay = ToJulianDayNumber(start) - 1;
		var lastDay = ToJulianDayNumber(end) + 1;
		for (var day = firstDay; day < lastDay; day++)
		{
			var firstJd = day - TimeZoneHours / 24.0;
			var secondJd = firstJd + 1.0;
			var firstLongitude = SolarLongitude(firstJd);
			var delta = NormalizeDegrees(SolarLongitude(secondJd) - firstLongitude);
			var unwrappedEnd = firstLongitude + delta;
			var target = ((int)Math.Floor(firstLongitude / 30.0) + 1) * 30;
			if (target > unwrappedEnd + 1e-8) continue;

			var low = firstJd;
			var high = secondJd;
			for (var iteration = 0; iteration < 45; iteration++)
			{
				var middle = (low + high) / 2;
				var longitude = SolarLongitude(middle);
				if (longitude < firstLongitude - 180) longitude += 360;
				if (longitude >= target) high = middle;
				else low = middle;
			}
			var eventJd = (low + high) / 2;
			var eventDay = (int)Math.Floor(eventJd + 0.5 + TimeZoneHours / 24.0);
			if (eventDay >= ToJulianDayNumber(start) && eventDay <= ToJulianDayNumber(end))
				events.Add((target % 360, FromJulianDayNumber(eventDay)));
		}
		return events;
	}

	private static int NewMoonDay(int k) => (int)Math.Floor(NewMoon(k) + 0.5 + TimeZoneHours / 24.0);

	/// <summary>Returns a Meeus Chapter 49 true new-moon instant, converted from TT to UT.</summary>
	private static double NewMoon(int k)
	{
		var t = k / 1236.85;
		var t2 = t * t;
		var t3 = t2 * t;
		var t4 = t3 * t;
		var jde = 2451550.09766 + 29.530588861 * k + 0.00015437 * t2 - 0.000000150 * t3 + 0.00000000073 * t4;
		var m = DegreesToRadians(2.5534 + 29.10535670 * k - 0.0000014 * t2 - 0.00000011 * t3);
		var mp = DegreesToRadians(201.5643 + 385.81693528 * k + 0.0107582 * t2 + 0.00001238 * t3 - 0.000000058 * t4);
		var f = DegreesToRadians(160.7108 + 390.67050284 * k - 0.0016118 * t2 - 0.00000227 * t3 + 0.000000011 * t4);
		var omega = DegreesToRadians(124.7746 - 1.56375588 * k + 0.0020672 * t2 + 0.00000215 * t3);
		var correction = -0.40720 * Math.Sin(mp) + 0.17241 * Eccentricity(t) * Math.Sin(m)
			+ 0.01608 * Math.Sin(2 * mp) + 0.01039 * Math.Sin(2 * f) + 0.00739 * Eccentricity(t) * Math.Sin(mp - m)
			- 0.00514 * Eccentricity(t) * Math.Sin(mp + m) + 0.00208 * Eccentricity(t) * Eccentricity(t) * Math.Sin(2 * m)
			- 0.00111 * Math.Sin(mp - 2 * f) - 0.00057 * Math.Sin(mp + 2 * f) + 0.00056 * Eccentricity(t) * Math.Sin(2 * mp + m)
			- 0.00042 * Math.Sin(3 * mp) + 0.00042 * Eccentricity(t) * Math.Sin(m + 2 * f) + 0.00038 * Eccentricity(t) * Math.Sin(m - 2 * f)
			- 0.00024 * Eccentricity(t) * Math.Sin(2 * mp - m) - 0.00017 * Math.Sin(omega) - 0.00007 * Math.Sin(mp + 2 * m)
			+ 0.00004 * Math.Sin(2 * mp - 2 * f) + 0.00004 * Math.Sin(3 * m) + 0.00003 * Math.Sin(mp + m - 2 * f)
			+ 0.00003 * Math.Sin(2 * mp + 2 * f) - 0.00003 * Math.Sin(mp + m + 2 * f) + 0.00003 * Math.Sin(mp - m + 2 * f)
			- 0.00002 * Math.Sin(mp - m - 2 * f) - 0.00002 * Math.Sin(3 * mp + m) + 0.00002 * Math.Sin(4 * mp);
		var planetary = 0.000325 * Math.Sin(DegreesToRadians(299.77 + 0.107408 * k - 0.009173 * t2))
			+ 0.000165 * Math.Sin(DegreesToRadians(251.88 + 0.016321 * k))
			+ 0.000164 * Math.Sin(DegreesToRadians(251.83 + 26.651886 * k))
			+ 0.000126 * Math.Sin(DegreesToRadians(349.42 + 36.412478 * k))
			+ 0.000110 * Math.Sin(DegreesToRadians(84.66 + 18.206239 * k))
			+ 0.000062 * Math.Sin(DegreesToRadians(141.74 + 53.303771 * k))
			+ 0.000060 * Math.Sin(DegreesToRadians(207.14 + 2.453732 * k))
			+ 0.000056 * Math.Sin(DegreesToRadians(154.84 + 7.306860 * k))
			+ 0.000047 * Math.Sin(DegreesToRadians(34.52 + 27.261239 * k))
			+ 0.000042 * Math.Sin(DegreesToRadians(207.19 + 0.121824 * k))
			+ 0.000040 * Math.Sin(DegreesToRadians(291.34 + 1.844379 * k))
			+ 0.000037 * Math.Sin(DegreesToRadians(161.72 + 24.198154 * k))
			+ 0.000035 * Math.Sin(DegreesToRadians(239.56 + 25.513099 * k))
			+ 0.000023 * Math.Sin(DegreesToRadians(331.55 + 3.592518 * k));
		return jde + correction + planetary - DeltaTSeconds(JulianYear(jde)) / 86400.0;
	}

	private static double Eccentricity(double t) => 1 - 0.002516 * t - 0.0000074 * t * t;

	private static double SolarLongitude(double jd)
	{
		var t = (jd - 2451545.0) / 36525.0;
		var t2 = t * t;
		var l0 = NormalizeDegrees(280.46646 + 36000.76983 * t + 0.0003032 * t2);
		var m = DegreesToRadians(NormalizeDegrees(357.52911 + 35999.05029 * t - 0.0001537 * t2 + t * t2 / 24490000.0));
		var c = (1.914602 - 0.004817 * t - 0.000014 * t2) * Math.Sin(m)
			+ (0.019993 - 0.000101 * t) * Math.Sin(2 * m) + 0.000289 * Math.Sin(3 * m);
		var omega = DegreesToRadians(125.04 - 1934.136 * t);
		var apparentLongitude = l0 + c - 0.00569 - 0.00478 * Math.Sin(omega);
		return NormalizeDegrees(apparentLongitude);
	}

	private static double DeltaTSeconds(double year)
	{
		var t = year - 2000;
		if (year >= 2005 && year < 2050) return 62.92 + 0.32217 * t + 0.005589 * t * t;
		if (year >= 2050) return -20 + 32 * Math.Pow((year - 1820) / 100, 2) - 0.5628 * (2150 - year);
		if (year >= 1986) return 63.86 + 0.3345 * t - 0.060374 * t * t + 0.0017275 * Math.Pow(t, 3) + 0.000651814 * Math.Pow(t, 4) + 0.00002373599 * Math.Pow(t, 5);
		if (year >= 1961)
		{
			var u = year - 1975;
			return 45.45 + 1.067 * u - u * u / 260 - Math.Pow(u, 3) / 718;
		}
		if (year >= 1941)
		{
			var u = year - 1950;
			return 29.07 + 0.407 * u - u * u / 233 + Math.Pow(u, 3) / 2547;
		}
		if (year >= 1920)
		{
			var u = year - 1920;
			return 21.20 + 0.84493 * u - 0.076100 * u * u + 0.0020936 * Math.Pow(u, 3);
		}
		if (year >= 1860)
		{
			var u = year - 1860;
			return 7.62 + 0.5737 * u - 0.251754 * u * u + 0.01680668 * Math.Pow(u, 3)
				- 0.0004473624 * Math.Pow(u, 4) + Math.Pow(u, 5) / 233174.0;
		}
		var v = year - 1900;
		return -2.79 + 1.494119 * v - 0.0598939 * v * v + 0.0061966 * Math.Pow(v, 3) - 0.000197 * Math.Pow(v, 4);
	}

	private static double JulianYear(double jd) => 2000 + (jd - 2451545.0) / 365.2425;
	private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
	private static double NormalizeDegrees(double degrees) => ((degrees % 360.0) + 360.0) % 360.0;
	private static int ToJulianDayNumber(DateOnly date) => date.DayNumber + 1721426;
	private static DateOnly FromJulianDayNumber(int jd) => DateOnly.FromDayNumber(jd - 1721426);

	private static void EnsureGregorianRange(DateOnly date)
	{
		if (date.Year is < 1900 or > 2101) throw new ArgumentOutOfRangeException(nameof(date), "Supported Gregorian years are 1900–2100, with the final lunar year extending briefly into 2101.");
		if (date.Year == 2101 && date >= GetLunarMonthsForEnvelope(2101)[0].StartGregorianDate)
			throw new ArgumentOutOfRangeException(nameof(date), "Supported dates end at the close of lunar year 2100.");
	}
}
