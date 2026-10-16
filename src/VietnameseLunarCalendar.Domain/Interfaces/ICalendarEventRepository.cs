using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Domain.Interfaces;

/// <summary>Stores locally managed calendar events.</summary>
public interface ICalendarEventRepository
{
	Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task SaveAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
