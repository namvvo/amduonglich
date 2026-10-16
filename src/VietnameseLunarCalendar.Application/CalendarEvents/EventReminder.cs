namespace VietnameseLunarCalendar.Application.CalendarEvents;

/// <summary>A single upcoming reminder occurrence, expressed in UTC for scheduling.</summary>
public sealed record EventReminder(Guid EventId, string Title, DateOnly EventDate, DateTimeOffset TriggerAtUtc,
	int ReminderDaysBefore, bool IsRecurring);
