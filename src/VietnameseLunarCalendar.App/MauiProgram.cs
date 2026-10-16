using Microsoft.Extensions.Logging;
using VietnameseLunarCalendar.App.ViewModels;
using VietnameseLunarCalendar.App.Views;
using VietnameseLunarCalendar.Application.CalendarEvents;
using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Infrastructure.LunarCalendar;
using VietnameseLunarCalendar.Infrastructure.CalendarEvents;

namespace VietnameseLunarCalendar.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<ILunarCalendarService, VietnameseLunarCalendarService>();
		builder.Services.AddSingleton<ICalendarEventRepository>(_ => new JsonCalendarEventRepository(FileSystem.AppDataDirectory));
		builder.Services.AddSingleton<ICalendarEventService, CalendarEventService>();
		builder.Services.AddSingleton<EventReminderCoordinator>();
#if ANDROID
		builder.Services.AddSingleton<ILocalNotificationPlatform, VietnameseLunarCalendar.App.Platforms.Android.AndroidLocalNotificationPlatform>();
#endif
		builder.Services.AddTransient<CalendarPage>();
		builder.Services.AddSingleton<CalendarEventsPage>();
		builder.Services.AddTransient<CalendarViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
