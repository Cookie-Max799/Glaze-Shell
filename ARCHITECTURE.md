# Архитектура Glaze Shell

## Цели

Glaze Shell строится как модульное Windows-приложение поверх официальных механизмов Windows. Архитектура разделяет UI, Core, Infrastructure, Data и Windows Integration так, чтобы пользовательские данные, системные детали и визуальный слой не смешивались.

## Структура

```text
GlazeShell/
├── README.md
├── ARCHITECTURE.md
├── CHANGELOG.md
├── LICENSE
├── .gitignore
├── .editorconfig
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── docs/
├── src/
│   ├── GlazeShell.App/
│   ├── GlazeShell.Core/
│   │   ├── Configuration/
│   │   ├── Events/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Services/
│   ├── GlazeShell.Windows/
│   ├── GlazeShell.Data/
│   └── GlazeShell.Infrastructure/
├── tests/
│   ├── GlazeShell.Core.Tests/
│   └── GlazeShell.Windows.Tests/
└── assets/
```

## Dependency flow

```text
UI
 ↓
ViewModels и Application Services
 ↓
Core
 ↓
Interfaces
 ↓
Windows Integration
 ↓
Win32 / Windows API
 ↓
Windows
```

Core не должен содержать P/Invoke, `HWND`, `HMONITOR`, `HANDLE` или прямые вызовы User32. Такие детали должны быть скрыты за интерфейсами и реализациями Windows Integration.

## Слои

### GlazeShell.App

WinUI 3 executable и composition root приложения. Содержит окно с явно заданным размером и центрированием на экране. На Stage 3 подключает `ApplicationDiscoveryService` и `WindowsApplicationLauncher`: создаёт реальный лаунчер-UI с поиском, списком приложений, статусной строкой и клавиатурной навигацией. UI-слой (`Presentation/MainViewModel`, `AsyncCommand`, `DispatcherQueueExtensions`) общается только с Core-интерфейсами.

### GlazeShell.Core

Независимый от UI и Win32 слой. Содержит доменные модели, контракты интерфейсов, события и базовые сервисы.

Модели:

- `Application`, `ApplicationCategory`, `DesktopItem`, `DesktopTab`, `DesktopLayout`;
- `UserSettings`;
- `WindowInfo`, `WindowType`, `WindowState`;
- `MonitorInfo`, `MonitorBounds`, `MonitorOrientation`;
- `Theme` с metadata, colors, fonts, dimensions, icons и анимацией;
- `ModelValidation` — единая валидация входных данных моделей.

Discovery models: `ApplicationCandidate`, `ApplicationDiscoveryResult`, `ApplicationDiscoveryOptions`, `ApplicationIdentity`, `ApplicationLaunchResult`.

