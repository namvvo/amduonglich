namespace VietnameseLunarCalendar.Infrastructure.CalendarEvents;

/// <summary>Indicates that locally persisted calendar event data cannot be read safely.</summary>
public sealed class CalendarEventPersistenceException : IOException
{
	/// <summary>Initializes the exception with a user-friendly persistence error.</summary>
	public CalendarEventPersistenceException(string message) : base(message) { }

	/// <summary>Initializes the exception with a user-friendly persistence error and its cause.</summary>
	public CalendarEventPersistenceException(string message, Exception innerException) : base(message, innerException) { }
}
