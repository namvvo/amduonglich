using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;
using VietnameseLunarCalendar.Infrastructure.CalendarEvents;
using VietnameseLunarCalendar.Application.CalendarEvents;
using System.Diagnostics;

namespace VietnameseLunarCalendar.App.Views;

public partial class CalendarEventsPage : ContentPage, IQueryAttributable
{
	private static readonly string?[] EventColors = [null, "#A43126", "#C87522", "#46805B", "#3974A8", "#7653A6"];
	private readonly ICalendarEventService events;
	private readonly ILunarCalendarService lunarCalendar;
	private readonly EventReminderCoordinator reminders;
	private bool suppressReminderToggle;
	private Guid? editingId;
	private DateTime? selectedDateContext;
	private bool hasLoaded;

	public CalendarEventsPage(ICalendarEventService events, ILunarCalendarService lunarCalendar, EventReminderCoordinator reminders)
	{
		var timer = Stopwatch.StartNew();
		InitializeComponent();
		this.events = events;
		this.lunarCalendar = lunarCalendar;
		this.reminders = reminders;
		if (Microsoft.Maui.Controls.Application.Current is { } application)
			application.RequestedThemeChanged += OnRequestedThemeChanged;
		timer.Stop();
		Debug.WriteLine($"PERF CalendarEventsPage constructor (XAML initialization + dependencies): {timer.Elapsed.TotalMilliseconds:F3} ms; instance={GetHashCode()}.");
		Debug.WriteLine("PERF CalendarEventsViewModel initialization: N/A; this page currently owns its list state in code-behind and has no CalendarEventsViewModel.");
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var timer = Stopwatch.StartNew();
		selectedDateContext = query.TryGetValue("selectedDate", out var value) && value is DateTime date
			? date.Date
			: null;
		UpdateSelectedDateCard();
		EventFormCard.IsVisible = selectedDateContext.HasValue;
		ClearForm();
		timer.Stop();
		Debug.WriteLine($"PERF CalendarEventsPage query/form setup: {timer.Elapsed.TotalMilliseconds:F3} ms.");
	}

