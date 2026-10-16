using VietnameseLunarCalendar.App.Views;

namespace VietnameseLunarCalendar.App;

public partial class AppShell : Shell
{
	public AppShell(CalendarPage calendarPage)
	{
		InitializeComponent();
		var calendarContent = new ShellContent
		{
			Title = "Lịch",
			Route = "calendar",
			Content = calendarPage
		};
		Shell.SetNavBarIsVisible(calendarContent, false);
		Items.Add(calendarContent);
		CurrentItem = Items[0];
		Routing.RegisterRoute("events", typeof(CalendarEventsPage));
	}
}
