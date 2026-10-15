using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;
using VietnameseLunarCalendar.App.ViewModels;

namespace VietnameseLunarCalendar.App.Views;

public partial class CalendarPage : ContentPage
{
	private readonly CalendarViewModel viewModel;
	private readonly CalendarCell[] cells;
	private IReadOnlyList<CalendarDayViewModel>? renderedDays;
	private bool isObservingViewModel;

	public CalendarPage(CalendarViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;

		var creationTimer = Stopwatch.StartNew();
		cells = new CalendarCell[42];
		for (var index = 0; index < cells.Length; index++)
		{
			var gregorianLabel = new Label
			{
				FontSize = 15,
				FontAttributes = FontAttributes.Bold,
				HorizontalTextAlignment = TextAlignment.Center
			};
			var lunarLabel = new Label
			{
				FontSize = 9,
				HorizontalTextAlignment = TextAlignment.Center
			};
			var content = new VerticalStackLayout
			{
				Spacing = 0,
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.Center,
				Children = { gregorianLabel, lunarLabel }
			};
			var cell = new Border
			{
				Margin = 2,
				Padding = 1,
				HeightRequest = 46,
				Stroke = Colors.Transparent,
				StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
				Content = content
			};
			var tap = new TapGestureRecognizer { Command = viewModel.SelectDateCommand };
			cell.GestureRecognizers.Add(tap);
			cells[index] = new CalendarCell(cell, gregorianLabel, lunarLabel, tap);
			CalendarDaysGrid.Add(cell, index % 7, index / 7);
		}
		creationTimer.Stop();
		Debug.WriteLine($"Calendar initial creation: {creationTimer.Elapsed.TotalMilliseconds:F3} ms for 42 cells (synchronous control construction and grid insertion; Android layout/rendering is asynchronous).");
		Debug.Assert(CalendarDaysGrid.Children.Count == 42);
		UpdateMonthCells(viewModel.Days);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (!isObservingViewModel)
		{
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
			isObservingViewModel = true;
		}
		RefreshCalendarGridIfNeeded();
	}

	protected override void OnDisappearing()
	{
		if (isObservingViewModel)
		{
			viewModel.PropertyChanged -= OnViewModelPropertyChanged;
			isObservingViewModel = false;
		}
		base.OnDisappearing();
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(CalendarViewModel.Days))
			Dispatcher.Dispatch(RefreshCalendarGridIfNeeded);
	}

	private void RefreshCalendarGridIfNeeded()
	{
		if (!ReferenceEquals(renderedDays, viewModel.Days))
			UpdateCalendarGrid(viewModel.Days);
	}

	private void UpdateCalendarGrid(IReadOnlyList<CalendarDayViewModel> days)
	{
		var isSameMonth = renderedDays is not null && SameDates(renderedDays, days);
		if (!isSameMonth)
		{
			UpdateMonthCells(days);
			return;
		}

		var timer = Stopwatch.StartNew();
		for (var i = 0; i < cells.Length; i++)
		{
			if (!Equals(renderedDays![i], days[i]))
				UpdateCell(cells[i], days[i]);
		}
		renderedDays = days;
		timer.Stop();
		Debug.WriteLine($"Calendar date selection UI update: {timer.Elapsed.TotalMilliseconds:F3} ms for changed cells (synchronous property assignment; Android layout/rendering is asynchronous).");
	}

	private void UpdateMonthCells(IReadOnlyList<CalendarDayViewModel> days)
	{
		var timer = Stopwatch.StartNew();
		for (var i = 0; i < cells.Length; i++)
			UpdateCell(cells[i], i < days.Count ? days[i] : CalendarDayViewModel.Blank);
		renderedDays = days;
		timer.Stop();
		Debug.WriteLine($"Calendar existing-cell month update: {timer.Elapsed.TotalMilliseconds:F3} ms for 42 cells (synchronous property assignment; Android layout/rendering is asynchronous).");
		Debug.Assert(CalendarDaysGrid.Children.Count == 42);
	}

	private static bool SameDates(IReadOnlyList<CalendarDayViewModel> left, IReadOnlyList<CalendarDayViewModel> right)
	{
		if (left.Count != right.Count) return false;
		for (var i = 0; i < left.Count; i++)
			if (left[i].Date != right[i].Date) return false;
		return true;
	}

	private static void UpdateCell(CalendarCell cell, CalendarDayViewModel day)
	{
		cell.GregorianLabel.Text = day.GregorianDay;
		cell.LunarLabel.Text = day.LunarDay;
		cell.Border.BackgroundColor = day.BackgroundColor;
		cell.GregorianLabel.TextColor = day.TextColor;
		cell.LunarLabel.TextColor = day.TextColor;
		cell.Tap.CommandParameter = day.Date.HasValue ? day : null;
		cell.Border.IsEnabled = day.Date.HasValue;
	}

	private sealed record CalendarCell(Border Border, Label GregorianLabel, Label LunarLabel, TapGestureRecognizer Tap);
}
