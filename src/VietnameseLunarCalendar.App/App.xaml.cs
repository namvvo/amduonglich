using Microsoft.Extensions.DependencyInjection;

using Microsoft.Maui.Storage;
using VietnameseLunarCalendar.Application.CalendarEvents;
using System.Diagnostics;

namespace VietnameseLunarCalendar.App;

public partial class App : Microsoft.Maui.Controls.Application
{
	private const string ThemePreferenceKey = "user-app-theme";
	private readonly AppShell _appShell;
	private readonly EventReminderCoordinator reminders;

	public App(AppShell appShell, EventReminderCoordinator reminders)
	{
		_appShell = appShell;
		this.reminders = reminders;
		InitializeComponent();
		UserAppTheme = Preferences.Default.Get(ThemePreferenceKey, string.Empty) switch
		{
			"Light" => AppTheme.Light,
			"Dark" => AppTheme.Dark,
			_ => AppTheme.Unspecified
		};
	}

	protected override void OnStart() => _ = ReconcileRemindersAsync("app start");

	protected override void OnResume() => _ = ReconcileRemindersAsync("app resume");

	private async Task ReconcileRemindersAsync(string reason)
	{
		try
		{
			await reminders.ReconcileAsync();
			Debug.WriteLine($"Event reminder schedules reconciled ({reason}).");
		}
		catch (Exception exception)
		{
			Debug.WriteLine($"Event reminder reconciliation failed ({reason}): {exception}");
		}
	}

	public void ToggleTheme()
	{
		var currentTheme = UserAppTheme == AppTheme.Unspecified ? RequestedTheme : UserAppTheme;
		var nextTheme = currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
		UserAppTheme = nextTheme;
		Preferences.Default.Set(ThemePreferenceKey, nextTheme.ToString());
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell);
	}
}
