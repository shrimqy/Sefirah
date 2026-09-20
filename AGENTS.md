# Repository Guidelines

## Purpose and Review Bar
This repository contains the desktop side of Sefirah, built with Uno Platform and WinUI-style XAML. Changes are reviewed closely. Prefer simple, explicit code that matches the existing design over clever abstractions, broad refactors, or framework churn. If an existing service, repository, helper, or model already covers the job, extend it instead of introducing a parallel path. The goal is consistency, reliability, cleanliness, separation of concerns, and minimal required code changes.

## Architecture Map
- `src/Sefirah/App.xaml.cs` owns app startup and window lifecycle.
- `src/Sefirah/Helpers/AppLifecycleHelper.cs` is the composition root. Register shared services, repositories, and app-level singleton viewmodels here. Not all viewmodels are registered in DI; some (e.g., `DeviceSettingsViewModel`) are constructed directly by their host view.
- `src/Sefirah/ViewModels` contains page/state logic. `Views` and `Dialogs` are mostly XAML plus UI event plumbing.
- `src/Sefirah/Services` contains long-lived workflows: networking, clipboard, notifications, ADB, screen mirroring, file transfer, and settings.
- `src/Sefirah/Data` holds contracts, enums, runtime models, socket message types, SQLite entities, repositories, and migrations.
- `src/Sefirah/Platforms/Windows` and `src/Sefirah/Platforms/Desktop` contain platform-specific implementations behind shared contracts.
- `src/Sefirah/Strings/*/Resources.resw` contains localized UI strings. `src/Sefirah/Assets` contains icons and splash content.
- Ignore generated output in `bin/`, `obj/`, and `AppPackages/`.

## Startup Flow
- Windows entry point: `src/Sefirah/Platforms/Windows/Program.cs`.
- Desktop entry point: `src/Sefirah/Platforms/Desktop/Program.cs`.
- Both end up creating `App`, which calls `ConfigureApp(...)` in `AppLifecycleHelper`.
- `AppLifecycleHelper.ConfigureApp(...)` wires DI, logging, configuration, repositories, services, and page viewmodels.
- `AppLifecycleHelper.InitializeAppComponentsAsync()` starts the app subsystems in practice: device manager, media, actions, notifications, clipboard, networking, discovery, ADB, and update checks.
- Platform-specific implementations are registered in `Platforms/*/ServiceCollectionExtensions.cs`. If a feature behaves differently on Windows and Desktop, start there.

## Directory and File Map
- `src/Sefirah/Data/Contracts`: service interfaces and shared abstractions such as `IDeviceManager`, `INetworkService`, `IScreenMirrorService`, `ISftpService`.
- `src/Sefirah/Data/Enums`: protocol, preference, and feature enums. Add enum-backed behavior here before scattering raw ints or strings.
- `src/Sefirah/Data/Models`: runtime models used across services and UI. `SocketMessage.cs` is the protocol hub for device-to-device messages.
- `src/Sefirah/Data/Models/Messages`: SMS/message-specific runtime models.
- `src/Sefirah/Data/Items`: lightweight UI/support items such as codec or language selector entries.
- `src/Sefirah/Data/AppDatabase/Models`: SQLite entities only. Keep persistence shape separate from runtime view models when possible.
- `src/Sefirah/Data/AppDatabase/Repository`: repository layer for SQLite access. Existing features usually go through a repository instead of querying `DatabaseContext` from viewmodels or pages.
- `src/Sefirah/Data/AppDatabase/Migrations`: schema upgrades. If persisted data changes, check whether a migration is required instead of relying on destructive fallback.
- `src/Sefirah/Services/Settings`: JSON-backed general and per-device settings. This is the canonical place for user preferences.
- `src/Sefirah/Views/Settings` and `src/Sefirah/ViewModels/Settings`: settings UI and corresponding logic.
- `src/Sefirah/Platforms/Windows/RemoteStorage`: Windows-only shell sync root and remote storage implementation. This is specialized code; avoid casual refactors here.
- `src/Sefirah/Helpers`, `src/Sefirah/Extensions`, `src/Sefirah/Utils`: shared support code. Reuse these before introducing one-off helpers inside feature folders.

## Build and Run
- `dotnet restore src/Sefirah.sln`
- `dotnet build src/Sefirah.sln`
- `dotnet run --project src/Sefirah/Sefirah.csproj -f net9.0-windows10.0.26100`
- `dotnet run --project src/Sefirah/Sefirah.csproj -f net9.0-desktop`

There is no real automated test suite in the repo today. A solid change includes a successful build and manual verification of the affected flow.

## Project Conventions
- Follow `src/.editorconfig`: file-scoped namespaces, 4-space indentation for C# and XAML, 2 spaces for JSON/YAML.
- Shared services and repositories usually use constructor injection, often with primary constructors. Viewmodels and views commonly resolve dependencies via `Ioc.Default.GetRequiredService(...)`. Match the surrounding file instead of mixing styles inside it.
- Use CommunityToolkit.Mvvm patterns already present here: `ObservableObject`, `[ObservableProperty]`, and `[RelayCommand]`.
- Keep UI mutation on `App.MainWindow.DispatcherQueue.EnqueueAsync(...)` when touching `ObservableCollection`, bound properties, or WinUI objects from background work.
- Log at service boundaries and catch exceptions where the app can recover. Do not add silent catch blocks unless failure is genuinely optional and already documented as such.
- Do not hardcode user-facing strings in new code. Add a key to `Strings/en/Resources.resw` and use `"Key".GetLocalizedResource()`. Some older code still has hardcoded strings; prefer fixing them when you touch those areas, but do not refactor unrelated files just for this.
- Do not update non-English translation files during the main feature implementation pass. Add the required key to `Strings/en/Resources.resw`, keep a note of which strings still need translation, and defer the localized `Strings/<locale>/Resources.resw` updates until the feature is complete and the user confirms it is ready for translation follow-up.
- Prefer the existing layer boundaries: use repositories for SQLite access, settings services for persisted preferences, services for long-running workflows, viewmodels for page state, and code-behind only for control-specific event handling.
- Before adding a new abstraction, search for an existing one. This repo already has central seams for session management, app lists, notifications, clipboard, ADB, screen mirroring, and remote storage.