	protected override async void OnAppearing()
	{
		var timer = Stopwatch.StartNew();
		Debug.WriteLine($"PERF CalendarEventsPage.OnAppearing start; instance={GetHashCode()}.");
		base.OnAppearing();
#if ANDROID
		VietnameseLunarCalendar.App.MainActivity.ApplyStatusBarIcons(lightIcons: true);
#endif
		if (!hasLoaded)
			await RefreshListAsync();
		else
			Debug.WriteLine($"PERF CalendarEventsPage.OnAppearing reused loaded list: 0 ms; instance={GetHashCode()}.");
		var dataReady = timer.Elapsed.TotalMilliseconds;
		Debug.WriteLine($"PERF CalendarEventsPage.OnAppearing data ready: {dataReady:F3} ms; instance={GetHashCode()}.");
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(32), () =>
		{
			timer.Stop();
			Debug.WriteLine($"PERF Event List first post-layout interactive callback: {timer.Elapsed.TotalMilliseconds:F3} ms; data stage: {dataReady:F3} ms; instance={GetHashCode()}.");
		});
	}

	private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) =>
		Dispatcher.Dispatch(UpdateDynamicEventTextColors);

	private void UpdateDynamicEventTextColors()
	{
		var isDark = IsDarkTheme;
		foreach (var row in EventList.Children.OfType<Grid>())
			if (row.BindingContext is CalendarEvent item && row.Children.FirstOrDefault() is Label title)
				title.TextColor = GetEventTextColor(item.Color, isDark);

		var selectedDateTextColor = Color.FromArgb(isDark ? "#F5F2EC" : "#252B2A");
		foreach (var label in SelectedDateEvents.Children.OfType<Label>())
			label.TextColor = selectedDateTextColor;
	}

	private void OnCalendarTypeChanged(object? sender, EventArgs e)
	{
		LeapMonthPanel.IsVisible = CalendarPicker.SelectedIndex == 0;
		if (editingId is null && selectedDateContext.HasValue)
			PrefillFromSelectedDate();
	}

	private void OnRecurrenceChanged(object? sender, EventArgs e)
	{
		YearEntry.IsVisible = RecurrencePicker.SelectedIndex == 1;
		if (selectedDateContext.HasValue && CalendarPicker.SelectedIndex >= 0)
			YearEntry.Text = EventYearForSelectedDate().ToString();
	}

	private async void OnReminderEnabledChanged(object? sender, ToggledEventArgs e)
	{
		if (suppressReminderToggle) return;
		if (!e.Value)
		{
			ReminderPermissionStatus.Text = string.Empty;
			ReminderSettingsButton.IsVisible = false;
			return;
		}

		try
		{
			var granted = await reminders.RequestPermissionAsync();
			ReminderPermissionStatus.Text = granted
				? "Thông báo đã được bật."
				: "Chưa có quyền thông báo. Hãy cấp quyền trong Cài đặt để nhận nhắc nhở.";
			ReminderSettingsButton.IsVisible = !granted;
			if (!granted)
			{
				suppressReminderToggle = true;
				ReminderEnabledSwitch.IsToggled = false;
				suppressReminderToggle = false;
			}
		}
		catch (Exception exception)
		{
			Debug.WriteLine($"Event reminder permission request failed: {exception}");
			ReminderPermissionStatus.Text = "Không thể yêu cầu quyền thông báo. Hãy kiểm tra Cài đặt.";
			ReminderSettingsButton.IsVisible = true;
			suppressReminderToggle = true;
			ReminderEnabledSwitch.IsToggled = false;
			suppressReminderToggle = false;
		}
	}

	private async void OnReminderSettingsClicked(object? sender, EventArgs e) => await reminders.OpenNotificationSettingsAsync();

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(TitleEntry.Text))
				throw new ArgumentException("Vui lòng nhập tên sự kiện.");
			if (CalendarPicker.SelectedIndex < 0 || RecurrencePicker.SelectedIndex < 0 ||
				!int.TryParse(DayEntry.Text, out var day) || !int.TryParse(MonthEntry.Text, out var month))
				throw new ArgumentException("Vui lòng nhập loại lịch, cách lặp, ngày, tháng và số ngày nhắc hợp lệ.");
			var reminder = 0;
			if (ReminderEnabledSwitch.IsToggled && (!int.TryParse(ReminderEntry.Text, out reminder) || reminder is < 0 or > 30))
				throw new ArgumentException("Số ngày nhắc phải nằm trong khoảng từ 0 đến 30.");

			int? year = null;
			if (RecurrencePicker.SelectedIndex == 1)
			{
				if (!int.TryParse(YearEntry.Text, out var parsedYear))
					throw new ArgumentException("Sự kiện một lần cần có năm hợp lệ.");
				year = parsedYear;
			}

			var item = new CalendarEvent
			{
				Id = editingId ?? Guid.NewGuid(),
				Title = TitleEntry.Text.Trim(),
				Description = EmptyToNull(DescriptionEntry.Text),
				CalendarType = CalendarPicker.SelectedIndex == 0 ? EventCalendarType.Lunar : EventCalendarType.Gregorian,
				RecurrenceType = RecurrencePicker.SelectedIndex == 0 ? EventRecurrenceType.Yearly : EventRecurrenceType.OneTime,
				Day = day,
				Month = month,
				Year = year,
				IsLeapMonth = CalendarPicker.SelectedIndex == 0 && LeapMonthSwitch.IsToggled,
				Category = EmptyToNull(CategoryEntry.Text),
				Color = EventColors.ElementAtOrDefault(ColorPicker.SelectedIndex),
				IsReminderEnabled = ReminderEnabledSwitch.IsToggled,
				ReminderDaysBefore = reminder
			};
			if (editingId.HasValue) await events.UpdateAsync(item);
			else await events.CreateAsync(item);
			await ReconcileRemindersSafelyAsync();
			ClearForm();
			await RefreshListAsync();
		}
		catch (Exception exception)
		{
			await DisplayAlertAsync("Không lưu được sự kiện", GetUserFriendlyErrorMessage(exception), "Đóng");
		}
	}

	private async void OnNewEventClicked(object? sender, EventArgs e)
	{
		ClearForm();
		EventFormCard.IsVisible = true;
		await PageScroll.ScrollToAsync(0, 0, true);
	}

	private async void OnClearClicked(object? sender, EventArgs e)
	{
		ClearForm();
		EventFormCard.IsVisible = false;
		await PageScroll.ScrollToAsync(0, 0, true);
	}

	private async Task RefreshListAsync()
	{
		var totalTimer = Stopwatch.StartNew();
		try
		{
			var loadTimer = Stopwatch.StartNew();
			var savedEvents = await events.GetAllAsync();
			loadTimer.Stop();
			Debug.WriteLine($"Event List JSON/service load: {loadTimer.Elapsed.TotalMilliseconds:F3} ms ({savedEvents.Count} events).");
			var renderTimer = Stopwatch.StartNew();
			EventList.Children.Clear();
			foreach (var item in savedEvents)
				EventList.Children.Add(CreateEventRow(item));
			await RefreshSelectedDateEventsAsync(savedEvents);
			renderTimer.Stop();
			Debug.WriteLine($"PERF Event List collection/control population: {renderTimer.Elapsed.TotalMilliseconds:F3} ms; {savedEvents.Count} rows, {savedEvents.Count * 3} row controls.");
			hasLoaded = true;
		}
		catch (Exception exception)
		{
			await DisplayAlertAsync("Không tải được sự kiện", GetUserFriendlyErrorMessage(exception), "Đóng");
		}
		finally
		{
			totalTimer.Stop();
			Debug.WriteLine($"PERF Event List data stage total: {totalTimer.Elapsed.TotalMilliseconds:F3} ms.");
		}
	}

	private View CreateEventRow(CalendarEvent item)
	{
		var isDark = IsDarkTheme;
		var recurrence = item.RecurrenceType == EventRecurrenceType.Yearly ? "hằng năm" : $"một lần, {item.Year}";
		var calendar = item.CalendarType == EventCalendarType.Lunar ? "âm lịch" : "dương lịch";
		var leap = item.IsLeapMonth ? " nhuận" : string.Empty;
		var title = new Label
		{
			Text = $"{item.Title} · {item.Day:00}/{item.Month:00}{leap} {calendar} · {recurrence}",
			VerticalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.WordWrap,
			TextColor = GetEventTextColor(item.Color, isDark)
		};
		var edit = new Button { Text = "Sửa", Padding = new Thickness(10, 4) };
		edit.Clicked += async (_, _) => await LoadForEditAsync(item);
		var delete = new Button { Text = "Xóa", Padding = new Thickness(10, 4), TextColor = Color.FromArgb("#A43126") };
		delete.Clicked += async (_, _) => await DeleteAsync(item);

		var row = new Grid { ColumnSpacing = 6, Padding = new Thickness(0, 3), BindingContext = item };
		row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
		Grid.SetColumn(edit, 1);
		Grid.SetColumn(delete, 2);
		row.Children.Add(title);
		row.Children.Add(edit);
		row.Children.Add(delete);
		return row;
	}

	private Task RefreshSelectedDateEventsAsync(IReadOnlyList<CalendarEvent> savedEvents)
	{
		SelectedDateEvents.Children.Clear();
		if (!selectedDateContext.HasValue) return Task.CompletedTask;
		var date = DateOnly.FromDateTime(selectedDateContext.Value);
		var lunar = lunarCalendar.GregorianToLunar(date);
		var occurring = savedEvents.Where(item => item.CalendarType == EventCalendarType.Gregorian
			? item.Day == date.Day && item.Month == date.Month && (item.RecurrenceType == EventRecurrenceType.Yearly || item.Year == date.Year)
			: item.Day == lunar.Day && item.Month == lunar.Month && item.IsLeapMonth == lunar.IsLeapMonth &&
			  (item.RecurrenceType == EventRecurrenceType.Yearly || item.Year == lunar.Year)).ToArray();
		SelectedDateEvents.Children.Add(new Label
		{
			Text = occurring.Length == 0 ? "Chưa có sự kiện trong ngày này." : string.Join(Environment.NewLine, occurring.Select(item =>
				string.IsNullOrWhiteSpace(item.Description) ? $"• {item.Title}" : $"• {item.Title}: {item.Description}")),
			TextColor = Color.FromArgb(IsDarkTheme ? "#F5F2EC" : "#252B2A")
		});
		return Task.CompletedTask;
	}

	private void UpdateSelectedDateCard()
	{
		SelectedDateCard.IsVisible = selectedDateContext.HasValue;
		if (selectedDateContext.HasValue)
			SelectedDateHeading.Text = $"Sự kiện ngày {selectedDateContext.Value:dd/MM/yyyy}";
		SelectedDateEvents.Children.Clear();
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

	private static Color GetEventTextColor(string? color, bool isDark)
	{
		if (string.IsNullOrEmpty(color) || !EventColors.Contains(color))
			return Color.FromArgb(isDark ? "#F5F2EC" : "#252B2A");
		if (!isDark) return Color.FromArgb(color);
		return color switch
		{
			"#A43126" => Color.FromArgb("#FF8A80"),
			"#C87522" => Color.FromArgb("#FFB259"),
			"#46805B" => Color.FromArgb("#81C784"),
			"#3974A8" => Color.FromArgb("#64B5F6"),
			"#7653A6" => Color.FromArgb("#CE93D8"),
			_ => Color.FromArgb("#F5F2EC")
		};
	}

	private async Task DeleteAsync(CalendarEvent item)
	{
		if (!await DisplayAlertAsync("Xóa sự kiện?", item.Title, "Xóa", "Hủy")) return;
		try
		{
			await events.DeleteAsync(item.Id);
			await ReconcileRemindersSafelyAsync();
			if (editingId == item.Id) ClearForm();
			await RefreshListAsync();
		}
		catch (Exception exception)
		{
			await DisplayAlertAsync("Không xóa được sự kiện", GetUserFriendlyErrorMessage(exception), "Đóng");
		}
	}

	private async Task LoadForEditAsync(CalendarEvent item)
	{
		EventFormCard.IsVisible = true;
		editingId = item.Id;
		FormHeading.Text = "Chỉnh sửa sự kiện";
		TitleEntry.Text = item.Title;
		DescriptionEntry.Text = item.Description;
		CalendarPicker.SelectedIndex = item.CalendarType == EventCalendarType.Lunar ? 0 : 1;
		RecurrencePicker.SelectedIndex = item.RecurrenceType == EventRecurrenceType.Yearly ? 0 : 1;
		DayEntry.Text = item.Day.ToString();
		MonthEntry.Text = item.Month.ToString();
		YearEntry.Text = item.Year?.ToString();
		CategoryEntry.Text = item.Category;
		ReminderEntry.Text = item.ReminderDaysBefore.ToString();
		suppressReminderToggle = true;
		ReminderEnabledSwitch.IsToggled = item.IsReminderEnabled;
		suppressReminderToggle = false;
		ReminderPermissionStatus.Text = item.IsReminderEnabled && !reminders.AreNotificationsEnabled()
			? "Thông báo đang bị chặn trong Cài đặt." : string.Empty;
		ReminderSettingsButton.IsVisible = item.IsReminderEnabled && !reminders.AreNotificationsEnabled();
		LeapMonthSwitch.IsToggled = item.IsLeapMonth;
		ColorPicker.SelectedIndex = Array.IndexOf(EventColors, item.Color);
		if (ColorPicker.SelectedIndex < 0) ColorPicker.SelectedIndex = 0;
		OnCalendarTypeChanged(this, EventArgs.Empty);
		OnRecurrenceChanged(this, EventArgs.Empty);
		await PageScroll.ScrollToAsync(0, 0, true);
	}

	private void ClearForm()
	{
		editingId = null;
		FormHeading.Text = "Thêm sự kiện";
		TitleEntry.Text = string.Empty;
		DescriptionEntry.Text = string.Empty;
		DayEntry.Text = string.Empty;
		MonthEntry.Text = string.Empty;
		YearEntry.Text = string.Empty;
		CategoryEntry.Text = string.Empty;
		CalendarPicker.SelectedIndex = 0;
		RecurrencePicker.SelectedIndex = 0;
		LeapMonthSwitch.IsToggled = false;
		ColorPicker.SelectedIndex = 0;
		ReminderEntry.Text = "0";
		suppressReminderToggle = true;
		ReminderEnabledSwitch.IsToggled = false;
		suppressReminderToggle = false;
		ReminderPermissionStatus.Text = string.Empty;
		ReminderSettingsButton.IsVisible = false;
		OnCalendarTypeChanged(this, EventArgs.Empty);
		OnRecurrenceChanged(this, EventArgs.Empty);
		if (selectedDateContext.HasValue) PrefillFromSelectedDate();
	}

	private void PrefillFromSelectedDate()
	{
		if (!selectedDateContext.HasValue) return;
		var date = selectedDateContext.Value.Date;
		if (CalendarPicker.SelectedIndex == 0)
		{
			var lunar = lunarCalendar.GregorianToLunar(DateOnly.FromDateTime(date));
			DayEntry.Text = lunar.Day.ToString();
			MonthEntry.Text = lunar.Month.ToString();
			LeapMonthSwitch.IsToggled = lunar.IsLeapMonth;
			YearEntry.Text = lunar.Year.ToString();
		}
		else
		{
			DayEntry.Text = date.Day.ToString();
			MonthEntry.Text = date.Month.ToString();
			YearEntry.Text = date.Year.ToString();
			LeapMonthSwitch.IsToggled = false;
		}
	}

	private int EventYearForSelectedDate()
	{
		if (!selectedDateContext.HasValue) return DateTime.Today.Year;
		return CalendarPicker.SelectedIndex == 0
			? lunarCalendar.GregorianToLunar(DateOnly.FromDateTime(selectedDateContext.Value)).Year
			: selectedDateContext.Value.Year;
	}

	private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

	private async Task ReconcileRemindersSafelyAsync()
	{
		try { await reminders.ReconcileAsync(); }
		catch (Exception exception)
		{
			Debug.WriteLine($"Event reminders could not be synchronized after event save: {exception}");
			ReminderPermissionStatus.Text = "Đã lưu sự kiện, nhưng chưa thể cập nhật lịch nhắc. Hãy mở lại ứng dụng.";
		}
	}

	private static string GetUserFriendlyErrorMessage(Exception exception) => exception switch
	{
		CalendarEventPersistenceException persistenceException => persistenceException.Message,
		ArgumentException => "Thông tin sự kiện chưa hợp lệ. Vui lòng kiểm tra ngày, tháng, năm và các trường bắt buộc.",
		KeyNotFoundException => "Không tìm thấy sự kiện cần thao tác.",
		_ => "Đã xảy ra lỗi khi xử lý sự kiện. Vui lòng thử lại."
	};
}
