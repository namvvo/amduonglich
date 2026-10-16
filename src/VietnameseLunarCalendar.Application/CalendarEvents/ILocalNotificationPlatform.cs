namespace VietnameseLunarCalendar.Application.CalendarEvents;

/// <summary>Platform boundary for local notification permissions and alarm scheduling.</summary>
public interface ILocalNotificationPlatform
{
	bool AreNotificationsEnabled();
	Task<bool> RequestPermissionAsync();
	Task OpenNotificationSettingsAsync();
	Task ReplaceSchedulesAsync(IReadOnlyList<EventReminder> reminders, CancellationToken cancellationToken = default);
	Task ShowNotificationAsync(EventReminder reminder, CancellationToken cancellationToken = default);
}
