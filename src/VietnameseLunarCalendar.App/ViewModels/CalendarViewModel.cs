using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using VietnameseLunarCalendar.Domain.Interfaces;

namespace VietnameseLunarCalendar.App.ViewModels;

/// <summary>Interactive month calendar with Vietnamese lunar date conversion.</summary>
public partial class CalendarViewModel : ObservableObject
{
	private readonly ILunarCalendarService calendar;
	private readonly CultureInfo vietnamese = CultureInfo.GetCultureInfo("vi-VN");
	private readonly Dictionary<DateOnly, VietnameseLunarCalendar.Domain.Models.LunarDate> displayedMonthLunar = new();
	private DateTime lastSelectedDate;

	[ObservableProperty]
	private DateTime displayedMonth;

	[ObservableProperty]
	private DateTime selectedDate;

	public ObservableCollection<CalendarDayViewModel> Days { get; } = new();
	public string MonthTitle => DisplayedMonth.ToString("MMMM yyyy", vietnamese);
	public string SelectedGregorian => SelectedDate.ToString("dddd, dd MMMM yyyy", vietnamese);
	public string SelectedLunar
	{
		get
		{
			var date = DateOnly.FromDateTime(SelectedDate);
			var lunar = displayedMonthLunar.TryGetValue(date, out var cached) ? cached : calendar.GregorianToLunar(date);
			return $"{lunar.Day} {LunarMonthName(lunar.Month)}{(lunar.IsLeapMonth ? " nhuận" : string.Empty)} năm {lunar.Year} · {CanChiYear(lunar.Year)}";
		}
	}
	public DateTime MinimumDate => new(1900, 1, 1);
	public DateTime MaximumDate => new(2100, 12, 31);
	public bool CanGoPrevious => DisplayedMonth > new DateTime(1900, 1, 1);
	public bool CanGoNext => DisplayedMonth < new DateTime(2100, 12, 1);

	public CalendarViewModel(ILunarCalendarService calendar)
	{
		this.calendar = calendar;
		selectedDate = DateTime.Today < MinimumDate || DateTime.Today > MaximumDate ? new DateTime(2026, 1, 1) : DateTime.Today;
		lastSelectedDate = selectedDate.Date;
		displayedMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
		RefreshMonth();
	}

	partial void OnDisplayedMonthChanged(DateTime value)
	{
		OnPropertyChanged(nameof(MonthTitle));
		OnPropertyChanged(nameof(CanGoPrevious));
		OnPropertyChanged(nameof(CanGoNext));
		RefreshMonth();
	}

	partial void OnSelectedDateChanged(DateTime value)
	{
		var timer = Stopwatch.StartNew();
		var previousDate = lastSelectedDate;
		if (value.Date == previousDate) return;
		lastSelectedDate = value.Date;
		OnPropertyChanged(nameof(SelectedGregorian));
		if (DisplayedMonth.Year != value.Year || DisplayedMonth.Month != value.Month)
			DisplayedMonth = new DateTime(value.Year, value.Month, 1);
		else
		{
			UpdateSelectedCell(previousDate, false);
			UpdateSelectedCell(value.Date, true);
		}
		OnPropertyChanged(nameof(SelectedLunar));
		timer.Stop();
		Debug.WriteLine($"Calendar date selection: {timer.Elapsed.TotalMilliseconds:F3} ms (same month: {DisplayedMonth.Year == value.Year && DisplayedMonth.Month == value.Month}).");
	}

	[RelayCommand]
	private void NextMonth()
	{
		if (CanGoNext) DisplayedMonth = DisplayedMonth.AddMonths(1);
	}

	[RelayCommand]
	private void PreviousMonth()
	{
		if (CanGoPrevious) DisplayedMonth = DisplayedMonth.AddMonths(-1);
	}

	[RelayCommand]
	private void GoToToday()
	{
		if (DateTime.Today >= MinimumDate && DateTime.Today <= MaximumDate)
			SelectedDate = DateTime.Today;
	}

	[RelayCommand]
	private void SelectDate(CalendarDayViewModel? day)
	{
		if (day is not null && day.Date.HasValue)
			SelectedDate = day.Date.Value;
	}

	private void RefreshMonth()
	{
		var timer = Stopwatch.StartNew();
		Days.Clear();
		displayedMonthLunar.Clear();
		var first = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
		var offset = ((int)first.DayOfWeek + 6) % 7;
		for (var i = 0; i < offset; i++) Days.Add(CalendarDayViewModel.Blank);
		var count = DateTime.DaysInMonth(first.Year, first.Month);
		for (var day = 1; day <= count; day++)
		{
			var date = new DateTime(first.Year, first.Month, day);
			var lunar = calendar.GregorianToLunar(DateOnly.FromDateTime(date));
			displayedMonthLunar[DateOnly.FromDateTime(date)] = lunar;
			Days.Add(new CalendarDayViewModel(date, lunar.Day == 1 ? lunar.Month.ToString() : lunar.Day.ToString(),
				date.Date == SelectedDate.Date, date.Date == DateTime.Today.Date));
		}
		while (Days.Count % 7 != 0) Days.Add(CalendarDayViewModel.Blank);
		timer.Stop();
		Debug.WriteLine($"Calendar month refresh {first:yyyy-MM}: {timer.Elapsed.TotalMilliseconds:F3} ms; {count} date conversions.");
	}

	private void UpdateSelectedCell(DateTime date, bool isSelected)
	{
		for (var index = 0; index < Days.Count; index++)
		{
			var day = Days[index];
			if (day.Date?.Date != date) continue;
			Days[index] = day with { IsSelected = isSelected };
			return;
		}
	}

	private static string LunarMonthName(int month) => month switch
	{
		1 => "tháng Giêng", 12 => "tháng Chạp", _ => $"tháng {month}"
	};

	private static string CanChiYear(int year)
	{
		string[] can = ["Canh", "Tân", "Nhâm", "Quý", "Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ"];
		string[] chi = ["Thân", "Dậu", "Tuất", "Hợi", "Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi"];
		return $"{can[year % 10]} {chi[year % 12]}";
	}
}

public sealed record CalendarDayViewModel(DateTime? Date, string LunarDay, bool IsSelected, bool IsToday)
{
	public static CalendarDayViewModel Blank { get; } = new(null, string.Empty, false, false);
	public string GregorianDay => Date?.Day.ToString() ?? string.Empty;
	public Color TextColor => IsSelected ? Colors.White : IsToday ? Color.FromArgb("#A43126") : Color.FromArgb("#252B2A");
	public Color BackgroundColor => IsSelected ? Color.FromArgb("#A43126") : Colors.Transparent;
}