Интерфейсы: `IApplicationManager`, `IApplicationLauncher`, `IApplicationDiscoverySource`, `IProcessInspector`, `IPackageLocationResolver`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IThemeManager`, `IEventManager`, `IMonitorManager`.

События: `WindowOpened`, `WindowClosed`, `ForegroundWindowChanged`, `ProcessStarted`, `ProcessExited`, `DisplayChanged`, `DesktopChanged`, `SettingsChanged`, `ApplicationChanged`.

Сервисы: `EventManager` — типизированная подписка и публикация с возвратом `IDisposable` для отписки; `InMemorySettingsManager` — хранение настроек в памяти без публикации дублирующих событий; `ApplicationDiscoveryService` — агрегация источников, dedup, cache и single-flight.

Идентификаторы окон, мониторов и приложений представлены строками, чтобы Core не зависел от `HWND`/`HMONITOR`. Соответствие строк и native handles устанавливается на уровне Windows Integration.

### GlazeShell.Windows

Слой Windows Integration. На Stage 2/3 содержит:

- `Shell/ShellLinkData.cs` — byte-safe managed парсер MS-SHLLINK (header, LinkInfo, StringData, relative path);
- `Shell/ShellLinkResolver.cs` — трёхуровневый резолвер: managed fast path → property store → `IShellLinkW`;
- `Shell/AppsFolderSource.cs` — обнаружение MSIX через `IShellItemArray`;
- `Shell/StartMenuShortcutSource.cs` — параллельное сканирование Start Menu;
- `Shell/IShellItem*.cs`, `IShellFolder.cs`, `IShellLinkW.cs`, `IPersistFile.cs` — официальные COM-интерфейсы;
- `Interop/ComApartment.cs` — STA-исполнение COM с балансом `CoInitializeEx`/`CoUninitialize`;
- `Interop/IApplicationActivationManager.cs` — активация MSIX;
- `Applications/WindowsApplicationLauncher.cs`, `ProcessInspector.cs`, `PackageInstallLocationResolver.cs` — Stage 3 launcher;
- `Win32/Ole32.cs`, `Shell32.cs` — P/Invoke объявления.

### GlazeShell.Data

Слой хранения и сериализации. Структура подготовлена, но JSON persistence, layout и SQLite намеренно не реализованы до Stage 7.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest test project. Проверяет foundation configuration, инварианты моделей, семантику `EventManager`, публикацию `SettingsChanged` в `InMemorySettingsManager` и discovery service.

### GlazeShell.Windows.Tests

MSTest test project. Проверяет managed резолвер `.lnk` (`ShellLinkResolverTests`), источники Start Menu и AppsFolder, `WindowsApplicationLauncher`. Использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо сломанного в окружении COM `IShellLinkW.Save`.

## Решения Stage 0

- Решение использует `GlazeShell.slnx`, поддерживаемый установленным .NET 10 SDK.
- Для App используется ручной unpackaged WinUI 3 scaffold, поскольку в окружении отсутствует WinUI template.
- `GlazeShell.Windows` уже ориентирован на Windows TFM, но не содержит системных вызовов.
- Пользовательские данные будут храниться в `%LOCALAPPDATA%\GlazeShell`; на этом этапе создаётся только logs path.
- Версии NuGet-пакетов фиксируются через `Directory.Packages.props` и не используют floating versions.
- `Core/Configuration` добавлен как объективное исключение из базовой структуры: базовая схема конфигурации должна быть доступна UI и Infrastructure без Win32-зависимостей.

## Решения Stage 1

- Отдельный `GlazeShell.Application` проект не создаётся: на этом этапе достаточно `GlazeShell.Core/Services`, а новый слой появится только вместе с реальными use cases.
- Идентификаторы сущностей (`WindowInfo.Id`, `MonitorInfo.Id`, `Application.Id`) — строки, а не native handles; это сохраняет Core framework-free.
- Модели валидируются в конструкторах через `ModelValidation`, чтобы некорректное состояние не могло покинуть границу Core.
- Модели объявлены `record`, что даёт value-семантику: `InMemorySettingsManager` может отсекать публикацию `SettingsChanged` для эквивалентных значений без ручного сравнения полей.
- События наследуются от `GlazeEvent` и публикуются только через `IEventManager`, чтобы UI и будущие Windows-реализации имели единый event-driven контракт.
- `EventManager` возвращает `IDisposable` из `Subscribe`, чтобы подписки гарантированно освобождались и не удерживали UI-объекты.
- Ошибка подписчика изолируется: при заданном `exceptionHandler` она логируется вызывающей стороной, а не прерывает доставку остальным подписчикам.
- Темы и обои только описываются моделями; загрузка и исполнение контента тем остаётся за пределами Core.
- Окно задаёт размер и позицию через `AppWindow`, а не полагается на поведение XAML по умолчанию: без явного `Resize` окно WinUI может получить нулевой размер.
- Платформа сборки App фиксируется как `x64`; значение `AnyCPU`, которое `dotnet run` передаёт по умолчанию, принудительно заменяется на `x64` в `.csproj`.
- Stage 1 не реализует discovery, launcher, Win32 interop, persistence, tabs/categories behavior и Windows event hooks.

## Решения Stage 2 — Discovery

- Managed парсер `ShellLinkData` является первичным путём для `.lnk`: он не требует COM и работает в повреждённом окружении, где `CLSID_ShellLink` зарегистрирован не в `shell32.dll`.
- `ShellLinkResolver` возвращает enum-стратегию резолва; failure возвращает описание для диагностики, а не исключение.
- CORP-отказ от сообщений: неразрешённые `.lnk` пропускаются с warning в `ApplicationDiscoveryResult`, discovery не падает целиком.
- Nodes/reparse points, `.url`, `explorer.exe` и targets без `RequireExistingTarget`-проверки отфильтровываются на уровне источника.
- `StartMenuShortcutSource` сканирует параллельно с bound `degreeOfParallelism = min(max(1, CPU), 8)` и выдерживает детерминированную сортировку результата.
- COM-интерфейсы не используют generic методы и создаются через `CoCreateInstance` с явными IIDs; веществами освобождаются в `finally`.
- AppsFolder на текущей машине возвращает `0x800401E5`; на штатных системах источник возвращает пакеты MSIX, иначе — объясняющий warning.

## Решения Stage 3 — Launcher

- `IApplicationActivationManager` реализует корректную vtable: `GetApplicationUserModelId` принимает process handle, а для PID используется `GetApplicationUserModelIdFromProcessId`.
- Win32 запуск использует `ProcessStartInfo` с `UseShellExecute=false` и не полагается на SHELLEXECUTE-командную строку.
- Закрытие выполняется только через `CloseMainWindow`; принудительный `Kill` не применяется.
- `ProcessInspector` освобождает каждый `Process` через `using` и проверяет границу каталога (а не `StartsWith` без разделителя).
- `PackageInstallLocationResolver` строит immutable cache реестра один раз и не держит открытый `RegistryKey` после инициализации.
- `ComApartment` выполняет COM в STA и балансирует `CoInitializeEx`/`CoUninitialize`; результат исключения пробрасывается в вызывающий поток через `ExceptionDispatchInfo`.

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн согласован: палитра (`#12151B`, `#1B1F27`, `#F4F6FA`, `#98A2B3`, `#6E7A8A`, `#4E5866`), шрифты и layout сохраняются без явного согласования изменений.

## Следующие архитектурные изменения

Stages 5–6 добавят window management, monitor/DPI integration и Windows event hooks. Перед добавлением каждого P/Invoke или Windows hook будут проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
