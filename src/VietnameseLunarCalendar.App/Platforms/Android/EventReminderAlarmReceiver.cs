using Android.App;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using VietnameseLunarCalendar.Application.CalendarEvents;

namespace VietnameseLunarCalendar.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class EventReminderAlarmReceiver : BroadcastReceiver
{
	public const string EventIdExtra = "event-id";
	public const string EventDateExtra = "event-date";

	public override void OnReceive(Context? context, Intent? intent)
	{
		if (intent is null || !Guid.TryParse(intent.GetStringExtra(EventIdExtra), out var id) ||
			!DateOnly.TryParseExact(intent.GetStringExtra(EventDateExtra), "yyyy-MM-dd", out var date)) return;
		var pendingResult = GoAsync();
		_ = HandleAsync(id, date, pendingResult);
	}

	private static async Task HandleAsync(Guid id, DateOnly date, PendingResult pendingResult)
	{
		try
		{
			var coordinator = IPlatformApplication.Current?.Services.GetService<EventReminderCoordinator>();
			if (coordinator is not null) await coordinator.NotifyAndReconcileAsync(id, date);
		}
		catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Event reminder alarm failed: {exception}"); }
		finally { pendingResult.Finish(); }
	}
}
