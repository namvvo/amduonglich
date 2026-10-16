using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Diagnostics;
using VietnameseLunarCalendar.Domain.Interfaces;

namespace VietnameseLunarCalendar.App.ViewModels;

/// <summary>Interactive month calendar with Vietnamese lunar date conversion.</summary>
public partial class CalendarViewModel : ObservableObject
{
	private readonly ILunarCalendarService calendar;
	private readonly ICalendarEventService events;
	private readonly object eventMonthGate = new();
	private readonly Dictionary<(int Year, int Month), Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<VietnameseLunarCalendar.Domain.Models.CalendarEvent>>>> eventMonthLoads = new();
	private readonly CultureInfo vietnamese = CultureInfo.GetCultureInfo("vi-VN");
	private readonly Dictionary<DateOnly, VietnameseLunarCalendar.Domain.Models.LunarDate> displayedMonthLunar = new();
	private DateTime lastSelectedDate;

	[ObservableProperty]
	private DateTime displayedMonth;

	[ObservableProperty]
	private DateTime selectedDate;

	[ObservableProperty]
	private IReadOnlyList<CalendarDayViewModel> days = Array.Empty<CalendarDayViewModel>();
	[ObservableProperty]
	private IReadOnlyList<VietnameseLunarCalendar.Domain.Models.CalendarEvent> selectedEvents = Array.Empty<VietnameseLunarCalendar.Domain.Models.CalendarEvent>();
	[ObservableProperty]
	private string selectedEventsMessage = "Chưa có sự kiện";
	public bool HasSelectedEvents => SelectedEvents.Count > 0;
	public bool HasNoSelectedEvents => SelectedEvents.Count == 0;
	public string MonthTitle => $"Tháng {DisplayedMonth.Month} {DisplayedMonth.Year}";
	public string LunarMonthRange
	{
		get
		{
			var first = calendar.GregorianToLunar(DateOnly.FromDateTime(DisplayedMonth));
			var last = calendar.GregorianToLunar(DateOnly.FromDateTime(DisplayedMonth.AddMonths(1).AddDays(-1)));
			var start = LunarMonthNumber(first);
			var end = LunarMonthNumber(last);
			var yearText = CanChiYear(first.Year);
			return first.Month == last.Month && first.Year == last.Year && first.IsLeapMonth == last.IsLeapMonth
				? $"Tháng {start} âm lịch, năm {yearText}" : $"Tháng {start} – {end} âm lịch, năm {yearText}";
		}
	}
	public string SelectedGregorian
	{
		get
		{
			var value = SelectedDate.ToString("dddd, dd 'tháng' M, yyyy", vietnamese);
			return value.ToUpper(vietnamese);
		}
	}
	public string SelectedLunar
	{
		get
		{
			var date = DateOnly.FromDateTime(SelectedDate);
			var lunar = displayedMonthLunar.TryGetValue(date, out var cached) ? cached : calendar.GregorianToLunar(date);
			var dayNumber = DateOnly.FromDateTime(SelectedDate).DayNumber;
			var canChiDay = CanChi((dayNumber + 5) % 10, (dayNumber + 3) % 12);
			var canChiMonth = CanChi((lunar.Year * 12 + lunar.Month + 3) % 10, (lunar.Month + 1) % 12);
			return $"{lunar.Day} tháng {lunar.Month}{(lunar.IsLeapMonth ? " nhuận" : string.Empty)} năm {lunar.Year} · {CanChiYear(lunar.Year)}\nNgày {canChiDay} · Tháng {canChiMonth} · Năm {CanChiYear(lunar.Year)}";
		}
	}
	public DateTime MinimumDate => new(1900, 1, 1);
	public DateTime MaximumDate => new(2100, 12, 31);
	public bool CanGoPrevious => DisplayedMonth > new DateTime(1900, 1, 1);
	public bool CanGoNext => DisplayedMonth < new DateTime(2100, 12, 1);

	public void RefreshEvents()
	{
		lock (eventMonthGate) eventMonthLoads.Clear();
		_ = RefreshMonthEventsAsync(DisplayedMonth.Year, DisplayedMonth.Month);
	}

	public CalendarViewModel(ILunarCalendarService calendar, ICalendarEventService events)
	{
		this.calendar = calendar;
		this.events = events;
		selectedDate = DateTime.Today < MinimumDate || DateTime.Today > MaximumDate ? new DateTime(2026, 1, 1) : DateTime.Today;
		lastSelectedDate = selectedDate.Date;
		displayedMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
		RefreshMonth();
	}

