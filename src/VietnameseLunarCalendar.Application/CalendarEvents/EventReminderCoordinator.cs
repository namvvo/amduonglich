using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Application.CalendarEvents;

/// <summary>Builds the next reminder for each event using Vietnam's fixed UTC+7 civil calendar.</summary>
public sealed class EventReminderCoordinator(ICalendarEventService events, ILunarCalendarService lunar,
	ILocalNotificationPlatform platform)
{
	private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

	public bool AreNotificationsEnabled() => platform.AreNotificationsEnabled();
	public Task<bool> RequestPermissionAsync() => platform.RequestPermissionAsync();
	public Task OpenNotificationSettingsAsync() => platform.OpenNotificationSettingsAsync();

	public async Task ReconcileAsync(CancellationToken cancellationToken = default)
	{
		var allEvents = await events.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var nowUtc = DateTimeOffset.UtcNow;
		var reminders = allEvents.Where(item => item.IsReminderEnabled)
			.Select(item => GetNextReminder(item, nowUtc))
			.Where(item => item is not null).Cast<EventReminder>().ToArray();
		await platform.ReplaceSchedulesAsync(reminders, cancellationToken).ConfigureAwait(false);
	}

	public async Task NotifyAndReconcileAsync(Guid eventId, DateOnly eventDate, CancellationToken cancellationToken = default)
	{
		var item = await events.GetByIdAsync(eventId, cancellationToken).ConfigureAwait(false);
		if (!item.IsReminderEnabled || !platform.AreNotificationsEnabled())
		{
			await ReconcileAsync(cancellationToken).ConfigureAwait(false);
			return;
		}

		var reminder = new EventReminder(item.Id, item.Title, eventDate,
			DateTimeOffset.UtcNow, item.ReminderDaysBefore, item.RecurrenceType == EventRecurrenceType.Yearly);
		await platform.ShowNotificationAsync(reminder, cancellationToken).ConfigureAwait(false);
		await ReconcileAsync(cancellationToken).ConfigureAwait(false);
	}

	public EventReminder? GetNextReminder(CalendarEvent item, DateTimeOffset nowUtc)
	{
		if (!item.IsReminderEnabled || item.ReminderDaysBefore is < 0 or > 30) return null;
		var todayInVietnam = DateOnly.FromDateTime(nowUtc.ToOffset(VietnamOffset).DateTime);
		var firstYear = item.RecurrenceType == EventRecurrenceType.OneTime ? item.Year!.Value :
			item.CalendarType == EventCalendarType.Lunar ? lunar.GregorianToLunar(todayInVietnam).Year : todayInVietnam.Year;
		var lastYear = item.RecurrenceType == EventRecurrenceType.OneTime
			? firstYear
			: Math.Min(2100, firstYear + 3);

		for (var year = firstYear; year <= lastYear; year++)
		{
			DateOnly eventDate;
			try
			{
				if (item.CalendarType == EventCalendarType.Gregorian)
				{
					if (item.Day > DateTime.DaysInMonth(year, item.Month)) continue;
					eventDate = new DateOnly(year, item.Month, item.Day);
				}
				else
				{
					var month = lunar.GetLunarMonths(year).SingleOrDefault(m => m.LunarMonth == item.Month && m.IsLeapMonth == item.IsLeapMonth);
					if (month is null || item.Day > month.DaysInMonth) continue;
					eventDate = lunar.LunarToGregorian(item.Day, item.Month, year, item.IsLeapMonth);
				}
			}
			catch (ArgumentException) { continue; }
			catch (InvalidOperationException) { continue; }

			var triggerDate = eventDate.AddDays(-item.ReminderDaysBefore);
			var triggerLocal = triggerDate.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);
			var triggerUtc = new DateTimeOffset(triggerLocal, VietnamOffset).ToUniversalTime();
			if (triggerUtc <= nowUtc) continue;
			return new EventReminder(item.Id, item.Title, eventDate, triggerUtc,
				item.ReminderDaysBefore, item.RecurrenceType == EventRecurrenceType.Yearly);
		}

		return null;
	}
}
