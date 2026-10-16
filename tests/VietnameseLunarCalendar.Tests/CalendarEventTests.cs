using VietnameseLunarCalendar.Application.CalendarEvents;
using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;
using VietnameseLunarCalendar.Infrastructure.CalendarEvents;
using VietnameseLunarCalendar.Infrastructure.LunarCalendar;
using Xunit;

namespace VietnameseLunarCalendar.Tests;

public sealed class CalendarEventTests
{
	private readonly VietnameseLunarCalendarService lunar = new();

	[Fact]
	public async Task Lunar_yearly_event_follows_lunar_year_and_transition()
	{
		var service = NewService();
		await service.CreateAsync(Event("Tet", EventCalendarType.Lunar, 1, 1));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2024, 2, 10)));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2025, 1, 29)));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2025, 1, 28)));
	}

	[Fact]
	public async Task Gregorian_yearly_and_one_time_events_match_expected_years()
	{
		var service = NewService();
		await service.CreateAsync(Event("Christmas", EventCalendarType.Gregorian, 25, 12));
		await service.CreateAsync(Event("Only in 2024", EventCalendarType.Gregorian, 10, 6, EventRecurrenceType.OneTime, 2024));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2025, 12, 25)));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2024, 6, 10)));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2025, 6, 10)));
	}

	[Fact]
	public async Task Lunar_leap_month_matches_only_the_explicit_leap_month()
	{
		var service = NewService();
		await service.CreateAsync(Event("Leap month", EventCalendarType.Lunar, 1, 2, leap: true));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2023, 3, 22)));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2023, 2, 20)));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2024, 3, 10)));
	}

	[Fact]
	public async Task One_time_lunar_event_is_limited_to_its_lunar_year()
	{
		var service = NewService();
		await service.CreateAsync(Event("One time lunar", EventCalendarType.Lunar, 1, 1, EventRecurrenceType.OneTime, 2024));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2024, 2, 10)));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2025, 1, 29)));
	}

	[Fact]
	public async Task Update_and_detail_retrieval_preserve_identity_and_creation_time()
	{
		var service = NewService();
		var created = await service.CreateAsync(Event("Before", EventCalendarType.Gregorian, 2, 3));
		var updated = await service.UpdateAsync(created with { Title = "After" });
		Assert.Equal(created.Id, updated.Id);
		Assert.Equal(created.CreatedAt, updated.CreatedAt);
		Assert.Equal("After", (await service.GetByIdAsync(created.Id)).Title);
		Assert.Equal("After", Assert.Single(await service.GetAllAsync()).Title);
		await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByIdAsync(Guid.NewGuid()));
	}

	[Fact]
	public async Task Recurring_lunar_day_30_skips_a_29_day_month()
	{
		var service = NewService();
		await service.CreateAsync(Event("Thirtieth", EventCalendarType.Lunar, 30, 2));
		Assert.Single(await service.GetEventsForDateAsync(new DateOnly(2024, 4, 8)));
		Assert.Equal((29, 2, 2025), (lunar.GregorianToLunar(new DateOnly(2025, 3, 28)).Day,
			lunar.GregorianToLunar(new DateOnly(2025, 3, 28)).Month, lunar.GregorianToLunar(new DateOnly(2025, 3, 28)).Year));
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2025, 3, 28)));
	}

	[Fact]
	public async Task One_time_lunar_events_work_at_both_supported_year_boundaries()
	{
		var service = NewService();
		var firstDay = lunar.LunarToGregorian(1, 1, 1900);
		var lastYearFirstDay = lunar.LunarToGregorian(1, 1, 2100);
		await service.CreateAsync(Event("First supported lunar year", EventCalendarType.Lunar, 1, 1, EventRecurrenceType.OneTime, 1900));
		await service.CreateAsync(Event("Last supported lunar year", EventCalendarType.Lunar, 1, 1, EventRecurrenceType.OneTime, 2100));

		Assert.Single(await service.GetEventsForDateAsync(firstDay));
		Assert.Empty(await service.GetEventsForDateAsync(firstDay.AddDays(-1)));
		Assert.Single(await service.GetEventsForDateAsync(lastYearFirstDay));
		Assert.Empty(await service.GetEventsForDateAsync(lastYearFirstDay.AddDays(-1)));
	}

	[Fact]
	public async Task Lunar_event_dates_outside_supported_range_are_rejected()
	{
		var service = NewService();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateAsync(
			Event("Before supported years", EventCalendarType.Lunar, 1, 1, EventRecurrenceType.OneTime, 1899)));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateAsync(
			Event("After supported years", EventCalendarType.Lunar, 1, 1, EventRecurrenceType.OneTime, 2101)));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetEventsForDateAsync(new DateOnly(1899, 12, 31)));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetEventsForDateAsync(new DateOnly(2101, 3, 1)));
	}

	[Fact]
	public async Task Multiple_events_can_apply_to_one_date_and_unrelated_dates_are_empty()
	{
		var service = NewService();
		await service.CreateAsync(Event("Lunar", EventCalendarType.Lunar, 1, 1));
		await service.CreateAsync(Event("Gregorian", EventCalendarType.Gregorian, 10, 2));
		Assert.Equal(2, (await service.GetEventsForDateAsync(new DateOnly(2024, 2, 10))).Count);
		Assert.Empty(await service.GetEventsForDateAsync(new DateOnly(2024, 2, 11)));
	}

	[Fact]
	public async Task Json_repository_persists_events_between_instances()
	{
		var directory = Path.Combine(Path.GetTempPath(), "lunar-events-tests", Guid.NewGuid().ToString("N"));
		try
		{
			var repository = new JsonCalendarEventRepository(directory);
			var service = new CalendarEventService(repository, lunar);
			var saved = await service.CreateAsync(Event("Persisted", EventCalendarType.Gregorian, 4, 5));
			var restarted = new CalendarEventService(new JsonCalendarEventRepository(directory), lunar);
			Assert.Equal(saved, await restarted.GetByIdAsync(saved.Id));
			Assert.Single(await restarted.GetAllAsync());
			await restarted.DeleteAsync(saved.Id);
			Assert.Empty(await restarted.GetAllAsync());
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task Json_repository_cache_stays_current_after_create_update_and_delete()
	{
		var directory = Path.Combine(Path.GetTempPath(), "lunar-events-tests", Guid.NewGuid().ToString("N"));
		try
		{
			var repository = new JsonCalendarEventRepository(directory);
			Assert.Empty(await repository.GetAllAsync()); // Warm the in-memory cache.
			var service = new CalendarEventService(repository, lunar);
			var created = await service.CreateAsync(Event("Before", EventCalendarType.Gregorian, 4, 5));
			Assert.Equal("Before", Assert.Single(await repository.GetAllAsync()).Title);

			await service.UpdateAsync(created with { Title = "After" });
			Assert.Equal("After", Assert.Single(await repository.GetAllAsync()).Title);

			await service.DeleteAsync(created.Id);
			Assert.Empty(await repository.GetAllAsync());
			var restarted = new JsonCalendarEventRepository(directory);
			Assert.Empty(await restarted.GetAllAsync());
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task Json_repository_returns_empty_list_when_file_is_missing_or_contains_empty_array()
	{
		var directory = Path.Combine(Path.GetTempPath(), "lunar-events-tests", Guid.NewGuid().ToString("N"));
		try
		{
			var repository = new JsonCalendarEventRepository(directory);
			Assert.Empty(await repository.GetAllAsync());
			Directory.CreateDirectory(directory);
			await File.WriteAllTextAsync(Path.Combine(directory, "calendar-events.json"), "[]");
			Assert.Empty(await repository.GetAllAsync());
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Theory]
	[InlineData("")]
	[InlineData(" \r\n\t")]
	[InlineData("{ invalid json }")]
	public async Task Json_repository_rejects_invalid_data_and_preserves_original_file(string contents)
	{
		var directory = Path.Combine(Path.GetTempPath(), "lunar-events-tests", Guid.NewGuid().ToString("N"));
		var path = Path.Combine(directory, "calendar-events.json");
		try
		{
			Directory.CreateDirectory(directory);
			await File.WriteAllTextAsync(path, contents);
			var repository = new JsonCalendarEventRepository(directory);
			var readError = await Assert.ThrowsAsync<CalendarEventPersistenceException>(() => repository.GetAllAsync());
			Assert.Contains("Dữ liệu gốc vẫn được giữ nguyên", readError.Message);
			await Assert.ThrowsAsync<CalendarEventPersistenceException>(() => repository.SaveAsync(Event("Should not replace data", EventCalendarType.Gregorian, 1, 1)));
			await Assert.ThrowsAsync<CalendarEventPersistenceException>(() => repository.DeleteAsync(Guid.NewGuid()));
			Assert.Equal(contents, await File.ReadAllTextAsync(path));
		}
		finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
	}

	[Fact]
	public async Task Invalid_event_data_is_rejected()
	{
		var service = NewService();
		await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Event(" ", EventCalendarType.Gregorian, 1, 1)));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.CreateAsync(Event("Reminder", EventCalendarType.Gregorian, 1, 1) with { ReminderDaysBefore = 31 }));
		await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Event("Missing year", EventCalendarType.Gregorian, 1, 1, EventRecurrenceType.OneTime)));
		await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Event("Short lunar month", EventCalendarType.Lunar, 30, 2, EventRecurrenceType.OneTime, 2025)));
		await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(Event("Invalid leap", EventCalendarType.Lunar, 1, 2, EventRecurrenceType.OneTime, 2024, true)));
	}

	private CalendarEventService NewService() => new(new MemoryEventRepository(), lunar);

	private static CalendarEvent Event(string title, EventCalendarType type, int day, int month,
		EventRecurrenceType recurrence = EventRecurrenceType.Yearly, int? year = null, bool leap = false) =>
		new() { Title = title, CalendarType = type, Day = day, Month = month, RecurrenceType = recurrence, Year = year, IsLeapMonth = leap };

	private sealed class MemoryEventRepository : ICalendarEventRepository
	{
		private readonly Dictionary<Guid, CalendarEvent> events = new();
		public Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CalendarEvent>>(events.Values.ToArray());
		public Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(events.GetValueOrDefault(id));
		public Task SaveAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default) { events[calendarEvent.Id] = calendarEvent; return Task.CompletedTask; }
		public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { events.Remove(id); return Task.CompletedTask; }
	}
}