	partial void OnDisplayedMonthChanged(DateTime value)
	{
		var navigationTimer = Stopwatch.StartNew();
		OnPropertyChanged(nameof(MonthTitle));
		OnPropertyChanged(nameof(LunarMonthRange));
		OnPropertyChanged(nameof(CanGoPrevious));
		OnPropertyChanged(nameof(CanGoNext));
		RefreshMonth();
		navigationTimer.Stop();
		Debug.WriteLine($"Calendar month navigation {value:yyyy-MM}: {navigationTimer.Elapsed.TotalMilliseconds:F3} ms (synchronous view-model operation; UI layout is asynchronous).");
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
            UpdateSelectedCell(previousDate, value.Date);
            //UpdateSelectedCell(previousDate, false);
            //UpdateSelectedCell(value.Date, true);
        }
		OnPropertyChanged(nameof(SelectedLunar));
		_ = RefreshSelectedEventsAsync(value.Date);
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
		var dayBuildTimer = Stopwatch.StartNew();
		var nextDays = new List<CalendarDayViewModel>(42);
		displayedMonthLunar.Clear();
		var first = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
		var offset = ((int)first.DayOfWeek + 6) % 7;
		var count = 42;
		var conversionTimer = Stopwatch.StartNew();
		var gridStart = first.AddDays(-offset);
		for (var index = 0; index < count; index++)
		{
			var date = gridStart.AddDays(index);
			if (date.Year is < 1900 or > 2100) { nextDays.Add(CalendarDayViewModel.Blank); continue; }
			var lunar = calendar.GregorianToLunar(DateOnly.FromDateTime(date));
			if (date.Month == first.Month) displayedMonthLunar[DateOnly.FromDateTime(date)] = lunar;
			nextDays.Add(new CalendarDayViewModel(date, $"{lunar.Day}/{lunar.Month}{(lunar.IsLeapMonth ? "N" : string.Empty)}",
				date.Date == SelectedDate.Date, date.Date == DateTime.Today.Date, 0, date.Month == first.Month, IsHoliday(date, lunar)));
		}
		conversionTimer.Stop();
		dayBuildTimer.Stop();
		Days = nextDays.ToArray();
		_ = RefreshMonthEventsAsync(first.Year, first.Month);
		timer.Stop();
		Debug.WriteLine($"Calendar month data generation {first:yyyy-MM}: {dayBuildTimer.Elapsed.TotalMilliseconds:F3} ms; lunar conversions {conversionTimer.Elapsed.TotalMilliseconds:F3} ms; {Days.Count} cells published in one Days property notification.");
		Debug.WriteLine($"Calendar month refresh {first:yyyy-MM}: {timer.Elapsed.TotalMilliseconds:F3} ms (including collection publication).");
	}

	private async Task RefreshMonthEventsAsync(int year, int month)
	{
		try
		{
			var visibleMonths = Days
				.Where(day => day.Date.HasValue)
				.Select(day => (day.Date!.Value.Year, day.Date.Value.Month))
				.Distinct()
				.ToArray();
			var monthMaps = await Task.WhenAll(visibleMonths.Select(GetMonthEventsAsync));
			if (DisplayedMonth.Year != year || DisplayedMonth.Month != month) return;
			var monthEvents = monthMaps.SelectMany(map => map).ToDictionary(item => item.Key, item => item.Value);
			Days = Days.Select(day => day.Date is DateTime date
				? day with { EventCount = monthEvents.TryGetValue(DateOnly.FromDateTime(date), out var occurring) ? occurring.Count : 0 }
				: day).ToArray();
			if (SelectedDate.Year == year && SelectedDate.Month == month)
				SetSelectedEvents(monthEvents.TryGetValue(DateOnly.FromDateTime(SelectedDate), out var selected) ? selected : Array.Empty<VietnameseLunarCalendar.Domain.Models.CalendarEvent>());
		}
		catch (Exception exception) { Debug.WriteLine($"Calendar event month load failed: {exception}"); }
	}

	private Task<IReadOnlyDictionary<DateOnly, IReadOnlyList<VietnameseLunarCalendar.Domain.Models.CalendarEvent>>> GetMonthEventsAsync((int Year, int Month) month)
	{
		lock (eventMonthGate)
		{
			if (!eventMonthLoads.TryGetValue(month, out var load))
				eventMonthLoads[month] = load = events.GetEventsForMonthAsync(month.Year, month.Month);
			return load;
		}
	}

	private async Task RefreshSelectedEventsAsync(DateTime date)
	{
		try
		{
			var selectedDate = DateOnly.FromDateTime(date);
			var occurring = await events.GetEventsForDateAsync(selectedDate);
			if (SelectedDate.Date != date.Date) return;
			SetSelectedEvents(occurring);
		}
		catch (Exception exception)
		{
			Debug.WriteLine($"Calendar event date load failed: {exception}");
			SelectedEvents = Array.Empty<VietnameseLunarCalendar.Domain.Models.CalendarEvent>();
			SelectedEventsMessage = "Không thể tải sự kiện";
		}
	}

	private void SetSelectedEvents(IReadOnlyList<VietnameseLunarCalendar.Domain.Models.CalendarEvent> occurring)
	{
		SelectedEvents = occurring;
		SelectedEventsMessage = occurring.Count == 0 ? "Chưa có sự kiện trong ngày này." : string.Empty;
		OnPropertyChanged(nameof(HasSelectedEvents));
		OnPropertyChanged(nameof(HasNoSelectedEvents));
	}

    //private void UpdateSelectedCell(DateTime date, bool isSelected)
    //{
    //	var updatedDays = Days.ToArray();
    //	for (var index = 0; index < updatedDays.Length; index++)
    //	{
    //		var day = updatedDays[index];
    //		if (day.Date?.Date != date) continue;
    //		updatedDays[index] = day with { IsSelected = isSelected };
    //		Days = updatedDays;
    //		return;
    //	}
    //}
    private void UpdateSelectedCell(DateTime previousDate, DateTime newDate)
    {
        var updatedDays = Days.ToArray();

        for (var index = 0; index < updatedDays.Length; index++)
        {
            var day = updatedDays[index];

            if (day.Date?.Date == previousDate.Date)
                updatedDays[index] = day with { IsSelected = false };

            if (day.Date?.Date == newDate.Date)
                updatedDays[index] = day with { IsSelected = true };
        }

        Days = updatedDays;
    }
	private static bool IsHoliday(DateTime date, VietnameseLunarCalendar.Domain.Models.LunarDate lunar) =>
		(date.Month == 1 && date.Day == 1) || (date.Month == 4 && date.Day == 30) || (date.Month == 5 && date.Day == 1) || (date.Month == 9 && date.Day == 2) ||
		(lunar.Day == 1 && lunar.Month == 1) || (lunar.Day == 10 && lunar.Month == 3) || (lunar.Day == 15 && lunar.Month == 8);

	private static string LunarMonthNumber(VietnameseLunarCalendar.Domain.Models.LunarDate lunar) => $"{lunar.Month}{(lunar.IsLeapMonth ? "N" : string.Empty)}";

	private static string LunarMonthName(int month) => month switch
	{
		1 => "tháng Giêng", 12 => "tháng Chạp", _ => $"tháng {month}"
	};

	private static string CanChi(int canIndex, int chiIndex)
	{
		string[] can = ["Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ", "Canh", "Tân", "Nhâm", "Quý"];
		string[] chi = ["Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi", "Thân", "Dậu", "Tuất", "Hợi"];
		return $"{can[((canIndex % 10) + 10) % 10]} {chi[((chiIndex % 12) + 12) % 12]}";
	}
	private static string CanChiYear(int year) => CanChi((year + 6) % 10, (year + 8) % 12);
}

