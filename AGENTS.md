# Repository guidelines

## Architecture overview

- `src/Sefirah/App.xaml.cs` — app startup and window lifecycle.
- `src/Sefirah/Services/` — long-lived workflows: networking, clipboard, notifications, ADB, screen mirroring, file transfer, settings.
- `src/Sefirah/Data/` — holds contracts, enums, runtime models, socket message types, SQLite entities, repositories, and migrations.
- `Platforms/Windows/` and `Platforms/Desktop/` contains platform-specific implementations.
- `Helpers/`, `Extensions/`, `Utils/` — shared support code. Reuse before writing one-off helpers.

## Implementation rules

- Keep cross-platform behavior behind `Data/Contracts`; check existing platform implementations first, then register in each `ServiceCollectionExtensions.cs`.
- Implement user-specific and device-specific settings in `GeneralSettingsService` and `DeviceSettingsService` respectively.
- For db changes, add or update entities in `Data/AppDatabase/Models`, then applicable repositories in `Data/AppDatabase/Repository`, and add/update migrations for schema changes.
- For protocol changes, update `Data/Models/SocketMessage.cs`, route in `Services/MessageHandler.cs`, and use `BaseRemoteDevice.SendMessage(...)` to sending.
- Prefer primary constructors when possible, and follow existing CommunityToolkit MVVM patterns.
- Use `logger.Info`, `logger.Warn`, and `logger.Error` (Uno.Core.Extensions.Logging).
- Keep changes small and reuse existing seams before adding new abstractions.
- UI changes should consider accessibility; use Accessibility Insights for Windows when the change touches keyboarding, focus, or visible controls.

## Localization

- Only edit `src/Sefirah/Strings/en/Resources.resw` for new strings.
- C#: `"stringKey".GetLocalizedResource()`
- XAML: `xmlns:helpers="using:Sefirah.Helpers"` → `{helpers:ResourceString Name=stringKey}`