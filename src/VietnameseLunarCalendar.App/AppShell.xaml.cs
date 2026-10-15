using VietnameseLunarCalendar.App.Views;

namespace VietnameseLunarCalendar.App;

public partial class AppShell : Shell
{
	public AppShell(CalendarPage calendarPage)
	{
		InitializeComponent();
		Items.Add(new ShellContent
		{
			Title = "Lịch",
			Route = "calendar",
			Content = calendarPage
		});
	}
}
