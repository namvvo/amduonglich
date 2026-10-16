using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Maui.Controls.Shapes;
using VietnameseLunarCalendar.App.ViewModels;
using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.App.Views;

public partial class CalendarPage : ContentPage
{
	private readonly CalendarViewModel viewModel;
	private readonly CalendarCell[] cells;
	private IReadOnlyList<CalendarDayViewModel>? renderedDays;
	private bool isObservingViewModel;
	private bool hasAppeared;
	private bool isNavigatingToEvents;

	public CalendarPage(CalendarViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;
		if (Microsoft.Maui.Controls.Application.Current is App app)
		{
			app.RequestedThemeChanged += OnRequestedThemeChanged;
			UpdateThemeToggle();
		}

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
			var eventIndicator = new Label
			{
				FontSize = 8,
				HorizontalTextAlignment = TextAlignment.Center,
				HeightRequest = 10
			};
			var content = new VerticalStackLayout
			{
				Spacing = 0,
				VerticalOptions = LayoutOptions.Center,
				HorizontalOptions = LayoutOptions.Center,
				Children = { gregorianLabel, lunarLabel, eventIndicator }
			};
			var cell = new Border
			{
				Margin = 2,
				Padding = 1,
				HeightRequest = 46,
				StrokeThickness = 1,
				Stroke = Colors.Transparent,
				StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
				Content = content
			};
			var tap = new TapGestureRecognizer { Command = viewModel.SelectDateCommand };
			cell.GestureRecognizers.Add(tap);
			cells[index] = new CalendarCell(cell, gregorianLabel, lunarLabel, eventIndicator, tap);
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
		RefreshThemeAwareContent();
		if (hasAppeared) viewModel.RefreshEvents();
		hasAppeared = true;
		if (!isObservingViewModel)
		{
			viewModel.PropertyChanged += OnViewModelPropertyChanged;
			isObservingViewModel = true;
		}
		RefreshCalendarGridIfNeeded();
		RefreshSelectedEventCards();
	}

	private async void OnAddEventClicked(object? sender, EventArgs e) => await NavigateToEventsAsync(includeSelectedDate: true);

	private async void OnOpenEventsClicked(object? sender, EventArgs e)
	{
		var timer = Stopwatch.StartNew();
		Debug.WriteLine("PERF Event List button click: handler entered.");
		await NavigateToEventsAsync(includeSelectedDate: false);
		timer.Stop();
		Debug.WriteLine($"PERF Event List button click to navigation completion: {timer.Elapsed.TotalMilliseconds:F3} ms.");
	}

	private async void OnAddSelectedDateEventClicked(object? sender, EventArgs e) => await NavigateToEventsAsync(includeSelectedDate: true);

	private Task NavigateToEventsAsync(bool includeSelectedDate)
	{
		if (isNavigatingToEvents)
		{
			Debug.WriteLine("PERF Duplicate Event List navigation request suppressed.");
			return Task.CompletedTask;
		}
		isNavigatingToEvents = true;
		var timer = Stopwatch.StartNew();
		var parameters = new Dictionary<string, object>();
		if (includeSelectedDate)
			parameters.Add("selectedDate", viewModel.SelectedDate.Date);
		Debug.WriteLine("PERF Event List navigation request start.");
		return NavigateAndMeasureAsync(parameters, timer, () => isNavigatingToEvents = false);
	}

	private static async Task NavigateAndMeasureAsync(IDictionary<string, object> parameters, Stopwatch timer, Action completed)
	{
		try
		{
			await Shell.Current.GoToAsync("events", false, parameters);
			timer.Stop();
			Debug.WriteLine($"PERF Event List route transition: {timer.Elapsed.TotalMilliseconds:F3} ms (GoToAsync completion).");
		}
		finally { completed(); }
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
		else if (e.PropertyName == nameof(CalendarViewModel.SelectedEvents))
			Dispatcher.Dispatch(RefreshSelectedEventCards);
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

	private void OnThemeToggleClicked(object? sender, EventArgs e)
	{
		if (Microsoft.Maui.Controls.Application.Current is App app)
		{
			app.ToggleTheme();
			RefreshThemeAwareContent();
		}
	}

	private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)
	{
		Dispatcher.Dispatch(RefreshThemeAwareContent);
	}

