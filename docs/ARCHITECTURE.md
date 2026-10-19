# Архитектура Glaze Shell

## Цели

Glaze Shell строится как модульное Windows-приложение поверх официальных механизмов Windows. Архитектура разделяет UI, Core, Infrastructure, Data и Windows Integration так, чтобы пользовательские данные, системные детали и визуальный слой не смешивались.

## Структура

```text
GlazeShell/
├── README.md
├── CHANGELOG.md
├── LICENSE
├── GlazeShell.slnx
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── glaze.cmd
├── .editorconfig
├── .gitattributes
├── .gitignore
├── docs/
│   ├── assets/
│   ├── design/
│   ├── ARCHITECTURE.md
│   ├── USAGE.md
│   ├── DEVELOPMENT.md
│   ├── ROADMAP.md
│   ├── DESIGN.md
│   ├── FEATURES.md
│   ├── features.json
│   ├── SECURITY.md
│   └── RELEASE.md
├── src/
│   ├── GlazeShell.App/
│   │   ├── Assets/
│   │   └── Presentation/
│   ├── GlazeShell.Core/
│   │   ├── Configuration/
│   │   ├── Discovery/
│   │   ├── Events/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   ├── Persistence/
│   │   └── Services/
│   ├── GlazeShell.Data/
│   │   ├── Persistence/
│   │   ├── Serialization/
│   │   └── Themes/
│   ├── GlazeShell.Infrastructure/
│   │   ├── Logging/
│   │   └── System/
│   └── GlazeShell.Windows/
│       ├── Applications/
│       ├── DisplayManagement/
│       ├── Interop/
│       ├── Lifecycle/
│       ├── Shell/
│       ├── SystemEvents/
│       ├── Win32/
│       └── WindowManagement/
└── tests/
    ├── GlazeShell.Core.Tests/
    ├── GlazeShell.Data.Tests/
    └── GlazeShell.Windows.Tests/
```

Локальные каталоги, перечисленные в `.gitignore` и не попадающие в репозиторий: `assets-src/` (исходные визуальные ассеты) и `AGENTS.md` (инструкции для агентов).

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

WinUI 3 executable и composition root приложения. Содержит окно с явно заданным размером и центрированием на экране. На Stage 3 подключает `ApplicationDiscoveryService` и `WindowsApplicationLauncher`: создаёт реальный лаунчер-UI с поиском, списком приложений, статусной строкой и клавиатурной навигацией. На Stage 4 подключает `WindowManager` и события окон: UI-слой (`Presentation/MainViewModel`, `ApplicationListViewModel`, `WindowListItemViewModel`, `AsyncCommand`, `DispatcherQueueExtensions`) общается только с Core-интерфейсами. На Stage 7 создаёт `JsonUserDataStore`, читает конфигурацию один раз из каталога по умолчанию, определяет каталог данных, загружает layout с привязкой мониторов и настройки, подключает `DesktopLayoutPersistenceWriter` и `PersistingSettingsManager` и сбрасывает writer при закрытии окна. На Stage 10 удерживает `SingleInstanceLease`: в конструкторе захватывает именованный mutex, при повторном запуске активирует окно работающего экземпляра и завершает процесс, а при закрытии окна освобождает lease вместе с остальными ресурсами. В Phase 1 (H1) создаёт `Theming/ThemeApplicator` и применяет оформление к корневому элементу окна: переключает `RequestedTheme` и перекрашивает токены-кисти. В Phase 1 (A6) создаёт и показывает `TopBarWindow` — плавающую верхнюю панель — и привязывает её присутствие к настройке `topBarEnabled` (создаёт при старте и при включении, закрывает при выключении и на shutdown). В Phase 1 (A1) создаёт `DockWindow` — плавающий нижний dock — и `TaskbarHider`, который скрывает системную панель задач, пока включены `dockEnabled` и `dockReplacesTaskbar`; при выходе, выключении dock или настройки панель восстанавливается.

### GlazeShell.Core

Независимый от UI и Win32 слой. Содержит доменные модели, контракты интерфейсов, события и базовые сервисы.

Модели:

- `Application`, `ApplicationCategory`, `DesktopItem`, `DesktopTab`, `DesktopLayout`;
- `UserSettings`;
- `WindowInfo`, `WindowType`, `WindowState`;
- `MonitorInfo`, `MonitorBounds`, `MonitorOrientation`;
- `Theme` с metadata, colors, fonts, dimensions, icons и анимацией;
- `ModelValidation` — единая валидация входных данных моделей.

Discovery models: `ApplicationCandidate`, `ApplicationDiscoveryResult`, `ApplicationDiscoveryOptions`, `ApplicationIdentity`, `ApplicationLaunchResult`, `PackageIdentity`. Сопоставление процессов и окон с приложением выполняет framework-free `ApplicationProcessMatcher` (`Discovery/ApplicationProcessMatcher.cs`): одна и та же логика используется лаунчером и UI.