public sealed record CalendarDayViewModel(DateTime? Date, string LunarDay, bool IsSelected, bool IsToday, int EventCount = 0, bool IsCurrentMonth = true, bool IsHoliday = false)
{
	public static CalendarDayViewModel Blank { get; } = new(null, string.Empty, false, false);
	public string GregorianDay => Date?.Day.ToString() ?? string.Empty;
	public Color TextColor
	{
		get
		{
			var app = Microsoft.Maui.Controls.Application.Current;
			var isDark = app?.UserAppTheme == AppTheme.Dark ||
				(app?.UserAppTheme == AppTheme.Unspecified && app?.RequestedTheme == AppTheme.Dark);
			if (IsSelected) return Colors.White;
			if (IsToday) return Color.FromArgb(isDark ? "#176B32" : "#168A36");
			if (EventCount > 0) return Color.FromArgb(IsWeekend || IsHoliday ? "#A43126" : "#6B4300");
			if (IsWeekend || IsHoliday) return Color.FromArgb(isDark ? "#FF8A80" : "#A43126");
			return IsCurrentMonth
				? Color.FromArgb(isDark ? "#F5F2EC" : "#252B2A")
				: Color.FromArgb(isDark ? "#A8A39A" : "#B8B5AE");
		}
	}
	public bool IsWeekend => Date?.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
	public Color BackgroundColor => IsSelected ? Color.FromArgb("#A43126") : IsToday ? Color.FromArgb("#E5F4E8") : EventCount > 0 ? Color.FromArgb("#FFF0DB") : Colors.Transparent;
	public Color StrokeColor => IsSelected ? Color.FromArgb("#A43126") : IsToday ? Color.FromArgb("#168A36") : Colors.Transparent;
	public Color EventColor => Color.FromArgb("#F47A00");
	public string EventIndicator => EventCount switch { <= 0 => string.Empty, 1 => "●", 2 => "● ●", _ => "● ● ●" };
}
