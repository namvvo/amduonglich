using System.Globalization;
using System.Security.Cryptography;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using VietnameseLunarCalendar.Application.CalendarEvents;

namespace VietnameseLunarCalendar.App.Platforms.Android;

/// <summary>Schedules inexact, wake-up alarms without requesting exact-alarm access.</summary>
public sealed class AndroidLocalNotificationPlatform : ILocalNotificationPlatform
{
	public const string ChannelId = "event-reminders";
	private const string ScheduledIdsKey = "scheduled-event-reminder-ids";
	private readonly Context context = global::Android.App.Application.Context;

	public AndroidLocalNotificationPlatform() => EnsureChannel();

	public bool AreNotificationsEnabled()
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
			ContextCompat.CheckSelfPermission(context, global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
			return false;
		return NotificationManagerCompat.From(context).AreNotificationsEnabled();
	}

	public async Task<bool> RequestPermissionAsync()
	{
		if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
		{
			var result = await Permissions.RequestAsync<Permissions.PostNotifications>();
			if (result != PermissionStatus.Granted) return false;
		}
		return NotificationManagerCompat.From(context).AreNotificationsEnabled();
	}

	public Task OpenNotificationSettingsAsync()
	{
		var intent = new Intent(Settings.ActionAppNotificationSettings);
		intent.PutExtra(Settings.ExtraAppPackage, context.PackageName);
		intent.AddFlags(ActivityFlags.NewTask);
		try { context.StartActivity(intent); }
		catch (ActivityNotFoundException)
		{
			var fallback = new Intent(Settings.ActionApplicationDetailsSettings,
				global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
			fallback.AddFlags(ActivityFlags.NewTask);
			context.StartActivity(fallback);
		}
		return Task.CompletedTask;
	}

	public Task ReplaceSchedulesAsync(IReadOnlyList<EventReminder> reminders, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var preferences = Preferences.Default;
		var previousIds = preferences.Get(ScheduledIdsKey, string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries)
			.Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
			.Where(id => id != Guid.Empty).ToArray();
		foreach (var id in previousIds) CancelAlarm(id);

		var activeIds = new List<string>();
		if (AreNotificationsEnabled())
		{
			EnsureChannel();
			foreach (var reminder in reminders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ScheduleAlarm(reminder);
				activeIds.Add(reminder.EventId.ToString("N"));
			}
		}
		preferences.Set(ScheduledIdsKey, string.Join(',', activeIds));
		return Task.CompletedTask;
	}

	public Task ShowNotificationAsync(EventReminder reminder, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!AreNotificationsEnabled()) return Task.CompletedTask;
		EnsureChannel();
		var dateText = reminder.EventDate.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
		var text = $"{reminder.Title} – diễn ra ngày {dateText}.";
		var notification = new NotificationCompat.Builder(context, ChannelId)
			.SetSmallIcon(Resource.Mipmap.appicon)
			.SetContentTitle("Nhắc nhở sự kiện")
			.SetContentText(text)
			.SetStyle(new NotificationCompat.BigTextStyle().BigText(text))
			.SetAutoCancel(true)
			.SetPriority((int)NotificationPriority.Default)
			.Build();
		NotificationManagerCompat.From(context).Notify(StableId(reminder.EventId), notification);
		return Task.CompletedTask;
	}

	private void ScheduleAlarm(EventReminder reminder)
	{
		var intent = AlarmIntent(reminder.EventId);
		intent.PutExtra(EventReminderAlarmReceiver.EventDateExtra, reminder.EventDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		var pendingIntent = PendingIntent.GetBroadcast(context, StableId(reminder.EventId), intent,
			PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
		var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService)
			?? throw new InvalidOperationException("Android AlarmManager is unavailable.");
		var triggerAt = reminder.TriggerAtUtc.ToUnixTimeMilliseconds();
		if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
			alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAt, pendingIntent);
		else
			alarmManager.Set(AlarmType.RtcWakeup, triggerAt, pendingIntent);
	}

	private void CancelAlarm(Guid eventId)
	{
		var pendingIntent = PendingIntent.GetBroadcast(context, StableId(eventId), AlarmIntent(eventId),
			PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
		if (pendingIntent is null) return;
		(context.GetSystemService(Context.AlarmService) as AlarmManager)?.Cancel(pendingIntent);
		pendingIntent.Cancel();
	}

	private Intent AlarmIntent(Guid eventId)
	{
		var intent = new Intent(context, typeof(EventReminderAlarmReceiver));
		intent.SetData(global::Android.Net.Uri.Parse($"amduonglich://event-reminder/{eventId:N}"));
		return intent;
	}

	private void EnsureChannel()
	{
		if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
		var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
		manager?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Nhắc nhở sự kiện", NotificationImportance.Default)
		{
			Description = "Thông báo nhắc các sự kiện trong lịch của bạn."
		});
	}

	private static int StableId(Guid eventId)
	{
		var hash = SHA256.HashData(eventId.ToByteArray());
		return BitConverter.ToInt32(hash, 0) & 0x7fffffff;
	}
}