Интерфейсы: `IApplicationManager`, `IApplicationLauncher`, `IApplicationDiscoverySource`, `IProcessInspector`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IUserDataStore`, `IThemeManager`, `IEventManager`, `IMonitorManager`.

События: `WindowOpened`, `WindowClosed`, `ForegroundWindowChanged`, `WindowStateChanged`, `ProcessStarted`, `ProcessExited`, `DisplayChanged`, `DesktopChanged`, `SettingsChanged`, `ApplicationChanged`.

Сервисы: `EventManager` — типизированная подписка и публикация с возвратом `IDisposable` для отписки; `InMemorySettingsManager` — хранение настроек в памяти без публикации дублирующих событий; `ApplicationDiscoveryService` — агрегация источников, dedup, cache и single-flight; `DesktopManager` — in-memory манипуляции с `DesktopLayout` (вкладки, категории, размещение приложений и канонический порядок на трёх уровнях) с публикацией `DesktopChanged`; `MonitorLayoutBinding` — framework-free сверка привязок вкладок к списку доступных мониторов; `ThemeManager` — реестр тем, выбор действующей темы и публикация `ThemeChanged` (темы неизменяемы после загрузки, а при отсутствии выбранной темы действует встроенная `Theme.CreateDefault()`).

Persistence contracts: `PersistenceStatus` (`Created`/`Loaded`/`Migrated`/`Recovered`/`Unsupported`), `PersistenceLoadResult<T>` (значение, статус, диагностика) и `UserDataDirectoryName` — единая валидация имени каталога пользовательских данных, используемая и Infrastructure, и слоем Data.

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
- `Applications/WindowsApplicationLauncher.cs`, `ProcessInspector.cs` — Stage 3 launcher (сопоставление процессов и окон с приложением — `ApplicationProcessMatcher` в Core);
- `Win32/Ole32.cs`, `Shell32.cs` — P/Invoke объявления.

На Stage 4 добавляет:

- `Win32/User32.cs` — P/Invoke user32 (окна, сообщения, hooks, threads) с `CharSet.Unicode` без `ExactSpelling`;
- `Win32/Dwmapi.cs` — `DwmGetWindowAttribute`/`DWMWA_CLOAKED` для отбрасывания cloak-нутых окон;
- `Win32/Kernel32.cs` — `GetCurrentThreadId`;
- `WindowManagement/NativeWindowEnumerator.cs` — перечисление и чтение окон по HWND (id = HEX-представление handle);
- `WindowManagement/WindowEventMonitor.cs` — `SetWinEventHook` на dedicated-потоке с `GetMessage`-pump, публикация оконных событий в `IEventManager`;
- `WindowManagement/WindowManager.cs` — реализация `IWindowManager`: фокус (best-effort + `AttachThreadInput` fallback), `ShowWindowAsync` и `WM_CLOSE`.

На Stage 5 добавляет:

- `DisplayManagement/NativeMonitorEnumerator.cs` — перечисление мониторов через `EnumDisplayMonitors`, чтение `MONITORINFOEXW`/`DEVMODEW`, DPI через `GetDpiForMonitor`/`GetDpiForSystem`; id = HEX-представление `HMONITOR`;
- `DisplayManagement/MonitorEventMonitor.cs` — hidden message window на dedicated-потоке, перехват `WM_DISPLAYCHANGE`;
- `DisplayManagement/MonitorManager.cs` — реализация `IMonitorManager` (`Start`/`Dispose` по паттерну `WindowManager`), публикация `DisplayChanged`;
- `Win32/Shcore.cs` — `GetDpiForMonitor`; display P/Invoke добавлены в `Win32/User32.cs` (`EnumDisplayMonitors`, `GetMonitorInfoW`, `MonitorFrom*`, `EnumDisplaySettingsW`, `RegisterClassW`, `CreateWindowExW`, `DefWindowProc`), расширен `Win32/Kernel32.cs` (`GetModuleHandle`).

На Stage 9 добавляет:

- `SystemEvents/ShellEventCoordinator.cs` — единая точка запуска источников системных событий с диагностикой отказов;
- `SystemEvents/ProcessEventMonitor.cs`, `SystemEvents/ProcessWatchOptions.cs` — события процессов по разнице снимков;
- контракт `IWindowsEventSource` (`SystemEvents/IWindowsEventSource.cs`) — `Start()` + `IDisposable` для источников Windows-событий.

На Stage 10 добавляет:

- `Lifecycle/SingleInstanceHelper.cs` — `SingleInstanceHelper` и `SingleInstanceLease`: именованный mutex, удерживаемый весь срок работы, и активация окна уже работающего экземпляра (`FindOwnVisibleWindow`, `ActivateExistingInstance`).

На Phase 1 (A1) добавляет:

- `Shell/TaskbarHider.cs` — обратное скрытие системной панели задач: `Hide`/`Restore`, исходное положение `Shell_TrayWnd`, повторное скрытие по `TaskbarCreated` и `WM_DISPLAYCHANGE` через hidden message window на отдельном потоке, статический `RestoreIfOffScreen` для восстановления после аварийного завершения.

### GlazeShell.Data

Слой хранения и сериализации. Реализует JSON persistence пользовательских данных: `JsonUserDataStore` (`IUserDataStore`) читает и пишет `config.json`, `settings.json` и `layout.json`; `JsonDocumentPipeline` выполняет общий путь загрузки (лимит размера, разбор, миграции, маппинг, recovery); `SchemaMigrationRunner` применяет цепочку миграций одного типа документа; `UserDataDirectory` реализует атомарную запись, резервные копии и изоляцию повреждённых документов. Подкаталог `Themes/` отвечает за чтение пользовательских тем: `ThemeStore` читает `themes/*.json` и возвращает `ThemeLoadResult` с темами и диагностикой, `ThemeDocumentMapper` проверяет документ темы. Слой зависит только от Core и `System.Text.Json`, поэтому тестируется без Windows и без UI. SQLite в текущем дизайне не используется: документы пользователя малы и должны оставаться читаемыми и переносимыми.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest test project. Проверяет foundation configuration, инварианты моделей, семантику `EventManager`, публикацию `SettingsChanged` в `InMemorySettingsManager`, discovery service, `ApplicationProcessMatcher` (точный путь, каталог установки с лаунчером, имя образа, аргумент `--processStart`, MSIX), `DesktopManager` (вкладки, активация, элементы, события `DesktopChanged`), `MonitorLayoutBinding`, `UserDataDirectoryName` и `ThemeManager` (порядок тем, выбор активной темы, `ThemeChanged`, запрет исполняемых ассетов и выхода за пределы каталога темы).

### GlazeShell.Data.Tests

MSTest test project. Проверяет `JsonUserDataStore` (round-trip, создание документов по умолчанию, recovery, изоляция, лимиты размера, атомарность записи, сохранение документов из будущих версий), `SchemaMigrationRunner` (последовательные миграции, пропуск версии, дубли, неподдерживаемые версии), `PersistingSettingsManager`, `DesktopLayoutPersistenceWriter` и `ThemeStore` (загрузка тем, отклонение непригодных по отдельности, лимиты количества и размера, проверка формата цвета и безопасности ассетов). Каждый тест работает в собственном временном каталоге, который удаляется после теста; файловой системой имитируется только недоступность каталогов `backups` и `recovery`.

### GlazeShell.Windows.Tests

MSTest test project. Проверяет managed резолвер `.lnk` (`ShellLinkResolverTests`), источники Start Menu и AppsFolder, `WindowsApplicationLauncher`, window manager (`WindowManagerTests` с окном-фикстурой `Win32TestWindow` на pumping-потоке), monitor manager (`MonitorManagerTests`: перечисление мониторов, primary, геометрия, DPI и событие `DisplayChanged` через `HWND_BROADCAST`), источники событий (`ProcessEventMonitorTests`, `ShellEventCoordinatorTests`) и lifecycle (`SingleInstanceHelperTests`: захват и освобождение lease, отказ второму экземпляру, поиск и активация собственного окна). Использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо сломанного в окружении COM `IShellLinkW.Save`; environment-зависимые проверки (foreground lock, мониторы) завершаются `Inconclusive`, а не падают.

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
- Строки `LinkInfo` разбираются по MS-SHLLINK в их фактической кодировке: `LocalBasePath`/`CommonPathSuffix` на ANSI-офсетах (`0x10`/`0x18`) декодируются в ANSI-кодировке системы (`ANSICodePage` через `CodePagesEncodingProvider`, с fallback на Latin1), а Unicode-варианты (`0x1C`/`0x20`) читаются только при `LinkInfoHeaderSize >= 0x24`. Опора на флаг `IsUnicode` из `LinkFlags` была ошибкой: реальные ярлыки имеют `LinkInfoHeaderSize = 0x1C` и ANSI-строки, поэтому декодирование их как Unicode давало мусорный непустой путь, из-за чего managed-резолвер формально «успевал» и все ярлыки отбрасывались как неразрешимые (пустой список приложений).
- `ShellLinkResolver` возвращает enum-стратегию резолва; failure возвращает описание для диагностики, а не исключение.
- CORP-отказ от сообщений: неразрешённые `.lnk` пропускаются с warning в `ApplicationDiscoveryResult`, discovery не падает целиком.
- Nodes/reparse points, `.url`, `explorer.exe` и targets без `RequireExistingTarget`-проверки отфильтровываются на уровне источника.
- `StartMenuShortcutSource` сканирует параллельно с bound `degreeOfParallelism = min(max(1, CPU), 8)` и выдерживает детерминированную сортировку результата.
- COM-интерфейсы не используют generic методы и создаются через `CoCreateInstance` с явными IIDs; веществами освобождаются в `finally`.
- AppsFolder на текущей машине возвращает `0x800401E5`; на штатных системах источник возвращает пакеты MSIX, иначе — объясняющий warning.

## Решения Stage 3 — Launcher

- `IApplicationActivationManager` реализует корректную vtable: `GetApplicationUserModelId` принимает process handle, а для PID используется `GetApplicationUserModelIdFromProcessId`.
- Win32 запуск использует `ProcessStartInfo` с `UseShellExecute=false` и не полагается на SHELLEXECUTE-командную строку.
- Закрытие выполняется вежливо: `CloseMainWindow`, а если главное окно процесса не найдено — `WM_CLOSE` по всем top-level окнам процесса через `PostMessage`. Результат определяется не фактом отправки сообщения, а фактическим завершением процесса: после запроса метод ждёт выхода до `CloseTimeoutMilliseconds` и сообщает «закрыто» только тогда, когда приложение действительно завершилось (у части приложений, например Acrobat, `CloseMainWindow` возвращает `false`/закрывается медленно, хотя запрос отрабатывает).
- Принудительное завершение (`TerminateAsync`, аналог «Снять задачу») — отдельный явный путь, а не поведение по умолчанию: он вызывается только из диалога подтверждения в UI. Завершение идёт через BCL `Process.Kill(entireProcessTree: true)`, а фоновые процессы (например, `AcroCEF.exe` у Acrobat, запускаемые отдельно от дерева основного процесса) добиваются по каталогу установки. Расширение по каталогу подключается только для несистемных каталогов: корень диска и каталог `%WINDIR%` исключены, иначе одно нажатие снесло бы процессы системы (для `notepad.exe` каталогом установки является `System32`). После запроса метод ждёт выхода до `ForceStopTimeoutMilliseconds`.
- `RestartAsync` сохраняет приоритет вежливого пути (приложение успевает сохранить данные), но если закрытие не удалось — переходит к принудительному завершению и только затем запускает приложение заново. Без этого незакрытый первый экземпляр и новый запуск давали бы два процесса.
- `ProcessInspector` освобождает каждый `Process` через `using` и проверяет границу каталога (а не `StartsWith` без разделителя).
- `ProcessInspector.FindProcessesByPackage` определяет пакет по разбору пути процесса (`WindowsApps\<PackageFullName>`), а не по каталогу установки из реестра.
- Сопоставление процессов и окон с приложением вынесено в framework-free `ApplicationProcessMatcher` (Core), поэтому одна и та же логика используется и лаунчером (`WindowsApplicationLauncher.FindProcesses`), и UI (`MainViewModel`), и не дублируется между слоями. Кроме точного совпадения пути учитывается каталог установки: приложения-лаунчеры (например Squirrel у Discord, ярлык `Discord.exe --processStart Discord.exe`) запускают настоящий исполняемый файл из подкаталога каталога установки, и путь из ярлыка после запуска перестаёт существовать, поэтому по одному пути приложение ошибочно считалось незапущенным. Чтобы каталог не смешивал разные приложения (например `git-bash.exe` и `git-cmd.exe` в одном каталоге Git), в пределах каталога сопоставление идёт по имени образа из набора ожидаемых имён: имя файла запуска плюс имя из аргумента `--processStart` (`GetImageNames`). Расширение по каталогу остаётся ограниченным именем образа, а `ProcessInspector.FindProcessesByImageInDirectory` используется только для безопасных (несистемных) каталогов.
- `ComApartment` выполняет COM в STA и балансирует `CoInitializeEx`/`CoUninitialize`; результат исключения пробрасывается в вызывающий поток через `ExceptionDispatchInfo`.

## Решения Stage 4 — Window Manager

- `WindowInfo.Id` — HEX-представление HWND: Core остаётся framework-free, а native handle восстанавливается `TryParseId` только на уровне Windows Integration.
- Окна перечисляются через `EnumWindows` и фильтруются: только видимые (`WS_VISIBLE`), не cloak-нутые (DWM `DWMWA_CLOAKED`) и не tool-windows; окна своего процесса не исключаются, чтобы панель показывала окна Glaze Shell тоже.
- События окон получаются через `SetWinEventHook` (OUTOFCONTEXT) на выделенном потоке с `GetMessage`-pump: опция `skipOwnProcess` включает `WINEVENT_SKIPOWNPROCESS`; hook-диапазоны разбиты на два (системный 0x0003–0x0017 и объектный 0x8000–0x8017) для предсказуемой доставки.
- Любое событие окон публикуется в `IEventManager` как `WindowOpened`/`WindowClosed`/`ForegroundWindowChanged`/`WindowStateChanged`; `EventManager` диспетчеризует по runtime-типу, поэтому подписка на конкретный тип не зависит от статического типа публикатора.
- Фокус окна — best-effort: `ShowWindowAsync(SW_RESTORE)` + `SetWindowPos` + `SetForegroundWindow`, при отказе (foreground lock) применяется `AttachThreadInput` к foreground/target threads; `SendInput` не используется.
- Управление состоянием — только `ShowWindowAsync` (не блокирует вызывающий поток), закрытие — только вежливый `WM_CLOSE` через `PostMessage`. Принудительное завершение — не часть управления окном, а отдельное действие над приложением (см. «Решения Stage 3»), и из window-manager по умолчанию не вызывается.
- `WindowInfo.MonitorId` получается при перечислении окна через `MonitorFromWindow` и совпадает по формату с `MonitorInfo.Id` (HEX `HMONITOR`). Это позволяет связать окно с монитором без дополнительного перечисления на стороне UI: панель окон показывает монитор окна, а панель мониторов — окна, открытые на каждом мониторе.
- Инлайн-панель окон привязана к карточке приложения по пути исполняемого файла (ordinal-ignore-case); у MSIX `ExecutablePath` не заполнен, поэтому их окна сопоставляются по имени семейства пакета, извлечённому из пути процесса окна.
- UI получает события окон через `IEventManager` и выполняет единый коалесированный refresh на UI-потоке (`DispatcherQueue`), чтобы пачки событий не порождали лавину перестроений.

## Решения Stage 5 — Desktop Integration

- `MonitorInfo.Id` — HEX-представление `HMONITOR`, как `WindowInfo.Id` для `HWND`: Core остаётся framework-free, native handle восстанавливается через `TryParseId` только на уровне Windows Integration.
- Мониторы перечисляются через `EnumDisplayMonitors` (все мониторы виртуального стола) и читаются через `GetMonitorInfoW` с `MONITORINFOEXW` (`cbSize = sizeof(MONITORINFOEXW)`), чтобы получить `szDevice` для `EnumDisplaySettingsW`.
- `DEVMODEW` объявлен с union-частью через `[StructLayout(LayoutKind.Explicit)]` (`DevModeUnion`): печатная и display-ветви перекрываются в памяти по Win32-офсетам; `dmSize` выставляется до вызова `EnumDisplaySettingsW`.
- DPI берётся через `GetDpiForMonitor(MDT_EFFECTIVE_DPI)` (scale factor = dpi / 96); при отсутствии shcore используется fallback на `GetDpiForSystem`.
- Событие изменения дисплеев получается через hidden message window (`CreateWindowExW` + `GetMessage`-pump) на dedicated-потоке: `WM_DISPLAYCHANGE` рассылается всем top-level окнам, поэтому monitor-поток не требует hooks.
- `MonitorManager` публикует `DisplayChanged` со свежим снимком `MonitorInfo` на каждый `WM_DISPLAYCHANGE`; коалесация публикаций выполняется на стороне UI (`DispatcherQueue`), а не на источнике — событие несёт фактическую конфигурацию, а не diff.
- `DesktopManager` полностью framework-free и оперирует строками вкладок/категорий/элементов; начиная со Stage 6 он предоставляет полный CRUD вкладок, категорий и размещения приложений (см. «Решения Stage 6»).
- UI-панель «Мониторы» получает `DisplayChanged` через `IEventManager` и обновляется на UI-потоке, переиспользуя VM по `Id` (паттерн `ApplicationListViewModel`); обновление не приводит к миганию списка.
- Окно разделено на две колонки: список приложений с поиском и кнопкой «Обновить» — слева, панель «Мониторы» — справа (фиксированная ширина ~360px, собственный `ScrollViewer` на всю высоту колонки). Так окна и мониторы видны одновременно с основным списком и не прокручиваются вместе с ним; строка состояния остаётся внизу во всю ширину окна. Пути в карточках приложений показываются сокращённо (`корень\первый каталог\…\последний каталог\файл`), а полный путь копируется кнопкой «Копировать».
- Панель показывает окна, открытые на каждом мониторе, и — при двух и более экранах — схематичную карту мониторов. Карта строится `MonitorMapLayout` (Core, framework-free): он раскладывает `MonitorInfo.Bounds` в единицах фиксированного холста с сохранением взаимного расположения и единым масштабом, поэтому логика раскладки тестируется без реальных мониторов. Для единственного монитора карта вырождается в один прямоугольник и потому не показывается (`HasMonitorMap`). Связь окон с мониторами берётся из `WindowInfo.MonitorId`.

## Решения Stage 6 — Tabs and Categories

- `DesktopTab.IsActive` удалён: поле дублировало `DesktopLayout.ActiveTabId` и никогда не поддерживалось менеджером, из-за чего UI мог прочитать неверное состояние. Единственный источник истины — `DesktopLayout.ActiveTabId`; активная вкладка хранится один раз.
- `ApplicationCategory` получил `Order`, поэтому порядок существует на всех трёх уровнях layout: tabs → categories → items. `Order` нормализуется в плотную последовательность `0..N-1` и совпадает с позицией в коллекции.
- Нормализация выполняется на границе `DesktopManager`: `SetLayout` и конструктор с `initialLayout` приводят внешний layout к каноническому виду (сортировка по `Order` с сохранением исходного порядка при равенстве, затем перенумерация). Внутри менеджера порядок всегда канонический, поэтому семантика `MoveX(newOrder)` однозначна.
- `MoveX` возвращает `false`, если целевая позиция совпадает с текущей — мутация, не меняющая состояние, не публикует `DesktopChanged` и не создаёт лишних копий layout.
- Все мутации выполняются через единый `Mutate(Func<DesktopLayout, DesktopLayout?>)`: делегат под lock возвращает новый layout либо `null` при отказе, запись в поле происходит только после успешного вычисления, поэтому исключение валидации не оставляет частично обновлённого состояния. `DesktopChanged` публикуется вне lock.
- `CreateTab`/`CreateCategory` принимают `int? order`: `null` добавляет в конец (обычный сценарий), явное значение вставляет по позиции с клампингом. Значение по умолчанию `0` не использовано намеренно — оно вставляло бы каждый новый объект в начало.
- `AddApplication` без `categoryId` пишет в первую категорию вкладки, а если категорий нет — лениво создаёт категорию по умолчанию (`main`/«Основная»). Это сохраняет поведение Stage 5 и покрывает «просто добавить приложение» без настройки категорий.
- `MoveApplication` поддерживает перенос между категориями: элемент извлекается из исходной категории, вставляется в целевую по позиции, обе коллекции перенумеровываются. Внутри одной категории поведение совпадает с переупорядочиванием.
- Идентификаторы (`DesktopItem.Id`) уникальны в пределах вкладки, но не между вкладками — один и тот же `DesktopItem` может присутствовать в разных вкладках рабочего пространства.
- Модели остаются immutable и валидирующими. Поскольку свойства get-only, изменение выполняется через конструктор; для удобства и сохранения валидации добавлены `DesktopTab.With(...)`, `ApplicationCategory.With(...)` и `DesktopItem.WithOrder(int)` по образцу существующего `Application.WithMetadata`.
- XAML не изменялся: Stage 6 добавляет только backend-контракт. Подключение вкладок и категорий к визуальному дереву выполняет владелец проекта.

## Решения Stage 7 — Persistence

- Документы пользовательских данных — единственный источник истины между запусками: `config.json` (имя приложения и каталог данных), `settings.json`, `layout.json`. Хранение в JSON выбрано из-за читаемости, переносимости и отсутствия миграций схемы БД; альтернатива SQLite отложена до появления данных, которые JSON не выражает.
- `GlazeShell.Data` зависит только от Core и BCL: слой не знает о Win32, UI и файловой системе Windows, поэтому вся логика persistence тестируется детерминированно в `GlazeShell.Data.Tests`.
- Пути берутся только из кода (`UserDataFileNames`), а `UserDataDirectory.GetDocumentPath` проверяет, что путь остаётся внутри корневого каталога. Значения из документов никогда не превращаются в пути.
- Версия схемы хранится в каждом документе (`schemaVersion`) и мигрируется строго последовательно: `v1 → v2 → v3`. Пропуск версии невозможен, поэтому документ не может оказаться в неизвестном текущему коду состоянии. Миграция принадлежит конкретному типу документа (`UserDataDocumentKind`), поэтому миграция настроек не может примениться к layout.
- Документ из будущей версии приложения не изменяется вообще: он остаётся на диске для более новой версии, а текущая работает на значениях по умолчанию. Это осознанный размен: лучше потерять настройки, чем переписать документ, который новая версия поймёт иначе.
- Запись атомарна (временный файл, `Flush(flushToDisk)`, `File.Move(overwrite)`), а перед перезаписью при миграции создаётся резервная копия. Если копия не создана, документ не переписывается: потеря исходных данных хуже, чем отказ от миграции.
- Повреждённый документ изолируется перемещением в `recovery`, а не удаляется. Если изоляция не удалась, файл остаётся нетронутым, а значения по умолчанию применяются только в памяти.
- Загрузка не бросает исключений наружу: результат — `PersistenceLoadResult<T>` со статусом `Created`/`Loaded`/`Migrated`/`Recovered`/`Unsupported` и диагностикой, которую composition root пишет в лог.
- Привязка вкладок к мониторам хранится в layout как `DesktopTab.MonitorId` (`string?`, идентификатор монитора, а не native handle). При загрузке `MonitorLayoutBinding.Reconcile` снимает привязки к недоступным мониторам, а `App` сохраняет результат, чтобы не пересчитывать его при каждом старте.
- `MonitorLayoutBinding` находится в Core и не зависит от Windows: он принимает `IEnumerable<MonitorInfo>`, поэтому правила привязки тестируются без мониторов. Пустой или недоступный список мониторов не считается основанием снимать привязки — при отсутствии достоверных сведений настройка пользователя сохраняется.
- `DesktopLayoutPersistenceWriter` подписывается на `DesktopChanged` и объединяет частые изменения в одну запись (400 мс), а `Flush`/`Dispose` вызываются при закрытии окна. Альтернатива (запись на каждое событие) упиралась бы в диск при массовых изменениях layout.
- `PersistingSettingsManager` повторяет семантику `InMemorySettingsManager`, включая публикацию `SettingsChanged`, и добавляет сохранение. Ошибка записи не отменяет изменение настроек: недоступный диск не должен делать настройки только для чтения.
- `App` читает конфигурацию один раз из каталога по умолчанию, определяет корневой каталог и уже из него загружает layout и настройки. Повторное чтение конфигурации из нового каталога создало бы там документ по умолчанию и сбросило бы настройки пользователя.
- XAML не изменялся: Stage 7 добавляет backend и composition wiring, подключение визуального дерева остаётся за владельцем проекта.

## Решения Stage 8 — Theme System

- Тема — это данные оформления, а не программа: `Theme` содержит только `Metadata`, `Colors`, `Fonts`, `Dimensions`, `Icons`, `Wallpaper` и `Effects`. Исполняемый код из темы не выполняется и не может быть описан: в модели нет полей для команд, скриптов и подключаемых сборок.
- Источник ассета проверяется по двум независимым признакам (`ThemeAssetValidation`): расширение не должно быть исполняемым или загружаемым, а путь должен быть относительным и не выходить за каталог темы. Запрет по расширению не зависит от того, что система считает выполняемым, а ограничение пути не позволяет теме указать файл за её пределами.
- Цвет задаётся только как `#RGB`, `#RRGGBB` или `#AARRGGBB`. Имена вроде `red` и выражения вроде `rgb(...)` отклоняются: UI передаёт значение движку рендеринга, а разбор выражения означал бы выполнение кода, заданного темой.
- Пользовательские темы лежат в `themes/*.json` каталога данных приложения и наполняются пользователем, поэтому программа их только читает. Загрузчик не обходит подкаталоги: тема не может подключить другие файлы.
- Непригодная тема отбрасывается по отдельности и попадает в `ThemeLoadResult.Diagnostics`, а весь набор тем не изолируется в `recovery`: в отличие от документов программы, тема является данными пользователя, и перемещение её файла означало бы потерю работы пользователя.
- `ThemeManager` хранит темы неизменяемыми: подписчик `ThemeChanged` получает согласованный снимок, а выбор той же темы событие не публикует — тем же правилом, что и мутации desktop layout. Порядок тем задаётся по имени и идентификатору, чтобы не зависеть от порядка файлов в каталоге.
- Действующая тема всегда существует: пока тема не выбрана, действует встроенная `Theme.CreateDefault()` (`glaze-default`). Неизвестный идентификатор из настроек не оставляет приложение без оформления — сохраняется предыдущая тема, а факт подмены попадает в лог.
- Идентификаторы тем сравниваются без учёта регистра: идентификатор попадает в `settings.json`, который пользователь читает и правит вручную, и `Midnight`/`midnight` не должны оказаться двумя разными темами под одним именем.
- XAML не изменялся: Stage 8 добавляет только backend. Как именно применить цвета, шрифты и размеры, решает UI, поэтому `IThemeManager` отдаёт значения, а не ресурсы XAML.

## Решения Stage 9 — Windows Events

- Источники системных событий запускаются из одного места (`ShellEventCoordinator`), а не каждый из `App.xaml.cs`. Причина: у каждого источника есть хук или message window, которые нужно установить ровно один раз и гарантированно снять при закрытии окна. Разбросанный запуск допускал двойной старт и оставшиеся хуки.
- Отказ одного источника не отменяет запуск остальных. `Start()` возвращает `ShellEventStatus` с флагом на источник и списком `Diagnostics`; подписчики на события неработающего источника просто не получают событий. Иначе неисправность `MonitorManager` выключала бы оболочку целиком, хотя остальные источники работают.
- `IWindowsEventSource` живёт в `GlazeShell.Windows`, а не в Core. Контракт описывает запуск и остановку WinEvent hooks и message windows, о которых Core не знает и которыми не владеет; выносить его в Core означало бы втащить понятие жизненного цикла источника в слой, где источников нет.
- События процессов получаются сравнением снимков списка процессов, а не подпиской на системное событие: события «процесс запущен» в Windows нет. `SetWinEventHook` работает с окнами, ETW требует трассировки с правами администратора, WMI требует службы, которая на пользовательской машине часто отключена. Разница снимков — единственный вариант без новых зависимостей и без привилегий.
- Первый снимок `ProcessEventMonitor` — базовый и событий не порождает. Иначе все процессы, работавшие до запуска Glaze Shell, выглядели бы как только что запущенные, а интерфейс показывал бы десятки «запущенных» приложений, которые пользователь не открывал.
- Если на `ProcessStarted`/`ProcessExited` никто не подписан, снимок не выполняется вовсе (`IEventManager.HasSubscribers<TEvent>`). Фоновая работа не должна продолжаться, когда её результат никто не слушает; проверка на каждом такте дешевле самого перечисления.
- Снимок стоит одного перечисления процессов: имя и путь читаются только для новых PID, для известных используется кэш предыдущего снимка. Открытие `MainModule` на каждом процессе при каждом такте было бы заметной нагрузкой, не зависящей от числа запущенных программ.
- `ProcessExited` публикуется раньше `ProcessStarted` в одном проходе: перезапуск приложения должен читаться как «вышел, затем открыто». Обратный порядок заставлял бы интерфейс на секунду показывать приложение выключенным после перезапуска.
- Состояние запущенных приложений обновляется по событиям, а не опросом каждые несколько секунд. Периодический опрос перечислял процессы для всех приложений списка независимо от того, изменилось ли что-нибудь; событийный подход пересчитывает только приложения, которых касается событие. Однократный полный пересчёт сохранён как базовая линия — он необходим, потому что процессы, работавшие до запуска оболочки, не порождают событий.
- Сопоставление события процесса с приложением идёт по полному пути к исполняемому файлу, а при его отсутствии — по имени образа; у MSIX-приложений без `ExecutablePath` — по имени семейства пакета, извлечённому из пути процесса (`WindowsApps\<PackageFullName>`). Сравнение только по имени образа дало бы ложные совпадения (`update.exe`, `setup.exe` у разных приложений), поэтому для Win32 полный путь остаётся основным признаком. Логика сопоставления общая для лаунчера и UI (`ApplicationProcessMatcher`), поэтому приложения-лаунчеры (например Discord через `--processStart`) распознаются одинаково и в событиях процессов, и при перечислении окон.
- `EventCoalescer` объединяет пачку сигналов в одно действие. Система присылает изменение дисплеев несколькими сообщениями подряд, и перечисление мониторов на каждое из них было бы лишней работой, заметной при подключении монитора. Обработчик ошибок обязателен: неперехваченное исключение в потоке таймера завершило бы процесс.
- `Cancel()` отменяет отложенную публикацию при остановке источника. Иначе источник, уже отключённый, успевал бы опубликовать `DisplayChanged` после закрытия, и подписчики получали бы данные от источника, которого больше нет.
- Отбор оконных сообщений, означающих изменение дисплеев, намеренно узкий: `WM_DEVICECHANGE` обрабатывается только для `DBT_DEVNODES_CHANGED`, `WM_SETTINGCHANGE` — только для `SPI_SETWORKAREA` и `SPI_SETLOGICALDPIOVERRIDE`. Остальные сообщения этой группы приходят постоянно (смена клавиатуры, раскладки, темы) и не меняют ни состав, ни геометрию мониторов.
- Определение состояния MSIX-приложения по AUMID удалено: оно сравнивало AUMID целевого приложения с AUMID собственного процесса оболочки, то есть проверяло не то приложение. Корректная альтернатива потребовала бы COM-вызова для каждого процесса (интерфейс создаётся на отдельном STA-потоке), что несопоставимо дороже проверки по каталогу установки. Пакет определяется разбором пути процесса (`PackageIdentity`), потому что каталог установки из реестра для bundle-пакетов указывает на `neutral`-вариант вместо установленного. Побочный эффект: пакеты с фоновым сервисом без окна показываются запущенными, пока сервис жив.

## Решения Stage 10 — Application Lifecycle

- Одиночность экземпляра обеспечивается именованным mutex (`SingleInstanceHelper.Acquire`), а не поиском процесса по имени exe: несколько процессов одного имени могут существовать по разным причинам, а mutex — единственный объект ядра, который живёт ровно столько, сколько живёт экземпляр, и не переживает аварийное завершение как «занятый».
- `SingleInstanceLease` удерживает mutex всё время работы и освобождается только на потоке-владельце: мутекс имеет thread affinity, поэтому освобождение с другого потока невозможно. `Dispose` идемпотентен, а при повторном запуске `Acquire` возвращает `null`, и вызывающий код завершает процесс — новый экземпляр не создаёт второй hook-поток и второе окно.
- `AbandonedMutexException` трактуется как «экземпляр свободен»: это означает, что предыдущий процесс завершился аварийно, не освободив mutex. Считать такой mutex занятым означало бы навсегда запретить запуск после падения.
- Активация существующего экземпляра (`ActivateExistingInstance`) — best-effort: окно восстанавливается через `ShowWindowAsync(SW_RESTORE)` и выводится на передний план через `SetForegroundWindow`. При foreground lock система может отклонить перевод на передний план; это не ошибка и не повод завершать новый процесс с ошибкой.
- Окно собственного экземпляра ищется по владеющему процессу (`GetWindowThreadProcessId` + сравнение пути процесса с `Environment.ProcessPath`), а не по пути модуля окна. `GetWindowModuleFileNameW` для чужого окна возвращает модуль вызывающего процесса, поэтому по нему нельзя понять, кто владеет окном, — сопоставление по процессу не имеет этого дефекта и не требует знать класс окна.
- Название класса окна больше не задаётся: каркас использовал `SetClassLongPtr` для установки имени класса и поиска окна по этому имени, но фактически ничего не менял, а поиск никогда не совпадал. Единственная точка истины — владеющий процесс окна.
- Освобождение ресурсов при выходе упорядочено: при закрытии окна сначала сбрасывается и освобождается `DesktopLayoutPersistenceWriter`, затем освобождается `ShellEventCoordinator` (источники событий и хуки), и последним — lease. Mutex освобождается только после остановки хуков и записи layout, поэтому новый экземпляр не поднимается раньше, чем предыдущий отпустил системные ресурсы.
- Stage 10 не реализует автозапуск при входе в систему: lifecycle здесь ограничен startup/shutdown и single-instance; автозапуск остаётся отдельной задачей.

## Решения Stage 12 — Security

- Проверка размера и само чтение выполняются через один открытый дескриптор (`BoundedTextFile`): связка `FileInfo.Length` + `File.ReadAllText` открывала файл дважды, и подменённый или выросший между проверкой и чтением файл обходил лимит. Теперь размер проверяется на том же дескрипторе, а число читаемых байт ограничено длиной, снятой при открытии, поэтому файл, выросший после проверки, не читается целиком. Дескриптор открывается с `FileShare.ReadWrite | FileShare.Delete`, чтобы чтение не мешало атомарной записи (переименованию) документа; `StreamReader` с `detectEncodingFromByteOrderMarks` сохраняет прежнее распознавание UTF-8/UTF-16 по BOM.
- Имя документа — это имя файла, а не путь. Помимо разделителей и `..`, отклоняются зарезервированные имена устройств Windows (`CON`, `PRN`, `AUX`, `NUL`, `COM1`…`COM9`, `LPT1`…`LPT9` — в том числе с расширением) и завершающие точка и пробел. Win32 отбрасывает завершающие точку и пробел, поэтому `settings.json.` открывает тот же файл, что и `settings.json`: без проверки имя документа можно было бы перепутать.
- `LogRedactor` не полагается на один вид пути или секрета. Путь профиля маскируется и при обратном, и при прямом слэше, и в JSON-экране (`C:\\Users\\…`); значение секрета после ключа маскируется целиком, включая многословные и bracket-значения; список ключей учитывает `private_key`, `secret_key`, `session_token`, `cookie`, `set-cookie` и подобные; токен `Bearer` маскируется и с нестандартными символами. Обрезка длинного текста не разрывает суррогатную пару, поэтому в лог не попадает одинокий high surrogate.
- Принудительное завершение по каталогу — самая опасная операция приложения: она завершает процессы, а не закрывает окна. Поэтому каталог установки считается безопасным только вне системной и общей области: корень диска, `C:\Windows` и любые его подкаталоги, а также общие корни `C:\Program Files`, `C:\Program Files (x86)` и `C:\ProgramData` не считаются безопасными. Раньше корень `C:\Windows` не отсекался (проверка «под каталогом» не включала равенство), а `FindProcessesByDirectory` ищет рекурсивно, поэтому приложение из `C:\Windows` или из корня `Program Files` могло снести процессы всей системы. Подкаталог конкретной программы остаётся безопасным, поэтому расширение по каталогу для фоновых процессов вроде `AcroCEF` сохранено.
- Ассеты темы не могут быть ярлыками, установщиками и элементами панели управления: список запрещённых расширений дополнен `.url`, `.cpl`, `.pif`, `.scf`, `.chm`, `.cab`, `.msp`, `.msix`, `.appx`, `.psd1`, `.appref-ms` и `.application`.
- Хуки окон снимает тот же поток, который их установил. `UnhookWinEvent` из потока остановки не снимает хук, а прежний `Stop()` очищал список хуков до `finally` воркера, из-за чего штатное снятие никогда не выполнялось и хуки оставались до завершения потока. Теперь поток остановки только публикует `WM_QUIT` и дожидается воркера, а `UnhookAll` выполняется в `finally` воркера; список хуков защищён блокировкой, потому что поток запуска читает его для проверки готовности.

## Решения Stage 13 — Testing и Release

- Покрытие расширяется по слоям, где есть логика и ветвления, а не по всем типам подряд. Добавлены тесты `ApplicationIdentity` (ключи запуска и идентификаторы для AUMID, пути и package family name, нормализация пути и детерминированность хэша), `ApplicationDiscoveryOptions` и `ProcessWatchOptions` (границы значений), мапперы документов `LayoutDocumentMapper`/`SettingsDocumentMapper`/`ConfigurationDocumentMapper` (уникальность идентификаторов, лимиты, ссылки на несуществующие цели, нормализация), `PersistenceLimits`, `MigrationResult` и события `GlazeEvents`. Тривиальные data-holder'ы без поведения отдельными тестами не покрываются: проверять у них нечего, кроме автогенерированного кода.
- `ApplicationDiscoveryService` тестируется на фейковых `IApplicationDiscoverySource`, а не на реальных источниках: это делает тест детерминированным и framework-free (без Start Menu и AppsFolder) и позволяет воспроизвести кэш, слияние кандидатов по ключу запуска, отсев кандидатов без цели, отключение системных приложений, изоляцию отказа источника в предупреждение, лимит предупреждений и однократную публикацию `ApplicationChanged` при неизменном снимке. Реальный запуск источников остаётся под environment-tolerant тестами слоя Windows.
- Тесты `ProcessInspector` опираются на текущий процесс: положительные проверки — по каталогу и по имени образа тест-хоста, отрицательные — по заведомо чужому пути и package family name. Так проверяется разделяемое поведение (`IsUnderDirectory`, `HasImageName`, безопасное чтение `MainModule`) без зависимости от конкретных установленных программ.
- Версия приложения задаётся один раз в `Directory.Build.props` (`Version`, `AssemblyVersion`, `FileVersion`, `Product`, `Company`, `Copyright`) и применяется ко всем проектам решения: одна точка правки исключает расхождение версий между App, Core, Data, Infrastructure и Windows. `Deterministic` и `ContinuousIntegrationBuild` из тех же props делают сборку воспроизводимой.
- Процесс релиза документирован в [docs/RELEASE.md](RELEASE.md), но автоматический CI не вводится: требования (Windows, Windows App SDK, x64) не дублируются в конфигурации, которую никто не поддерживает. Публикация бинарников не выполняется: приложение unpackaged, а Installer и MSIX отсутствуют, поэтому собранный `GlazeShell.exe` не считается готовым релизом для конечных пользователей.

## Продуктовый UI Phase 1 — H1 (движок тем)

MVP закрывал инженерный фундамент (Stage 0–13); Phase 1 открывает продуктовый UI, и первая функция — H1 «движок тем». Функция разделена на модель выбора (Core/Data) и применение оформления (App).

- Режим оформления и акцент — отдельные понятия. `ThemeMode` (Системная/Светлая/Тёмная) задаёт, какую палитру использовать, а `AccentId` — акцентный цвет поверх неё. Режим «Системная» не хранит вычисленную светлую/тёмную тему: она разрешается при применении (`ThemeModeResolver.Resolve`) через системную настройку `AppsUseLightTheme`, поэтому смена системной темы не требует перезаписи настроек пользователя.
- Акценты ограничены словарём `AccentPalette` (6 значений из дизайн-системы плюс «по умолчанию»). Идентификатор акцента — идентификатор из пользовательских данных, он валидируется так же строго, как `activeThemeId`: неизвестный акцент отклоняет весь документ `settings.json` (`SettingsDocumentMapper`), а не подменяется молча.
- `settings.json` расширен полями `themeMode` и `accentId` без повышения `schemaVersion`. Отсутствие полей даёт значения по умолчанию (режим «Системная», акцент темы), поэтому старые документы читаются без миграции — тем же приёмом, что и добавление `monitorNames`.
- Цвета UI заданы токенами-кистями в `App.xaml` (`GlazeBackgroundBrush`, `GlazeSurfaceBrush`, `GlazeElevatedBrush`, `GlazeBorderBrush`, `GlazeTextPrimaryBrush`, `GlazeTextSecondaryBrush`, `GlazeTextMutedBrush`, `GlazeAccentBrush`, `GlazeSelectionBrush`, `GlazeStatusSuccessBrush`, `GlazeStatusCriticalBrush`), а `MainWindow.xaml` ссылается на них через `StaticResource` вместо жёстких hex-значений. Один источник значений — `Theming/GlazePalette` (светлая и тёмная палитры из `docs/DESIGN.md`).
- `ThemeApplicator` меняет `RequestedTheme` корневого элемента (светлая/тёмная для стандартных контролов WinUI) и перекрашивает те же кисти на месте. Поскольку `StaticResource` ссылается на один экземпляр кисти, уже открытые элементы обновляются без перезагрузки XAML; кисти `ListViewItemBackground*` заданы в корне словаря, поэтому подсветка выбранной карточки перекрывает значения темы.
- Выбор режима и акцента хранит `MainViewModel` (списки `ThemeModeOptions`/`AccentOptions`, свойства `SelectedThemeMode`/`SelectedAccent`). При изменении настройка сохраняется через `ISettingsManager.SetSettings`, а окно получает событие `ThemeSelectionChanged` и вызывает `ThemeApplicator.Apply`. Так UI-модель не знает о кистях XAML, а применение оформления остаётся в одном месте.
- Движок меняет только интерфейс Glaze Shell. Системная тема Windows читается (`AppsUseLightTheme`), но не изменяется: синхронизация оформления ОС и запись `WM_SETTINGCHANGE` в H1 не входят.
- Custom-темы Stage 8 (`ThemeManager`, `themes/*.json`) остаются backend-данными оформления и в H1 к UI не подключены: H1 применяет встроенную светлую/тёмную палитру и акцент, а пользовательские темы — следующий шаг.

## Продуктовый UI Phase 1 — A6 (top bar)

Вторая функция Phase 1 — A6 «Top bar»: плавающая верхняя панель над рабочим столом. Реализован базовый слайс; полную спецификацию (резервирование рабочей области, панель на каждый монитор, переполнение иконок трея, popover часов и календаря D4) см. в [FEATURES.md](FEATURES.md).

- Панель — отдельное win-UI-окно `TopBarWindow` с `DesktopAcrylicBackdrop`, создаваемое композиционным корнем (`App`) до активации главного окна, чтобы показ панели не перехватывал фокус. Окно переводится в topmost инструментальное (`WS_EX_TOPMOST`/`WS_EX_TOOLWINDOW`) и non-activating (`WS_EX_NOACTIVATE`), показывается через `ShowWindow(SW_SHOWNOACTIVATE)`, а не `Activate()`. Это исключает кражу фокуса при появлении панели и кликах по ней.
- Позиционирование выполняется в физических пикселях относительно `DisplayArea` основного монитора с масштабированием по DPI (`OverlayWindow.GetScaleFactor`), поэтому панель не «плывёт» на мониторах с масштабом, отличным от 100%. Скругление окна задаётся через `DwmSetWindowAttribute` (corner preference).
- Native-детали панели инкапсулированы в `GlazeShell.Windows`, чтобы Core не знал об `HWND`: `WindowManagement/OverlayWindow` (topmost/non-activating/скругление/DPI), `SystemStatus/SystemStatusProvider` (батарея через `GetSystemPowerStatus` и сеть через `NetworkInterface`) и `Applications/ApplicationNameResolver` (понятное имя приложения через `FileDescription`).
- Состояние панели — `Presentation/TopBarViewModel`: имя и расположение foreground-приложения (событие `ForegroundWindowChanged`, процесс запрашивается через `IWindowManager.GetWindow`), часы с датой, батарея и сеть. Обновление раз в секунду через `DispatcherQueueTimer`; собственный процесс (`Environment.ProcessId`) исключается.
- Включение и выключение панели связано с настройкой `topBarEnabled` (`settings.json`): `MainViewModel` сохраняет её, `PersistingSettingsManager` публикует `SettingsChanged`, а `App` создаёт или закрывает `TopBarWindow`. Панель следует за сменой оформления (H1) и системной темой; фокус строки поиска включается только по клику.
- Ограничения базового слайса: одна панель на основном мониторе (не на каждый монитор), рабочая область не резервируется (окна могут перекрывать панель), иконки трея не размещаются, popover часов и календаря (D4) не реализован, `Ctrl K` — визуальная подсказка без обработчика, уровень громкости пока не читается.

## Продуктовый UI Phase 1 — A1 (dock)

Третья функция Phase 1 — A1 «Dock»: плавающая нижняя панель для запуска и переключения приложений. Реализован базовый слайс; полную спецификацию (перетаскивание, бейджи, стопки папок, dock на каждый монитор, резервирование рабочей области) см. в [FEATURES.md](FEATURES.md).

- `DockWindow` — отдельное WinUI-окно с `DesktopAcrylicBackdrop`, topmost инструментальным (`WS_EX_TOPMOST`/`WS_EX_TOOLWINDOW`) и non-activating (`WS_EX_NOACTIVATE`) стилями. Показывается через `ShowWindow(SW_SHOWNOACTIVATE)`, поэтому клик по плитке не забирает фокус у активного приложения; контекстное меню временно разрешает активацию и снова запрещает её после закрытия. Окно центрируется по горизонтали основного монитора на 18 px выше нижнего края, ширина считается по числу плиток и разделителей, размеры масштабируются по DPI. Рабочая область не резервируется.
- `DockViewModel` — список плиток: «Пуск», закреплённые приложения и запущенные приложения. Закрепления хранятся в `settings.json` (`dockPinnedApps`) и ограничены `UserSettings.MaxDockPinnedApps`; запущенные приложения вычисляются по окнам через `ApplicationProcessMatcher` и обновляются по `WindowOpened`/`WindowClosed`/`WindowStateChanged` через `EventCoalescer` (250 мс), поэтому пачка окон одного запуска даёт одну пересборку. Приложения вне discovery (портативные, распакованные) группируются по пути исполняемого файла и тоже получают плитку.
- Клик по незапущенному приложению запускает его, по запущенному — фокусирует окно, а повторный клик по активному окну сворачивает его (поведение панели задач). Значки извлекаются `ShellIconProvider` в фоне и кэшируются по пути; пока значка нет, плитка показывает букву на устойчивом пастельном градиенте, поэтому панель не «мигает» пустотой.
- Подъём и масштаб плитки при наведении выполняет Composition-анимация (140 мс, подъём 10 px, масштаб 1.18; соседние плитки — 4 px и 1.06). Значения меньше дизайнерских (16 px, 1.2), потому что плитка увеличивается внутри окна и всё, что вышло за его границы, было бы обрезано. Вместо размытого ореола используется акцентная рамка: отдельный визуальный слой с blur не создаётся.
- Замена панели задач вынесена в `GlazeShell.Windows` (`Shell/TaskbarHider`): окно `Shell_TrayWnd` не уничтожается и не отключается в реестре, а сдвигается `SetWindowPos` за нижнюю границу своего монитора (на 16 px). Исходное положение запоминается и возвращается при выходе, выключении dock или настройки; в `Shutdown` панель восстанавливается первым шагом, чтобы сбой остальных очисток не оставил пользователя без панели задач. После перезапуска Explorer или смены разрешения скрытие повторяется по `TaskbarCreated`/`WM_DISPLAYCHANGE`, а `RestoreIfOffScreen` при следующем запуске возвращает панель, если предыдущий процесс завершился аварийно.
- Скрытие включается только при `dockEnabled && dockReplacesTaskbar`, поэтому выключение dock не оставляет систему без панели задач. Обе настройки добавлены в `settings.json` без повышения `schemaVersion`: отсутствующие поля дают dock и замену панели задач включёнными по умолчанию.

## Windows API

### Текущий статус

Glaze Shell использует только официальные Win32 и COM API. P/Invoke и COM-интерфейсы изолированы в `GlazeShell.Windows` (каталоги `Interop`, `Shell`, `Win32`, `Applications`, `WindowManagement`, `DisplayManagement`, `SystemEvents`, `Lifecycle`); `GlazeShell.Core` и `GlazeShell.Data` Windows API не используют: Core не содержит native типов, а Data работает через BCL (`System.Text.Json`, `System.IO`) и зависит только от Core, поэтому проверки безопасности Windows к этим слоям не применяются.

### Целевая платформа и версии API

- Target framework: `net10.0-windows10.0.19041.0`.
- Минимальная версия, заявленная для App и Windows Integration: Windows 10 1809 (`10.0.17763.0`).
- Перед использованием API необходимо проверить его availability для этой версии и для Windows 11.

### Используемые API

| Область | API | Каталог |
| --- | --- | --- |
| Shortcuts | бинарный формат MS-SHLLINK через managed `ShellLinkData` | `Shell/ShellLinkData.cs` |
| Shortcuts | `IShellLinkW` (Load/Get*), `IPersistFile` (Load) | `Shell/IShellLinkW.cs`, `Shell/ShellIdentifiers.cs` |
| Shortcuts | `IShellItem`, `IShellItem2` property store (`PKEY_Link_TargetParsingPath`, `PKEY_Link_Arguments`, `PKEY_Link_Name`) | `Shell/IShellItem.cs` |
| AppsFolder | `SHCreateItemFromParsingName`, `IShellFolder`, `IShellItemArray`, `IID_IShellItemArray` `{56FDF344-FD6D-11D0-958A-006097C9A090}` | `Shell/AppsFolderSource.cs` |
| MSIX | `IApplicationActivationManager` (`ActivateApplication`) | `Interop/IApplicationActivationManager.cs` |
| MSIX | разбор пути процесса (`WindowsApps\<PackageFullName>`) через `PackageIdentity` | `GlazeShell.Core/Discovery/PackageIdentity.cs`, `Applications/ProcessInspector.cs` |
| Processes | `Process.GetProcesses`, `CloseMainWindow`, `Process.Kill(entireProcessTree: true)` | `Applications/WindowsApplicationLauncher.cs`, `Applications/ProcessInspector.cs`, `SystemEvents/ProcessEventMonitor.cs`, `GlazeShell.Core/Discovery/ApplicationProcessMatcher.cs` |
| COM | `CoInitializeEx`, `CoUninitialize`, `CoTaskMemFree`, `CoCreateInstance` | `Win32/Ole32.cs`, `Shell/ShellIdentifiers.cs` |
| Windows | `EnumWindows`, `GetForegroundWindow`, `IsWindow`, `GetWindowText`, `GetClassName`, `GetWindowThreadProcessId`, `ShowWindowAsync`, `PostMessage`, `SetForegroundWindow`, `AttachThreadInput`, `GetWindowPlacement`, `SetWindowPos` | `Win32/User32.cs` |
| Windows events | `SetWinEventHook`/`UnhookWinEvent`, `GetMessage`/`TranslateMessage`/`DispatchMessage` (0x0003–0x0017, 0x8000–0x8017) | `Win32/User32.cs`, `WindowManagement/WindowEventMonitor.cs` |
| Lifecycle | именованный mutex (BCL `System.Threading.Mutex`), `EnumWindows`, `IsWindowVisible`, `GetWindowThreadProcessId`, `IsIconic`, `ShowWindowAsync`, `SetForegroundWindow` | `Lifecycle/SingleInstanceHelper.cs`, `Win32/User32.cs` |
| Window cloak | `DwmGetWindowAttribute` (`DWMWA_CLOAKED`) | `Win32/Dwmapi.cs`, `WindowManagement/NativeWindowEnumerator.cs` |
| Monitors | `EnumDisplayMonitors`, `EnumDisplaySettingsW`, `MonitorFromWindow`, `MonitorFromPoint` | `Win32/User32.cs`, `DisplayManagement/NativeMonitorEnumerator.cs` |
| DPI | `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI`), `GetDpiForSystem`, `GetDpiForWindow` | `Win32/Shcore.cs`, `Win32/User32.cs` |
| Display events | `WM_DISPLAYCHANGE` (`0x007E`), `WM_DEVICECHANGE` (`0x0219`), `WM_SETTINGCHANGE` (`0x001A`), `WM_DPICHANGED` (`0x02E0`) в hidden message window, `DisplayChanged` | `DisplayManagement/MonitorEventMonitor.cs`, `DisplayManagement/DisplaySignal.cs`, `Win32/User32.cs` |
| Taskbar | `FindWindow` (`Shell_TrayWnd`), `RegisterWindowMessage` (`TaskbarCreated`), `SetWindowPos` (`SWP_NOSIZE`/`SWP_NOZORDER`/`SWP_NOACTIVATE`), `MonitorFromWindow`, `GetMonitorInfoW` | `Shell/TaskbarHider.cs`, `Win32/User32.cs` |
| Known folders | `SHGetKnownFolderPath`, `SHGetKnownFolderItem` | `Win32/Shell32.cs` |
| Threads | `GetCurrentThreadId` | `Win32/Kernel32.cs` |

### Правила использования

- P/Invoke размещается только в `GlazeShell.Windows` и изолируется в `Win32` или `Interop`.
- Core не импортирует Windows API и не использует native handle types.
- Каждый API задокументирован: permissions, lifetime ресурсов, thread affinity и альтернатива.
- COM-объекты создаются в STA и освобождаются; `CoInitializeEx`/`CoUninitialize` балансируются на dedicated threads.
- P/Invoke user32 объявляется с `CharSet.Unicode` и без `ExactSpelling`, когда нативный экспорт существует только с суффиксом `W`/`A` (`GetMessage`, `DefWindowProc`, `GetModuleHandle`, `PostMessage`).
- `SetWinEventHook` работает в режиме `WINEVENT_OUTOFCONTEXT`: callback вызывается на потоке, установившем hook, только когда этот поток вызывает `GetMessage`. Hook-поток обязан иметь цикл сообщений.
- Managed fast path (`ShellLinkData`) используется первым: он не требует COM и работает при нерабочем `CLSID_ShellLink`.
- Не используются undocumented API без отдельного обоснования и security review.

### Известные ограничения среды

- В текущем окружении `CLSID_ShellLink` зарегистрирован в `C:\Windows\System32\windows.storage.dll`, `IShellLinkW.Load` реальных `.lnk` возвращает `0x00000001`, `Save` — `0x80070002`.
- `IPropertyStore`/`IShellItem2` на реальных `.lnk` возвращают `0x80004002`; AppsFolder через `MatchOption.None` — `0x800401E5`.
- `SHGetPathFromIDListEx` с извлечённым из `.lnk` PIDL вызывал `0xC0000005`; native PIDL-резолв не используется, вместо него работает managed LinkInfo.
- Эти ограничения специфичны для данной машины; на штатных системах соответствующие fallback возвращают результат.
- Delivery событий `SetWinEventHook` зависит от сессии: в службовых/неинтерактивных сессиях и в некоторых host-окружениях события могут не доставляться, поэтому событийные тесты являются environment-tolerant (`Inconclusive`).

## Производительность

### Исходные условия

Приложение не создаёт фоновые service процессы. Startup создаёт WinUI window, startup log record, запускает однократное сканирование приложений в фоне и (со Stage 4) поднимает один hook-поток оконных событий.

Stage 1 добавил только framework-free Core: модели, контракты и in-memory сервисы, которые не создают потоков, таймеров или внешних ресурсов.

`EventManager` не хранит события и не запускает фоновую обработку: доставка выполняется синхронно в потоке publisher, а `Publish` работает с копией списка подписок, поэтому подписка и отписка не требуют блокировки на стороне вызывающего.

### Stage 2 — Discovery

- `StartMenuShortcutSource` использует параллельное сканирование с ограничением `min(max(1, Environment.ProcessorCount), 8)` потоков; результат детерминированно сортируется.
- Для каждого `.lnk` сначала выполняется managed fast path (`ShellLinkData`) без COM; fallback на `IShellLinkW`/property store выполняется только для нерезолвнутых файлов.
- AppsFolder сканируется через один STA-вызов `IShellItemArray`; COM-объекты освобождаются в `finally`.
- `ApplicationDiscoveryService` кэширует снапшот на `CacheDuration` (5 минут) и использует single-flight lock, поэтому повторный `DiscoverAsync` в течение окна не сканирует диск.
- Результат сканирования (десятки shortlinks) собирается за время меньше секунды на штатных системах.

### Stage 3 — Launcher

- `ProcessInspector.FindProcessesByPackage` разбирает пути процессов без обращения к реестру: имя семейства пакета вычисляется из имени каталога `WindowsApps\<PackageFullName>`.
- `ProcessInspector` итерирует процессы один раз, освобождая каждый `Process` через `using`, и не копирует `Process[]` бесконечно.
- Состояние запущенных приложений обновляется по событиям процессов (`ProcessStarted`/`ProcessExited`), а не периодическим опросом (см. «Решения Stage 9»); однократный полный пересчёт выполняется только после первичного сканирования и задаёт базовую линию для приложений, запущенных до Glaze Shell.
- `ApplicationProcessMatcher` не перечисляет процессы сам: он работает над уже полученными путём и именем образа (из снимка процессов или события), поэтому сопоставление не добавляет проходов по системе.

### Stage 4 — Window Manager

- Оконные события получаются через `SetWinEventHook` вместо polling: callback вызывается только при реальном событии, фоновый поток с `GetMessage`-pump простаивает без нагрузки.
- Hook-поток один и создаётся только при `WindowManager.Start()`; на нём живут оба hook-диапазона, отдельный поток на окно не создаётся.
- UI не перестраивает список на каждое событие: `MainViewModel` коалесирует события (один pending refresh) и выполняет перечисление окон на UI-потоке.
- `EnumWindows` перечисляет только видимые top-level окна и возвращает ограниченное количество элементов; чтение метаданных (`DWMWA_CLOAKED`, состояние, заголовок) выполняется только после фильтрации.
- Оконные хуки и dedicated-поток освобождаются в `Dispose` (`UnhookWinEvent`), чтобы не копить hook-идентификаторы между запусками.

### Stage 7 — Persistence

- Документы малы (десятки килобайт) и читаются один раз при старте: `LoadConfiguration`, `LoadLayout` и `LoadSettings` не выполняются в цикле и не блокируют регулярную работу приложения.
- Запись layout выполняется по событию `DesktopChanged`, но с задержкой 400 мс: серия изменений (перетаскивание, массовое перемещение) приводит к одной записи вместо одной на событие. Таймер одноразовый и не создаёт постоянного фонового цикла.
- Запись атомарна и завершается синхронно в вызывающем потоке, но объём данных ограничен лимитом 4 MiB, поэтому запись не может стать источником длительной блокировки UI.
- Проверка размера файла выполняется через `FileInfo.Length` до чтения: слишком большой документ не попадает в память целиком.
- Разбор JSON ограничен глубиной 32 и числом элементов (`PersistenceLimits`), поэтому повреждённый документ не может исчерпать память или время разбора.
- Резервные копии и изолированные документы ограничены тремя последними файлами, поэтому каталог пользовательских данных не растёт бесконечно.
- Запись выполняется только при изменении состояния: мутации `DesktopManager`, не меняющие layout, не публикуют `DesktopChanged` и не порождают запись на диск.

### Stage 8 — Theme System

- Темы читаются один раз при старте и остаются в памяти: `ThemeStore` не наблюдает за каталогом и не перечитывает файлы, поэтому фоновая нагрузка отсутствует.
- Каталог ограничен 64 темами по 512 KiB, а глубина JSON — 32 уровнями: стоимость загрузки ограничена сверху и не зависит от размера каталога.
- Файлы сортируются по имени файла, поэтому порядок диагностики и выбор первой темы не зависят от файловой системы.
- Непригодная тема отбрасывается по отдельности и не порождает повторных попыток: чтение каталога линейно по числу файлов.
- `ThemeManager` отдаёт неизменяемый снимок, поэтому подписчики `ThemeChanged` не перестраивают набор тем и не копируют его на каждое обращение.
- Смена темы не пишет на диск: выбор темы меняет только состояние в памяти, а `ActiveThemeId` сохраняется вместе с настройками обычным путём.

### Stage 10 — Application Lifecycle

- Проверка одиночности не создаёт фоновой работы: именованный mutex удерживается без потока и таймера, а его цена — один объект ядра на процесс.
- Перечисление окон (`EnumWindows`) выполняется только при повторном запуске и только до первого совпадения: в обычном сценарии первого экземпляра оно не запускается вовсе.
- Активация существующего экземпляра использует `ShowWindowAsync` и не блокирует вызывающий поток; отказ `SetForegroundWindow` не приводит к повторным попыткам или опросу.
- Освобождение lease происходит на штатном пути закрытия окна, поэтому ресурс не копится между запусками и не требует отдельного монитора процессов.

### Stage 11 — Performance

- Профилирование добавлено прежде оптимизаций: сначала измеряется baseline, и только найденные bottlenecks становятся поводом что-то менять (см. «Принципы проектирования»). Оптимизация «вслепую» противоречит цели стадии.
- Снимок ресурсов (`PerformanceSnapshot`) снимается дважды: baseline сразу после `window.Activate()` (к этому моменту в uptime попадает фактическое время запуска, а не только композиция объектов) и итог по закрытию окна, уже после освобождения hooks и источников событий, — поэтому приращения handles/потоков отражают состояние после остановки фоновой работы.
- `ProcessMetricsCollector` читает метрики через BCL (`Process`, `GC`), а не через native API: те же значения доступны без P/Invoke, а сбой любого отдельного поля даёт `0`, не роняя снимок (за исключением `OutOfMemoryException`/`StackOverflowException`). Handle-count берётся из `Process.HandleCount`; managed heap — из `GC.GetTotalMemory(forceFullCollection: false)`, чтобы снимок не запускал сборку мусора и не искажал замер.
- Итог сессии (`PerformanceSession.Summarize`) не хранит историю: считается разница между двумя снимками. Средняя загрузка CPU нормируется на число логических процессоров и ограничивается сверху 100%, потому что суммарное процессорное время нескольких потоков больше настенного времени одного ядра.
- Задержка доставки событий измеряется в `EventManager.Publish` через `Stopwatch.GetTimestamp()`/`GetElapsedTime` (не через `DateTime`, чтобы не зависеть от перевода системных часов) и только при наличии `EventLatencyRecorder`. Публикация без подписчиков не измеряется: она не отражает стоимость доставки. Сборщик потокобезопасен (lock), потому что publisher-потоки хук-событий и UI-поток публикуют параллельно.
- Периодическое idle-профилирование выключено по умолчанию: постоянный таймер — это как раз та фоновая нагрузка, которую стадия призвана устранять, поэтому он включается только явно через переменную окружения `GLAZESHELL_PERF_INTERVAL_MS`. Таймер одноразовый на каждый тик (`System.Threading.Timer` с перепланированием), освобождается в `Dispose`, который идемпотентен и вызывается на пути закрытия окна.
- `PerformanceMonitor` и `EventLatencyRecorder` живут как локальные значения `OnLaunched`, захваченные замыканием `window.Closed`, а не как поля `App`: так профилировщик освобождается тем же путём, что и остальные рантайм-ресурсы, и `App` не становится владельцем disposable-поля (CA1001).

### Принципы проектирования

- Использовать Windows events и callbacks вместо постоянных polling loops.
- Polling допускается только при documented technical limitation, с минимальным интервалом и измеримым обоснованием.
- Не создавать background service без конкретной потребности.
- Не доставлять события из фонового потока в UI без явного маршалинга на UI thread.
- Проверять idle CPU, RAM, startup time, thread count, Win32 handles, GDI/USER handles и allocations.
- Освобождать event hooks, native handles, subscriptions и disposable services на всех exit paths.
- Не выполнять тяжёлую disk/network операцию на UI thread.
- Не добавлять rendering или animation logic до появления соответствующего UI design.

### Измерения Stage 11

Stage 11 добавил инфраструктуру для измерения и покрывает следующими замерами:

- запуск приложения — baseline-снимок сразу после `window.Activate()`;
- idle CPU и RAM — снимки по таймеру при включённом `GLAZESHELL_PERF_INTERVAL_MS`;
- количество потоков и handles — поля `ThreadCount`/`HandleCount` снимка и их приращение в итоге сессии;
- event callback latency — `EventLatencyRecorder` (count/avg/min/max по синхронной доставке в `EventManager`);
- утечек resources после create/destroy сценариев — сравнение baseline и итога сессии по освобождении hooks и источников событий.

Поведение при monitor/DPI changes измеряется косвенно: соответствующие обработчики живут на тех же источниках событий, а их влияние видно в idle-снимках и приращении handles/потоков. Отдельные оптимизации по результатам замеров вносятся по мере обнаружения bottlenecks и фиксируются в этом документе.

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн согласован: палитра (`#12151B`, `#1B1F27`, `#F4F6FA`, `#98A2B3`, `#6E7A8A`, `#4E5866`), шрифты и layout сохраняются без явного согласования изменений.

Имена дисплеев — presentation-данные: отображаемое имя вычисляется в UI (значение по умолчанию плюс пользовательская переопределёнка из `UserSettings.MonitorNames`), а Core и Data хранят только карту «идентификатор монитора → имя» и не знают о Win32. Пользовательское имя сохраняется вместе с остальными настройками обычным путём persistence.

## Следующие архитектурные изменения

Дорожная карта Stage 0–13 завершена: Stage 13 Testing и Release добавил покрытие для доменной логики (identity, discovery на фейковых источниках, мапперы документов, мониторинг процессов) и задал релизный процесс — версия в `Directory.Build.props` (`0.1.0`), чеклист в [RELEASE.md](RELEASE.md) без CI. Ближайшие задачи вне карты: Installer и MSIX с code signing (первый действительно распределяемый релиз), CI для сборки и тестов, автозапуск при входе в систему и применение тем к визуальному дереву. Security review и hardening boundaries выполнены на Stage 12: чтение документов и тем ограничено на уровне открытого дескриптора, имена документов и ассеты тем проверяются строже, логовый редактор маскирует больше форм секретов, принудительное завершение по каталогу больше не распространяется на системную и общую область, а хуки окон снимаются потоком-владельцем. Application Lifecycle реализован на Stage 10 (single-instance и корректный startup/shutdown). Performance-инфраструктура (измерения baseline/idle/latency) введена на Stage 11. Persistence расширяется только по необходимости: если данные потребуют запросов или транзакций, будет рассмотрен переход на SQLite с явной версией схемы. Система тем расширяется в сторону каталога тем и в сторону UI: загрузчик уже ограничен лимитами и проверками, а применение темы к визуальному дереву остаётся за владельцем проекта. Перед добавлением каждого P/Invoke или Windows hook будут проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
