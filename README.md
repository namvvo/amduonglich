# Âm Dương Lịch

Vietnamese lunar calendar for Android, built with .NET 10 MAUI. The application is designed to work offline and keep user data on the device.

## Current phase

Phase 1 establishes the Android MAUI project, the clean-architecture solution layout, dependency injection, and an MVVM-based calendar entry screen. The lunar conversion engine, storage, event management, notifications, and settings are subsequent implementation phases.

## Solution layout

- `src/VietnameseLunarCalendar.App`: MAUI Android presentation, Shell navigation, and CommunityToolkit.Mvvm.
- `src/VietnameseLunarCalendar.Domain`: domain entities, rules, and interfaces.
- `src/VietnameseLunarCalendar.Application`: use cases and application services.
- `src/VietnameseLunarCalendar.Infrastructure`: local persistence, calendar algorithms, and Android integrations.
- `tests/VietnameseLunarCalendar.Tests`: planned unit tests.

## Prerequisites

- .NET SDK 10.0
- .NET MAUI Android workload
- Android SDK and an Android emulator or device

## Build and run

```powershell
dotnet workload restore
dotnet restore VietnameseLunarCalendar.sln
dotnet build VietnameseLunarCalendar.sln -f net10.0-android
dotnet build src/VietnameseLunarCalendar.App/VietnameseLunarCalendar.App.csproj -t:Run -f net10.0-android
```

See [docs/vietnamese-lunar-calendar.md](docs/vietnamese-lunar-calendar.md) for the Phase 2 astronomical algorithm, UTC+7 assumptions, supported range, independent verification sources, and test command.
