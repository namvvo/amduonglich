using VietnameseLunarCalendar.App.ViewModels;

namespace VietnameseLunarCalendar.App.Views;

public partial class CalendarPage : ContentPage
{
	public CalendarPage(CalendarViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
