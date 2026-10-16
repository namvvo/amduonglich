using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Domain.Interfaces;

/// <summary>Validates and resolves event CRUD and date applicability.</summary>
public interface ICalendarEventService
{
	Task<CalendarEvent> CreateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
	Task<CalendarEvent> UpdateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
	Task<CalendarEvent> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<CalendarEvent>> GetEventsForDateAsync(DateOnly gregorianDate, CancellationToken cancellationToken = default);
	Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<CalendarEvent>>> GetEventsForMonthAsync(int year, int month, CancellationToken cancellationToken = default);
}