## Where to Put New Code
- New cross-platform capability: add/update a contract in `Data/Contracts`, implement it in `Platforms/Windows` and `Platforms/Desktop`, then register it in the matching `ServiceCollectionExtensions.cs`.
- New persisted user preference: add it to `GeneralSettingsService` or `DeviceSettingsService`. Do not create ad-hoc JSON files or alternate settings stores.
- New database-backed feature: add entity/model code under `Data/AppDatabase`, wire it through a repository, and update migrations or `DatabaseContext` schema handling.
- New socket/protocol message: add the type in `Data/Models/SocketMessage.cs`, then route it in `Services/MessageHandler.cs` and update the sending side.
- New page/feature UI: put view state and commands in a viewmodel; keep code-behind focused on visual event handling, navigation, and control-specific behavior.

## Persistence Map
- App configuration in `appsettings.json` is for app-level configuration loaded at startup, not user-editable runtime state.
- General user settings live in `GeneralSettingsService` and are JSON-backed through the custom serialization layer in `Utils/Serialization`. Both `GeneralSettingsService` and `DeviceSettingsService` are mediated by `IUserSettingsService` (`Services/Settings/UserSettingsService.cs`).
- Device-specific preferences live in `DeviceSettingsService`; use that when behavior differs per paired device.
- Durable feature data such as paired devices, notifications, apps, contacts, conversations, and messages lives in SQLite via `DatabaseContext` and repositories.
- If a feature needs both durable state and runtime state, keep the storage entity and the UI/runtime model separate unless the existing feature already couples them.

## Feature Map
- Discovery and pairing: `Services/DiscoveryService.cs`, `Services/NetworkService.cs`, `Services/DeviceManager.cs`, `Data/Models/SocketMessage.cs`.
- Session and protocol routing: `Services/NetworkService.cs` and `Services/MessageHandler.cs`.
- Clipboard sync: `Services/ClipboardService.cs`; file handoff goes through `Services/FileTransfer`.
- Notifications: `Services/NotificationService.cs`, `Data/AppDatabase/Repository/NotificationRepository.cs`, plus `Platforms/*/Services/*NotificationHandler.cs`.
- App list, app icons, pinning, and shortcuts: `Data/AppDatabase/Repository/RemoteAppRepository.cs`, `ViewModels/AppsViewModel.cs`, `Data/Models/ApplicationItem.cs`. Per-device notification filter updates flow through `ViewModels/Settings/DeviceSettingsViewModel.cs` and `RemoteAppRepository`.
- SMS and conversations: `Services/SmsHandlerService.cs`, `ViewModels/MessagesViewModel.cs`, `Data/AppDatabase/Repository/SmsRepository.cs`, `Data/Models/Messages`.
- Screen mirroring and scrcpy/ADB integration: `Services/ScreenMirrorService.cs`, `Services/AdbService.cs`, `ViewModels/Settings/DeviceSettingsViewModel.cs`.
- File transfer: `Services/FileTransfer/FileTransferService.cs`, `SendFileHandler.cs`, `ReceiveFileHandler.cs`.
- Remote storage: `Platforms/Windows/Services/WindowsSftpService.cs` and `Platforms/Windows/RemoteStorage/*`.
- Settings UI: `Views/Settings/*`, `Views/DeviceSettings/*`, and matching viewmodels in `ViewModels/Settings`.

## Safe Change Patterns
- When adding a new socket message, update the message type definition, the routing in `MessageHandler`, and every sender/receiver affected by the new message. Protocol work is rarely one-file-only here.
- When adding a new setting, wire the property into the correct settings service and consume it from the relevant service or viewmodel. Do not duplicate the same preference in both general and device settings unless the repo already needs both scopes.
- When adding or changing persisted data, inspect repository code first. Existing repositories often also maintain derived state, icons, sort order, or UI update events.
- When changing collections used by the UI, inspect whether the existing code marshals back to `DispatcherQueue` and preserve that pattern.
- When touching Windows-only shell or sync-root code, keep changes narrow and verify the call chain from `WindowsSftpService` through `Platforms/Windows/RemoteStorage`.
- When introducing or changing user-facing text, implement the English resource key now, mention the pending translation work in your summary, and leave locale file updates for a final dedicated pass.

## Verification Checklist
- Build the solution.
- Exercise the exact user flow you changed: pairing, discovery, clipboard sync, notification actions, SMS, app list refresh, screen mirroring, or file transfer.
- If you changed localized text, confirm the resource key resolves in the UI.
- If you changed persistence or schema code, verify upgrade behavior against existing local data.
- Check logs under the app local folder if behavior is unclear; Serilog writes rolling files to `Logs/`.
