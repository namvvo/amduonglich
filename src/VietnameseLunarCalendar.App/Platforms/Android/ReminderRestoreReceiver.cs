using Android.App;
using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using VietnameseLunarCalendar.Application.CalendarEvents;

namespace VietnameseLunarCalendar.App.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted, Intent.ActionTimeChanged, Intent.ActionTimezoneChanged,
	"android.intent.action.DATE_CHANGED", Intent.ActionMyPackageReplaced])]
public sealed class ReminderRestoreReceiver : BroadcastReceiver
{
	public override void OnReceive(Context? context, Intent? intent)
	{
		var pendingResult = GoAsync();
		_ = RestoreAsync(pendingResult);
	}

	private static async Task RestoreAsync(PendingResult pendingResult)
	{
		try
		{
			var coordinator = IPlatformApplication.Current?.Services.GetService<EventReminderCoordinator>();
			if (coordinator is not null) await coordinator.ReconcileAsync();
		}
		catch (Exception exception) { System.Diagnostics.Debug.WriteLine($"Event reminder restore failed: {exception}"); }
		finally { pendingResult.Finish(); }
	}
}