	private void RefreshThemeAwareContent()
	{
		UpdateThemeToggle();
		UpdateMonthCells(viewModel.Days);
		RefreshSelectedEventCards();
	}

	private void UpdateThemeToggle()
	{
		var isDark = IsDarkTheme;
		ThemeToggleButton.Text = isDark ? "☾" : "☀";
		SemanticProperties.SetDescription(ThemeToggleButton,
			isDark ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối");
#if ANDROID
		MainActivity.ApplyStatusBarTheme(isDark);
#endif
	}

	private static bool IsDarkTheme
	{
		get
		{
			var application = Microsoft.Maui.Controls.Application.Current;
			return application?.UserAppTheme == AppTheme.Dark ||
				(application?.UserAppTheme == AppTheme.Unspecified && application?.RequestedTheme == AppTheme.Dark);
		}
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
		cell.Border.Stroke = day.StrokeColor;
		cell.GregorianLabel.TextColor = day.TextColor;
		cell.LunarLabel.TextColor = day.TextColor;
		cell.EventIndicator.Text = day.EventIndicator;
		cell.EventIndicator.TextColor = day.EventColor;
		cell.Tap.CommandParameter = day.Date.HasValue ? day : null;
		cell.Border.IsEnabled = day.Date.HasValue;
	}

	private void RefreshSelectedEventCards()
	{
		SelectedEventCards.Children.Clear();
		for (var index = 0; index < viewModel.SelectedEvents.Count; index++)
			SelectedEventCards.Children.Add(CreateSelectedEventCard(viewModel.SelectedEvents[index], index));
	}

	private View CreateSelectedEventCard(CalendarEvent item, int index)
	{
		var color = GetEventColor(item, index);
		var isDark = IsDarkTheme;
		var detailTextColor = isDark ? Color.FromArgb("#B8B3A9") : Color.FromArgb("#777B76");
		var summary = $"{(item.CalendarType == EventCalendarType.Lunar ? "Âm lịch" : "Dương lịch")} · {item.Day:00}/{item.Month:00}{(item.IsLeapMonth ? " nhuận" : string.Empty)} · {(item.RecurrenceType == EventRecurrenceType.Yearly ? "lặp hằng năm" : $"một lần, {item.Year}")}";
		var detail = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
		detail.Children.Add(new Label
		{
			Text = item.Title,
			FontSize = 15,
			FontAttributes = FontAttributes.Bold,
			TextColor = isDark ? Color.FromArgb("#F5F2EC") : Color.FromArgb("#252B2A"),
			LineBreakMode = LineBreakMode.WordWrap
		});
		detail.Children.Add(new Label { Text = summary, FontSize = 12, TextColor = detailTextColor, LineBreakMode = LineBreakMode.WordWrap });
		if (!string.IsNullOrWhiteSpace(item.Description))
			detail.Children.Add(new Label { Text = item.Description, FontSize = 12, TextColor = detailTextColor, LineBreakMode = LineBreakMode.WordWrap, MaxLines = 2 });

		var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
		row.Children.Add(new BoxView { WidthRequest = 12, HeightRequest = 12, CornerRadius = 6, Color = color, VerticalOptions = LayoutOptions.Center });
		Grid.SetColumn(detail, 1);
		row.Children.Add(detail);
		var chevron = new Label { Text = "›", FontSize = 24, TextColor = color, VerticalTextAlignment = TextAlignment.Center };
		Grid.SetColumn(chevron, 2);
		row.Children.Add(chevron);
		var card = new Border
		{
			Padding = new Thickness(12, 10),
			BackgroundColor = isDark ? Color.FromArgb("#242424") : Color.FromArgb("#FFFFFF"),
			Stroke = Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
			Content = row
		};
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await NavigateToEventsAsync(includeSelectedDate: true);
		card.GestureRecognizers.Add(tap);
		return card;
	}

	private static Color GetEventColor(CalendarEvent item, int index)
	{
		if (!string.IsNullOrWhiteSpace(item.Color))
		{
			try { return Color.FromArgb(item.Color); }
			catch (ArgumentException) { }
		}
		Color[] defaults = [Color.FromArgb("#F47A00"), Color.FromArgb("#0875D1"), Color.FromArgb("#6A32C9")];
		return defaults[index % defaults.Length];
	}

	private sealed record CalendarCell(Border Border, Label GregorianLabel, Label LunarLabel, Label EventIndicator, TapGestureRecognizer Tap);
}
