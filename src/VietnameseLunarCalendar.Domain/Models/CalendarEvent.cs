namespace VietnameseLunarCalendar.Domain.Models;

public enum EventCalendarType { Lunar, Gregorian }
public enum EventRecurrenceType { Yearly, OneTime }

/// <summary>A local calendar event and its recurrence fields.</summary>
public sealed record CalendarEvent
{
	public Guid Id { get; init; } = Guid.NewGuid();
	public string Title { get; init; } = string.Empty;
	public string? Description { get; init; }
	public EventCalendarType CalendarType { get; init; }
	public int Day { get; init; }
	public int Month { get; init; }
	public bool IsLeapMonth { get; init; }
	public int? Year { get; init; }
	public EventRecurrenceType RecurrenceType { get; init; }
	public string? Category { get; init; }
	public string? Color { get; init; }
	public bool IsReminderEnabled { get; init; }
	public int ReminderDaysBefore { get; init; }
	public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
	public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}
