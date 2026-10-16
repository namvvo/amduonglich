using System.Text.Json;
using System.Diagnostics;
using VietnameseLunarCalendar.Domain.Interfaces;
using VietnameseLunarCalendar.Domain.Models;

namespace VietnameseLunarCalendar.Infrastructure.CalendarEvents;

/// <summary>Stores events in an atomic JSON file in the application's private data directory.</summary>
public sealed class JsonCalendarEventRepository(string dataDirectory) : ICalendarEventRepository
{
	private readonly SemaphoreSlim gate = new(1, 1);
	private readonly object cacheSync = new();
	private readonly string filePath = Path.Combine(dataDirectory, "calendar-events.json");
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private IReadOnlyList<CalendarEvent>? cachedEvents;
	private Task<IReadOnlyList<CalendarEvent>>? cacheLoadTask;

	/// <inheritdoc />
	public async Task<IReadOnlyList<CalendarEvent>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		var timer = Stopwatch.StartNew();
		Task<IReadOnlyList<CalendarEvent>> loadTask;
		TaskCompletionSource<IReadOnlyList<CalendarEvent>>? loadOwner = null;
		lock (cacheSync)
		{
			if (cachedEvents is not null)
			{
				timer.Stop();
				Debug.WriteLine($"PERF JSON repository cache hit: {timer.Elapsed.TotalMilliseconds:F3} ms; {cachedEvents.Count} events.");
				return cachedEvents;
			}
			if (cacheLoadTask is null)
			{
				loadOwner = new TaskCompletionSource<IReadOnlyList<CalendarEvent>>(TaskCreationOptions.RunContinuationsAsynchronously);
				cacheLoadTask = loadOwner.Task;
			}
			loadTask = cacheLoadTask;
		}
		if (loadOwner is not null) _ = LoadCacheAsync(loadOwner);
		try
		{
			var result = await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			timer.Stop();
			Debug.WriteLine($"PERF JSON repository shared load wait: {timer.Elapsed.TotalMilliseconds:F3} ms; {result.Count} events.");
			return result;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
	}

	private async Task LoadCacheAsync(TaskCompletionSource<IReadOnlyList<CalendarEvent>> completion)
	{
		try
		{
			await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
			IReadOnlyList<CalendarEvent> snapshot;
			try
			{
				snapshot = cachedEvents ?? (await ReadAsync(CancellationToken.None).ConfigureAwait(false)).ToArray();
				lock (cacheSync) cachedEvents = snapshot;
			}
			finally { gate.Release(); }
			Debug.WriteLine($"PERF JSON repository cache fill complete; {snapshot.Count} events.");
			completion.TrySetResult(snapshot);
		}
		catch (Exception exception)
		{
			lock (cacheSync)
				if (ReferenceEquals(cacheLoadTask, completion.Task)) cacheLoadTask = null;
			completion.TrySetException(exception);
		}
	}

	/// <inheritdoc />
	public async Task<CalendarEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
		(await GetAllAsync(cancellationToken)).FirstOrDefault(e => e.Id == id);

	/// <inheritdoc />
	public async Task SaveAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			var events = (cachedEvents ?? (await ReadAsync(cancellationToken)).ToArray()).ToList();
			var index = events.FindIndex(e => e.Id == calendarEvent.Id);
			if (index < 0) events.Add(calendarEvent); else events[index] = calendarEvent;
			await WriteAsync(events, cancellationToken);
			lock (cacheSync) cachedEvents = events.ToArray();
		}
		finally { gate.Release(); }
	}

	/// <inheritdoc />
	public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await gate.WaitAsync(cancellationToken);
		try
		{
			var events = (cachedEvents ?? (await ReadAsync(cancellationToken)).ToArray()).Where(e => e.Id != id).ToArray();
			await WriteAsync(events, cancellationToken);
			lock (cacheSync) cachedEvents = events;
		}
		finally { gate.Release(); }
	}

	private async Task<IReadOnlyList<CalendarEvent>> ReadAsync(CancellationToken token)
	{
		if (!File.Exists(filePath))
		{
			Debug.WriteLine("PERF JSON file read: missing file; 0 ms; deserialize: 0 ms.");
			return Array.Empty<CalendarEvent>();
		}
		var readTimer = Stopwatch.StartNew();
		var readThread = Environment.CurrentManagedThreadId;
		var json = await File.ReadAllTextAsync(filePath, token).ConfigureAwait(false);
		readTimer.Stop();
		if (string.IsNullOrWhiteSpace(json))
			throw new CalendarEventPersistenceException("Tệp sự kiện đang trống hoặc chỉ chứa khoảng trắng. Dữ liệu gốc vẫn được giữ nguyên.");

		var deserializeTimer = Stopwatch.StartNew();
		try
		{
			var result = JsonSerializer.Deserialize<List<CalendarEvent>>(json, JsonOptions)
				?? throw new CalendarEventPersistenceException("Tệp sự kiện không chứa danh sách hợp lệ. Dữ liệu gốc vẫn được giữ nguyên.");
			deserializeTimer.Stop();
			Debug.WriteLine($"PERF JSON file read: {readTimer.Elapsed.TotalMilliseconds:F3} ms; {json.Length} chars; thread {readThread}->{Environment.CurrentManagedThreadId}. Deserialize: {deserializeTimer.Elapsed.TotalMilliseconds:F3} ms; {result.Count} events.");
			return result;
		}
		catch (JsonException exception)
		{
			throw new CalendarEventPersistenceException("Không thể đọc tệp sự kiện vì dữ liệu JSON không hợp lệ. Dữ liệu gốc vẫn được giữ nguyên.", exception);
		}
	}

	private async Task WriteAsync(IReadOnlyCollection<CalendarEvent> events, CancellationToken token)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
		var tempPath = filePath + ".tmp";
		await using (var stream = File.Create(tempPath))
			await JsonSerializer.SerializeAsync(stream, events, JsonOptions, token);
		File.Move(tempPath, filePath, true);
	}
}
