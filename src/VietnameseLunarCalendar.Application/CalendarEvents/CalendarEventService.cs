using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;
using System.Diagnostics;

namespace VietnameseLunarCalendar.Application.CalendarEvents;

/// <summary>Applies calendar recurrence rules independently of the user interface.</summary>
public sealed class CalendarEventService(ICalendarEventRepository repository, ILunarCalendarService lunarCalendar) : ICalendarEventService
{
	public async Task<CalendarEvent> CreateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
	{
		Validate(calendarEvent);
		if (await repository.GetByIdAsync(calendarEvent.Id, cancellationToken) is not null)
			throw new ArgumentException("An event with this ID already exists.", nameof(calendarEvent));
		var now = DateTime.UtcNow;
		var created = calendarEvent with { CreatedAt = now, UpdatedAt = now };
		await repository.SaveAsync(created, cancellationToken);
		return created;
	}

	public async Task<CalendarEvent> UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
	{
		Validate(calendarEvent);
		var existing = await repository.GetByIdAsync(calendarEvent.Id, cancellationToken)
			?? throw new KeyNotFoundException($"Event {calendarEvent.Id} was not found.");
		var updated = calendarEvent with { CreatedAt = existing.CreatedAt, UpdatedAt = DateTime.UtcNow };
		await repository.SaveAsync(updated, cancellationToken);
		return updated;
	}

	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		if (await repository.GetByIdAsync(id, cancellationToken) is null)
			throw new KeyNotFoundException($"Event {id} was not found.");
		await repository.DeleteAsync(id, cancellationToken);
	}

	public async Task<CalendarEvent> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		await repository.GetByIdAsync(id, cancellationToken) ?? throw new KeyNotFoundException($"Event {id} was not found.");

	public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var timer = Stopwatch.StartNew();
		var result = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
		timer.Stop();
		Debug.WriteLine($"PERF ICalendarEventService.GetAllAsync: {timer.Elapsed.TotalMilliseconds:F3} ms; {result.Count} records.");
		return result;
	}

	public async Task<IReadOnlyList<CalendarEvent>> GetEventsForDateAsync(DateOnly gregorianDate, CancellationToken cancellationToken = default)
	{
		var lunarDate = lunarCalendar.GregorianToLunar(gregorianDate);
		var events = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
		return events.Where(e => Applies(e, gregorianDate, lunarDate)).DistinctBy(e => e.Id).ToArray();
	}

	public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<CalendarEvent>>> GetEventsForMonthAsync(int year, int month, CancellationToken cancellationToken = default)
	{
		var first = new DateOnly(year, month, 1);
		var events = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var result = new Dictionary<DateOnly, IReadOnlyList<CalendarEvent>>();
		for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var date = first.AddDays(day - 1);
			var lunar = lunarCalendar.GregorianToLunar(date);
			var applicable = events.Where(e => Applies(e, date, lunar)).DistinctBy(e => e.Id).ToArray();
			if (applicable.Length > 0) result[date] = applicable;
		}
		return result;
	}

	private static bool Applies(CalendarEvent e, DateOnly date, LunarDate lunar)
	{
		if (e.CalendarType == EventCalendarType.Gregorian)
			return e.Day == date.Day && e.Month == date.Month && (e.RecurrenceType == EventRecurrenceType.Yearly || e.Year == date.Year);
		return e.Day == lunar.Day && e.Month == lunar.Month && e.IsLeapMonth == lunar.IsLeapMonth &&
			(e.RecurrenceType == EventRecurrenceType.Yearly || e.Year == lunar.Year);
	}

	private void Validate(CalendarEvent e)
	{
		if (e.Id == Guid.Empty) throw new ArgumentException("Event ID is required.", nameof(e));
		if (string.IsNullOrWhiteSpace(e.Title) || e.Title.Trim().Length > 120) throw new ArgumentException("Title must contain 1 to 120 characters.", nameof(e));
		if (!Enum.IsDefined(e.CalendarType) || !Enum.IsDefined(e.RecurrenceType)) throw new ArgumentException("Calendar and recurrence types are invalid.", nameof(e));
		if (e.ReminderDaysBefore is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(e), "Reminder must be between 0 and 30 days.");
		if (e.Description?.Length > 2000 || e.Category?.Length > 80 || e.Color?.Length > 32) throw new ArgumentException("Optional event text exceeds its allowed length.", nameof(e));
		if (e.CalendarType == EventCalendarType.Gregorian)
		{
			if (e.Day < 1 || e.Month is < 1 or > 12 || e.IsLeapMonth) throw new ArgumentException("Gregorian event date is invalid.", nameof(e));
			if (e.RecurrenceType == EventRecurrenceType.Yearly && e.Year is not null || e.RecurrenceType == EventRecurrenceType.OneTime && e.Year is null)
				throw new ArgumentException("Only one-time Gregorian events have a year.", nameof(e));
			if (e.Year is int year && (year < 1 || year > 9999 || e.Day > DateTime.DaysInMonth(year, e.Month))) throw new ArgumentException("Gregorian event date is invalid.", nameof(e));
		}
		else
		{
			if (e.Day is < 1 or > 30 || e.Month is < 1 or > 12) throw new ArgumentException("Lunar event date is invalid.", nameof(e));
			if (e.RecurrenceType == EventRecurrenceType.Yearly && e.Year is not null || e.RecurrenceType == EventRecurrenceType.OneTime && e.Year is null)
				throw new ArgumentException("Only one-time lunar events have a year.", nameof(e));
			if (e.Year is int year && year is < 1900 or > 2100) throw new ArgumentOutOfRangeException(nameof(e), "Lunar year must be between 1900 and 2100.");
			if (e.IsLeapMonth && e.Year is int y && !lunarCalendar.IsLeapMonth(e.Month, y)) throw new ArgumentException("The selected lunar month is not a leap month in that year.", nameof(e));
			if (e.Year is int targetYear && !e.IsLeapMonth && e.Day > lunarCalendar.GetDaysInMonth(e.Month, targetYear)) throw new ArgumentException("Lunar day does not exist in the selected month.", nameof(e));
			if (e.Year is int leapYear && e.IsLeapMonth && e.Day > lunarCalendar.GetDaysInMonth(e.Month, leapYear, true)) throw new ArgumentException("Lunar day does not exist in the selected leap month.", nameof(e));
		}
	}
}
