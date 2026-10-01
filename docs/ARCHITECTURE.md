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
├── .editorconfig
├── .gitattributes
├── .gitignore
├── docs/
│   ├── ARCHITECTURE.md
│   └── SECURITY.md
├── src/
│   ├── GlazeShell.App/
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
│       ├── Shell/
│       ├── Win32/
│       └── WindowManagement/
└── tests/
    ├── GlazeShell.Core.Tests/
    ├── GlazeShell.Data.Tests/
    └── GlazeShell.Windows.Tests/
```
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

WinUI 3 executable и composition root приложения. Содержит окно с явно заданным размером и центрированием на экране. На Stage 3 подключает `ApplicationDiscoveryService` и `WindowsApplicationLauncher`: создаёт реальный лаунчер-UI с поиском, списком приложений, статусной строкой и клавиатурной навигацией. На Stage 4 подключает `WindowManager` и события окон: UI-слой (`Presentation/MainViewModel`, `ApplicationListViewModel`, `WindowListItemViewModel`, `AsyncCommand`, `DispatcherQueueExtensions`) общается только с Core-интерфейсами. На Stage 7 создаёт `JsonUserDataStore`, читает конфигурацию один раз из каталога по умолчанию, определяет каталог данных, загружает layout с привязкой мониторов и настройки, подключает `DesktopLayoutPersistenceWriter` и `PersistingSettingsManager` и сбрасывает writer при закрытии окна.

### GlazeShell.Core

Независимый от UI и Win32 слой. Содержит доменные модели, контракты интерфейсов, события и базовые сервисы.

Модели:

- `Application`, `ApplicationCategory`, `DesktopItem`, `DesktopTab`, `DesktopLayout`;
- `UserSettings`;
- `WindowInfo`, `WindowType`, `WindowState`;
- `MonitorInfo`, `MonitorBounds`, `MonitorOrientation`;
- `Theme` с metadata, colors, fonts, dimensions, icons и анимацией;
- `ModelValidation` — единая валидация входных данных моделей.

Discovery models: `ApplicationCandidate`, `ApplicationDiscoveryResult`, `ApplicationDiscoveryOptions`, `ApplicationIdentity`, `ApplicationLaunchResult`, `PackageIdentity`.

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
- `Applications/WindowsApplicationLauncher.cs`, `ProcessInspector.cs` — Stage 3 launcher;
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

### GlazeShell.Data

Слой хранения и сериализации. Реализует JSON persistence пользовательских данных: `JsonUserDataStore` (`IUserDataStore`) читает и пишет `config.json`, `settings.json` и `layout.json`; `JsonDocumentPipeline` выполняет общий путь загрузки (лимит размера, разбор, миграции, маппинг, recovery); `SchemaMigrationRunner` применяет цепочку миграций одного типа документа; `UserDataDirectory` реализует атомарную запись, резервные копии и изоляцию повреждённых документов. Подкаталог `Themes/` отвечает за чтение пользовательских тем: `ThemeStore` читает `themes/*.json` и возвращает `ThemeLoadResult` с темами и диагностикой, `ThemeDocumentMapper` проверяет документ темы. Слой зависит только от Core и `System.Text.Json`, поэтому тестируется без Windows и без UI. SQLite в текущем дизайне не используется: документы пользователя малы и должны оставаться читаемыми и переносимыми.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest test project. Проверяет foundation configuration, инварианты моделей, семантику `EventManager`, публикацию `SettingsChanged` в `InMemorySettingsManager`, discovery service, `DesktopManager` (вкладки, активация, элементы, события `DesktopChanged`), `MonitorLayoutBinding`, `UserDataDirectoryName` и `ThemeManager` (порядок тем, выбор активной темы, `ThemeChanged`, запрет исполняемых ассетов и выхода за пределы каталога темы).

### GlazeShell.Data.Tests

MSTest test project. Проверяет `JsonUserDataStore` (round-trip, создание документов по умолчанию, recovery, изоляция, лимиты размера, атомарность записи, сохранение документов из будущих версий), `SchemaMigrationRunner` (последовательные миграции, пропуск версии, дубли, неподдерживаемые версии), `PersistingSettingsManager`, `DesktopLayoutPersistenceWriter` и `ThemeStore` (загрузка тем, отклонение непригодных по отдельности, лимиты количества и размера, проверка формата цвета и безопасности ассетов). Каждый тест работает в собственном временном каталоге, который удаляется после теста; файловой системой имитируется только недоступность каталогов `backups` и `recovery`.

### GlazeShell.Windows.Tests

MSTest test project. Проверяет managed резолвер `.lnk` (`ShellLinkResolverTests`), источники Start Menu и AppsFolder, `WindowsApplicationLauncher`, window manager (`WindowManagerTests` с окном-фикстурой `Win32TestWindow` на pumping-потоке) и monitor manager (`MonitorManagerTests`: перечисление мониторов, primary, геометрия, DPI и событие `DisplayChanged` через `HWND_BROADCAST`). Использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо сломанного в окружении COM `IShellLinkW.Save`; environment-зависимые проверки (foreground lock, мониторы) завершаются `Inconclusive`, а не падают.

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
- `ProcessInspector.FindProcessesByPackage` определяет пакет по разбору пути процесса (`WindowsApps\<PackageFullName>`), а не по каталогу установки из реестра.
- `ComApartment` выполняет COM в STA и балансирует `CoInitializeEx`/`CoUninitialize`; результат исключения пробрасывается в вызывающий поток через `ExceptionDispatchInfo`.

## Решения Stage 4 — Window Manager

- `WindowInfo.Id` — HEX-представление HWND: Core остаётся framework-free, а native handle восстанавливается `TryParseId` только на уровне Windows Integration.
- Окна перечисляются через `EnumWindows` и фильтруются: только видимые (`WS_VISIBLE`), не cloak-нутые (DWM `DWMWA_CLOAKED`) и не tool-windows; окна своего процесса не исключаются, чтобы панель показывала окна Glaze Shell тоже.
- События окон получаются через `SetWinEventHook` (OUTOFCONTEXT) на выделенном потоке с `GetMessage`-pump: опция `skipOwnProcess` включает `WINEVENT_SKIPOWNPROCESS`; hook-диапазоны разбиты на два (системный 0x0003–0x0017 и объектный 0x8000–0x8017) для предсказуемой доставки.
- Любое событие окон публикуется в `IEventManager` как `WindowOpened`/`WindowClosed`/`ForegroundWindowChanged`/`WindowStateChanged`; `EventManager` диспетчеризует по runtime-типу, поэтому подписка на конкретный тип не зависит от статического типа публикатора.
- Фокус окна — best-effort: `ShowWindowAsync(SW_RESTORE)` + `SetWindowPos` + `SetForegroundWindow`, при отказе (foreground lock) применяется `AttachThreadInput` к foreground/target threads; `SendInput` не используется.
- Управление состоянием — только `ShowWindowAsync` (не блокирует вызывающий поток), закрытие — только вежливый `WM_CLOSE` через `PostMessage`; принудительное завершение процесса не применяется.
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
- Сопоставление события процесса с приложением идёт по полному пути к исполняемому файлу, а при его отсутствии — по имени образа; у MSIX-приложений без `ExecutablePath` — по имени семейства пакета, извлечённому из пути процесса (`WindowsApps\<PackageFullName>`). Сравнение только по имени образа дало бы ложные совпадения (`update.exe`, `setup.exe` у разных приложений), поэтому для Win32 полный путь остаётся основным признаком.
- `EventCoalescer` объединяет пачку сигналов в одно действие. Система присылает изменение дисплеев несколькими сообщениями подряд, и перечисление мониторов на каждое из них было бы лишней работой, заметной при подключении монитора. Обработчик ошибок обязателен: неперехваченное исключение в потоке таймера завершило бы процесс.
- `Cancel()` отменяет отложенную публикацию при остановке источника. Иначе источник, уже отключённый, успевал бы опубликовать `DisplayChanged` после закрытия, и подписчики получали бы данные от источника, которого больше нет.
- Отбор оконных сообщений, означающих изменение дисплеев, намеренно узкий: `WM_DEVICECHANGE` обрабатывается только для `DBT_DEVNODES_CHANGED`, `WM_SETTINGCHANGE` — только для `SPI_SETWORKAREA` и `SPI_SETLOGICALDPIOVERRIDE`. Остальные сообщения этой группы приходят постоянно (смена клавиатуры, раскладки, темы) и не меняют ни состав, ни геометрию мониторов.
- Определение состояния MSIX-приложения по AUMID удалено: оно сравнивало AUMID целевого приложения с AUMID собственного процесса оболочки, то есть проверяло не то приложение. Корректная альтернатива потребовала бы COM-вызова для каждого процесса (интерфейс создаётся на отдельном STA-потоке), что несопоставимо дороже проверки по каталогу установки. Пакет определяется разбором пути процесса (`PackageIdentity`), потому что каталог установки из реестра для bundle-пакетов указывает на `neutral`-вариант вместо установленного. Побочный эффект: пакеты с фоновым сервисом без окна показываются запущенными, пока сервис жив.

## Windows API

### Текущий статус

Glaze Shell использует только официальные Win32 и COM API. P/Invoke и COM-интерфейсы изолированы в `GlazeShell.Windows` (каталоги `Interop`, `Shell`, `Win32`, `Applications`, `WindowManagement`, `DisplayManagement`); `GlazeShell.Core` и `GlazeShell.Data` Windows API не используют: Core не содержит native типов, а Data работает через BCL (`System.Text.Json`, `System.IO`) и зависит только от Core, поэтому проверки безопасности Windows к этим слоям не применяются.

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
| Processes | `Process.GetProcesses`, `CloseMainWindow` | `Applications/WindowsApplicationLauncher.cs`, `Applications/ProcessInspector.cs`, `SystemEvents/SystemProcessSnapshotSource.cs` |
| COM | `CoInitializeEx`, `CoUninitialize`, `CoTaskMemFree`, `CoCreateInstance` | `Win32/Ole32.cs`, `Shell/ShellIdentifiers.cs` |
| Windows | `EnumWindows`, `GetForegroundWindow`, `IsWindow`, `GetWindowText`, `GetClassName`, `GetWindowThreadProcessId`, `ShowWindowAsync`, `PostMessage`, `SetForegroundWindow`, `AttachThreadInput`, `GetWindowPlacement`, `SetWindowPos` | `Win32/User32.cs` |
| Windows events | `SetWinEventHook`/`UnhookWinEvent`, `GetMessage`/`TranslateMessage`/`DispatchMessage` (0x0003–0x0017, 0x8000–0x8017) | `Win32/User32.cs`, `WindowManagement/WindowEventMonitor.cs` |
| Window cloak | `DwmGetWindowAttribute` (`DWMWA_CLOAKED`) | `Win32/Dwmapi.cs`, `WindowManagement/NativeWindowEnumerator.cs` |
| Monitors | `EnumDisplayMonitors`, `EnumDisplaySettingsW`, `MonitorFromWindow`, `MonitorFromPoint` | `Win32/User32.cs`, `DisplayManagement/NativeMonitorEnumerator.cs` |
| DPI | `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI`), `GetDpiForSystem`, `GetDpiForWindow` | `Win32/Shcore.cs`, `Win32/User32.cs` |
| Display events | `WM_DISPLAYCHANGE` (`0x007E`), `WM_DEVICECHANGE` (`0x0219`), `WM_SETTINGCHANGE` (`0x001A`), `WM_DPICHANGED` (`0x02E0`) в hidden message window, `DisplayChanged` | `DisplayManagement/MonitorEventMonitor.cs`, `DisplayManagement/DisplaySignal.cs`, `Win32/User32.cs` |
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
- UI проверяет фоновые процессы не чаще, чем раз в 3 секунды, и только после завершения первичного сканирования.
- Периодический polling оправдан техническим ограничением: .NET не предоставляет событие «процесс запущен» для произвольных exe.

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

### Принципы проектирования

- Использовать Windows events и callbacks вместо постоянных polling loops.
- Polling допускается только при documented technical limitation, с минимальным интервалом и измеримым обоснованием.
- Не создавать background service без конкретной потребности.
- Не доставлять события из фонового потока в UI без явного маршалинга на UI thread.
- Проверять idle CPU, RAM, startup time, thread count, Win32 handles, GDI/USER handles и allocations.
- Освобождать event hooks, native handles, subscriptions и disposable services на всех exit paths.
- Не выполнять тяжёлую disk/network операцию на UI thread.
- Не добавлять rendering или animation logic до появления соответствующего UI design.

### Планируемые измерения

Stage 11 выполнит профилирование и зафиксирует baseline для:

- запуска приложения;
- idle CPU и RAM;
- количества потоков и handles;
- поведения при monitor/DPI changes;
- event callback latency;
- утечек resources после create/destroy сценариев.

Результаты измерений и найденные bottlenecks будут добавлены в этот документ.

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн согласован: палитра (`#12151B`, `#1B1F27`, `#F4F6FA`, `#98A2B3`, `#6E7A8A`, `#4E5866`), шрифты и layout сохраняются без явного согласования изменений.

## Следующие архитектурные изменения

Следующие стадии: Windows Events (централизованная event-driven модель), Application Lifecycle (single-instance и автозапуск) и Stage 13 Security (общий security review с hardening boundaries). Persistence расширяется только по необходимости: если данные потребуют запросов или транзакций, будет рассмотрен переход на SQLite с явной версией схемы. Система тем расширяется в сторону каталога тем и в сторону UI: загрузчик уже ограничен лимитами и проверками, а применение темы к визуальному дереву остаётся за владельцем проекта. Перед добавлением каждого P/Invoke или Windows hook будут проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
