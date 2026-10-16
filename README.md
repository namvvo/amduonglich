# Âm Dương Lịch

Vietnamese lunar calendar for Android, built with .NET 10 MAUI. The app works offline and stores user events on the device.

## Current implementation

Phases 1–3 are implemented: the Android MAUI app and clean architecture, the Vietnamese lunar conversion engine, and local calendar event management. The calendar displays event indicators and selected-date events. Event management supports create, edit, delete, Gregorian and lunar dates, annual recurrence, and one-time events.

## Event recurrence

- Annual Gregorian events match the same Gregorian day and month each year.
- Annual lunar events match the lunar day and month each year. Leap-month events match only the explicitly selected leap month; they do not match the regular month with the same number.
- One-time events require a year. Lunar one-time events use a lunar year, including at lunar new year transitions.
- A lunar day 30 event is skipped in years where that lunar month has 29 days. It does not move to day 29.
- Lunar calendar conversion and event dates support lunar years 1900–2100. The lunar year 2100 may extend briefly into January 2101.

## Local storage and reminders

Events are stored as JSON in the app's private data directory (`calendar-events.json`). A missing file and a valid empty array (`[]`) mean there are no saved events. Empty, whitespace-only, or malformed JSON causes a user-friendly persistence error; the original file is preserved, and writes or deletes do not replace it.

Events can store a reminder lead time from 0 to 30 days, but reminders are not scheduled or delivered yet. Android notification support is not part of the current implementation.

## Solution layout

- `src/VietnameseLunarCalendar.App`: MAUI Android presentation, Shell navigation, and CommunityToolkit.Mvvm.
- `src/VietnameseLunarCalendar.Domain`: domain entities, rules, and interfaces.
- `src/VietnameseLunarCalendar.Application`: use cases and application services.
- `src/VietnameseLunarCalendar.Infrastructure`: local persistence and lunar calendar algorithms.
- `tests/VietnameseLunarCalendar.Tests`: lunar calendar and event unit tests.

## Prerequisites

- .NET SDK 10.0
- .NET MAUI Android workload
- Android SDK and an Android emulator or device

## Build, run, and test

```powershell
dotnet workload restore
dotnet restore VietnameseLunarCalendar.sln
dotnet build VietnameseLunarCalendar.sln --configuration Debug
dotnet build src/VietnameseLunarCalendar.App/VietnameseLunarCalendar.App.csproj -t:Run -f net10.0-android
dotnet test VietnameseLunarCalendar.sln --configuration Debug
```

See [docs/vietnamese-lunar-calendar.md](docs/vietnamese-lunar-calendar.md) for the astronomical algorithm, UTC+7 assumptions, supported range, independent verification sources, and lunar calendar tests.
