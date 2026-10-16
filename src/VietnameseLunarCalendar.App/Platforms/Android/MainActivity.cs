using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.View;

namespace VietnameseLunarCalendar.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	protected override void OnPostResume()
	{
		base.OnPostResume();
		var application = Microsoft.Maui.Controls.Application.Current;
		var isDark = application?.UserAppTheme == AppTheme.Dark ||
			(application?.UserAppTheme == AppTheme.Unspecified && application?.RequestedTheme == AppTheme.Dark);
		ApplyStatusBarTheme(isDark);
	}

	public static void ApplyStatusBarTheme(bool isDark)
	{
		ApplyStatusBarIcons(isDark);
	}

	public static void ApplyStatusBarIcons(bool lightIcons)
	{
		var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
		if (window?.DecorView is not { } decorView) return;

		var controller = WindowCompat.GetInsetsController(window!, decorView);
		controller!.AppearanceLightStatusBars = !lightIcons;
	}
}
