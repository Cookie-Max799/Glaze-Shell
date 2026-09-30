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
│   │   ├── Persistence/
│   │   └── Services/
│   ├── GlazeShell.Windows/
│   ├── GlazeShell.Data/
│   └── GlazeShell.Infrastructure/
├── tests/
│   ├── GlazeShell.Core.Tests/
│   ├── GlazeShell.Data.Tests/
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

Discovery models: `ApplicationCandidate`, `ApplicationDiscoveryResult`, `ApplicationDiscoveryOptions`, `ApplicationIdentity`, `ApplicationLaunchResult`.

Интерфейсы: `IApplicationManager`, `IApplicationLauncher`, `IApplicationDiscoverySource`, `IProcessInspector`, `IPackageLocationResolver`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IUserDataStore`, `IThemeManager`, `IEventManager`, `IMonitorManager`.

События: `WindowOpened`, `WindowClosed`, `ForegroundWindowChanged`, `WindowStateChanged`, `ProcessStarted`, `ProcessExited`, `DisplayChanged`, `DesktopChanged`, `SettingsChanged`, `ApplicationChanged`.

Сервисы: `EventManager` — типизированная подписка и публикация с возвратом `IDisposable` для отписки; `InMemorySettingsManager` — хранение настроек в памяти без публикации дублирующих событий; `ApplicationDiscoveryService` — агрегация источников, dedup, cache и single-flight; `DesktopManager` — in-memory манипуляции с `DesktopLayout` (вкладки, категории, размещение приложений и канонический порядок на трёх уровнях) с публикацией `DesktopChanged`; `MonitorLayoutBinding` — framework-free сверка привязок вкладок к списку доступных мониторов.

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
- `Applications/WindowsApplicationLauncher.cs`, `ProcessInspector.cs`, `PackageInstallLocationResolver.cs` — Stage 3 launcher;
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

Слой хранения и сериализации. Реализует JSON persistence пользовательских данных: `JsonUserDataStore` (`IUserDataStore`) читает и пишет `config.json`, `settings.json` и `layout.json`; `JsonDocumentPipeline` выполняет общий путь загрузки (лимит размера, разбор, миграции, маппинг, recovery); `SchemaMigrationRunner` применяет цепочку миграций одного типа документа; `UserDataDirectory` реализует атомарную запись, резервные копии и изоляцию повреждённых документов. Слой зависит только от Core и `System.Text.Json`, поэтому тестируется без Windows и без UI. SQLite в текущем дизайне не используется: документы пользователя малы и должны оставаться читаемыми и переносимыми.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest test project. Проверяет foundation configuration, инварианты моделей, семантику `EventManager`, публикацию `SettingsChanged` в `InMemorySettingsManager`, discovery service и `DesktopManager` (вкладки, активация, элементы, события `DesktopChanged`).

### GlazeShell.Data.Tests

MSTest test project. Проверяет `JsonUserDataStore` (round-trip, создание документов по умолчанию, recovery, изоляция, лимиты размера, атомарность записи, сохранение документов из будущих версий), `SchemaMigrationRunner` (последовательные миграции, пропуск версии, дубли, неподдерживаемые версии), `PersistingSettingsManager` и `DesktopLayoutPersistenceWriter`. Каждый тест работает в собственном временном каталоге, который удаляется после теста; файловой системой имитируется только недоступность каталогов `backups` и `recovery`.

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
- `PackageInstallLocationResolver` строит immutable cache реестра один раз и не держит открытый `RegistryKey` после инициализации.
- `ComApartment` выполняет COM в STA и балансирует `CoInitializeEx`/`CoUninitialize`; результат исключения пробрасывается в вызывающий поток через `ExceptionDispatchInfo`.

## Решения Stage 4 — Window Manager

- `WindowInfo.Id` — HEX-представление HWND: Core остаётся framework-free, а native handle восстанавливается `TryParseId` только на уровне Windows Integration.
- Окна перечисляются через `EnumWindows` и фильтруются: только видимые (`WS_VISIBLE`), не cloak-нутые (DWM `DWMWA_CLOAKED`) и не tool-windows; окна своего процесса не исключаются, чтобы панель показывала окна Glaze Shell тоже.
- События окон получаются через `SetWinEventHook` (OUTOFCONTEXT) на выделенном потоке с `GetMessage`-pump: опция `skipOwnProcess` включает `WINEVENT_SKIPOWNPROCESS`; hook-диапазоны разбиты на два (системный 0x0003–0x0017 и объектный 0x8000–0x8017) для предсказуемой доставки.
- Любое событие окон публикуется в `IEventManager` как `WindowOpened`/`WindowClosed`/`ForegroundWindowChanged`/`WindowStateChanged`; `EventManager` диспетчеризует по runtime-типу, поэтому подписка на конкретный тип не зависит от статического типа публикатора.
- Фокус окна — best-effort: `ShowWindowAsync(SW_RESTORE)` + `SetWindowPos` + `SetForegroundWindow`, при отказе (foreground lock) применяется `AttachThreadInput` к foreground/target threads; `SendInput` не используется.
- Управление состоянием — только `ShowWindowAsync` (не блокирует вызывающий поток), закрытие — только вежливый `WM_CLOSE` через `PostMessage`; принудительное завершение процесса не применяется.
- Инлайн-панель окон привязана к карточке приложения по пути исполняемого файла (ordinal-ignore-case); у MSIX `ExecutablePath` не заполнен, поэтому их окна не попадают в панель — задокументированное ограничение.
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

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн согласован: палитра (`#12151B`, `#1B1F27`, `#F4F6FA`, `#98A2B3`, `#6E7A8A`, `#4E5866`), шрифты и layout сохраняются без явного согласования изменений.

## Следующие архитектурные изменения

Следующие стадии: Theme System (загрузка данных тем без исполняемого кода), Application Lifecycle (single-instance и автозапуск) и Stage 13 Security (общий security review с hardening boundaries). Persistence расширяется только по необходимости: если данные потребуют запросов или транзакций, будет рассмотрен переход на SQLite с явной версией схемы. Перед добавлением каждого P/Invoke или Windows hook будут проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
