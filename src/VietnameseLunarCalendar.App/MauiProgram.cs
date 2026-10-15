using Microsoft.Extensions.Logging;
using VietnameseLunarCalendar.App.ViewModels;
using VietnameseLunarCalendar.App.Views;
using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Infrastructure.LunarCalendar;

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
		builder.Services.AddTransient<CalendarPage>();
		builder.Services.AddTransient<CalendarViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
