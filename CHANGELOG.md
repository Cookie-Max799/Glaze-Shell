# Changelog

Все значимые изменения Glaze Shell будут записываться в этом файле.

## [Unreleased]

### Added

#### Stage 9 — Windows Events

- Реализован `ShellEventCoordinator` (`GlazeShell.Windows/SystemEvents`) — единая точка запуска источников системных событий: оконные события (`SetWinEventHook`), события дисплеев (скрытое message window) и события процессов. Источники запускаются из одного места, поэтому ни один не поднимается дважды и ни один не остаётся работать после закрытия приложения.
- Отказ отдельного источника не отменяет запуск остальных: он попадает в `ShellEventStatus.Diagnostics`, а подписчики на его события просто не приходят. `Start()` возвращает `ShellEventStatus` с флагами по источникам и `AllSourcesStarted`; повторный `Start()` выбрасывает `InvalidOperationException`, повторный `Dispose()` не делает ничего.
- Добавлен контракт `IWindowsEventSource` (`Start()` + `IDisposable`) в слое Windows Integration: `WindowManager` и `MonitorManager` реализуют его и остаются при этом источниками запросов состояния для интерфейса. Контракт живёт не в Core, потому что Core не владеет WinEvent hooks и message windows.
- Реализован `ProcessEventMonitor` — публикация `ProcessStarted`/`ProcessExited` по разнице снимков списка процессов (`ProcessWatchOptions`, интервал по умолчанию 1 с, диапазон 500 мс – 5 мин). События «процесс запущен» в Windows нет: `SetWinEventHook` работает с окнами, ETW требует прав администратора, а WMI — службы, которая часто отключена. Первый снимок — базовый и событий не порождает: процессы, работающие до запуска Glaze Shell, не считаются только что запущенными.
- Снимок стоит одного перечисления процессов: имя и путь читаются только для новых PID, для известных используется кэш предыдущего снимка, поэтому цена прохода в установившемся состоянии не зависит от числа запущенных программ. Если на события никто не подписан, перечисление не выполняется вовсе (`HasSubscribers<TEvent>`).
- `ProcessExited` расширен необязательным `ExecutablePath` (симметрично `ProcessStarted`): выход публикуется раньше запуска, поэтому перезапуск приложения читается как «вышел, затем открыто». Обратная совместимость вызовов с одним и двумя аргументами сохранена.
- Добавлен `EventCoalescer` (`GlazeShell.Core/Services`, framework-free): объединяет частые запросы в одно выполнение. Обработчик ошибок обязателен — неперехваченное исключение в потоке таймера завершило бы процесс, а молча проглотить ошибку нельзя. `Cancel()` отменяет отложенное выполнение, чтобы остановленный источник не публиковал данные после остановки.
- `MonitorEventMonitor` теперь реагирует на `WM_DEVICECHANGE` (только `DBT_DEVNODES_CHANGED`), `WM_SETTINGCHANGE` (только `SPI_SETWORKAREA` и `SPI_SETLOGICALDPIOVERRIDE`) и `WM_DPICHANGED`. Отбор вынесен в `DisplaySignal` и намеренно узкий: остальные сообщения этой группы приходят постоянно (смена клавиатуры, раскладки, темы) и не меняют ни состав, ни геометрию мониторов, поэтому перечисление мониторов на них было бы лишней работой.
- `MonitorManager` публикует `DisplayChanged` один раз после паузы 300 мс вместо перечисления мониторов на каждое сообщение, а при `Dispose()` отменяет отложенную публикацию.
- `IEventManager` расширен `HasSubscribers<TEvent>()`: источники используют его, чтобы не выполнять фоновую работу, когда никто не слушает. Учитывается только точный тип события.
- `App.xaml.cs` создаёт `ShellEventCoordinator` после создания окна (подписчики уже зарегистрированы, ни одно событие не теряется между подпиской и первым проходом источника), логирует статус запуска и диагностики отказов, а при закрытии окна освобождает все источники. Отказ источника не прерывает запуск приложения: он попадает в лог как warning.
- Добавлены тесты Stage 9: `EventCoalescerTests` (объединение запросов, запрос во время выполнения, `Flush`/`Cancel`, изоляция ошибки, работа после сбоя), `EventManagerTests` на `HasSubscribers`, `ProcessEventMonitorTests` (базовый снимок, публикация запуска и выхода, порядок «вышел, затем открыто», отсутствие пути, пропуск снимков без подписчиков, восстановление после сбоя снимка, остановка при освобождении), `ShellEventCoordinatorTests` (статус запуска, изоляция отказа источника, однократность `Start`, идемпотентность `Dispose`), `DisplaySignalTests` (отбор оконных сообщений) и расширенные `MonitorManagerTests` (коалесцирование пачки, разнесённые во времени сигналы, отмена отложенной публикации при освобождении).

### Changed

- `MainViewModel` больше не опрашивает состояние запущенных приложений каждые 3 секунды. Состояние обновляется по событиям `ProcessStarted`/`ProcessExited`: приложения сопоставляются по полному пути к исполняемому файлу, при его отсутствии — по имени образа, пересчитываются только затронутые приложения, а пачка событий одного запуска объединяется (`EventCoalescer`, 250 мс). Однократный полный пересчёт остался в `RefreshAsync` и задаёт базовую линию для приложений, запущенных до Glaze Shell.
- `WindowsApplicationLauncher.IsRunningAsync` больше не определяет состояние MSIX-приложения по AUMID собственного процесса оболочки (`Environment.ProcessId`). Состояние пакета определяется по процессам в его каталоге установки; если каталог не разрешён, состояние считается неизвестным, а не «не запущено» в отчёт об остановке. Проверка AUMID потребовала бы COM-вызова для каждого процесса, что несопоставимо дороже проверки по пути.

### Changed (ранее)

- Документация сведена к двум файлам, чтобы её было проще читать на GitHub: `docs/ARCHITECTURE.md` (слои, решения по этапам, таблица использованных Windows API, производительность) и `docs/SECURITY.md` (модель угроз и принятые риски). Раздел «Разработка» перенесён в `README.md`; `docs/DEVELOPMENT.md`, `docs/WINDOWS_API.md`, `docs/PERFORMANCE.md` и корневой `ARCHITECTURE.md` удалены. Содержание сохранено полностью, изменены только пути, уровни заголовков и формулировки под фактическое состояние репозитория.
- `docs/ARCHITECTURE.md`: таблица Windows API дополнена реально используемыми API Stage 5/6 (`EnumDisplayMonitors`, `EnumDisplaySettingsW`, `MonitorFromWindow`, `MonitorFromPoint`, `GetDpiForMonitor`, `GetDpiForSystem`, `GetDpiForWindow`, `WM_DISPLAYCHANGE`, `SHGetKnownFolderPath`/`SHGetKnownFolderItem`) и исправлена ссылка на `Shell/IPersistFile.cs`: интерфейс объявлен в `Shell/IShellLinkW.cs`, отдельного файла нет.
- Из структуры репозитория убраны пустые каталоги-заглушки с `.gitkeep`: `assets`, `GlazeShell.Data/Database`, `GlazeShell.Infrastructure/Diagnostics`, `GlazeShell.Infrastructure/Startup`, `GlazeShell.Windows/Windows`, а также устаревшие `.gitkeep` в непустых `GlazeShell.Windows/Interop`, `Shell` и `Win32`. Git не хранит пустые каталоги, поэтому файлы-заглушки только мешали читать дерево репозитория; каталоги появятся вместе с содержимым.

### Added

#### Stage 8 — Theme System

- Реализован `ThemeManager` (`IThemeManager`): реестр тем в детерминированном порядке (по имени, затем по идентификатору), выбор действующей темы, `ThemeChanged` при фактической смене. Темы возвращаются как неизменяемый снимок и не меняются после загрузки.
- `IThemeManager` расширен `GetActiveTheme` и `SetActiveTheme`. Действующая тема всегда существует: пока тема не выбрана, действует встроенная `Theme.CreateDefault()` (`glaze-default`). Неизвестный идентификатор не оставляет приложение без темы — сохраняется предыдущая.
- Добавлена встроенная тема `glaze-default`: цвета (`layerBackground`, `layerText`, `subtleText`, `controlBackground`, `controlBackgroundHover`, `controlBorder`, `accentBackground`, `accentText`, `criticalBackground`, `criticalText`), шрифты, размеры и эффекты.
- Добавлен `ThemeStore` (`GlazeShell.Data/Themes`): чтение пользовательских тем из каталога `themes` в каталоге данных приложения, только `*.json` верхнего уровня. Темы наполняет пользователь, поэтому программа их только читает.
- Непригодная тема отбрасывается по отдельности, а не отправляет весь набор в recovery: пользовательские темы — данные пользователя, а не документы программы. Причины отклонения возвращаются в `ThemeLoadResult.Diagnostics`.
- Добавлены `ThemeDocument` и `ThemeDocumentMapper`: проверяются идентификатор, имя, метаданные, формат цвета (`#RGB`, `#RRGGBB`, `#AARRGGBB`), размеры, режим обоев, диапазоны анимаций и лимиты количества (64 темы, 256 цветов, 64 шрифта, 256 иконок, 512 KiB на файл).
- `App.xaml.cs` загружает темы при старте, выбирает тему из `UserSettings.ActiveThemeId` и логирует количество загруженных тем, диагностику и факт замены недоступной темы на встроенную.
- `ThemeColors.Find` ищет цвет по имени без учёта регистра; дубликаты имён цветов отклоняются моделью.
- Добавлены тесты `ThemeManagerTests` (Core) и `ThemeStoreTests` (Data), включая запрет исполняемых ассетов, выход за пределы каталога темы, неверный формат цвета, превышение лимитов и устойчивость к одной повреждённой теме.
- Результат: 220/221 tests green в Release (`GlazeShell.Core.Tests` 121/121, `GlazeShell.Data.Tests` 64/64, `GlazeShell.Windows.Tests` 35 passed + 1 пропущенный). Debug и Release build — 0 warnings / 0 errors.

#### Stage 7 — Persistence

- Добавлен слой `GlazeShell.Data` с `JsonUserDataStore` (`IUserDataStore`): документы `config.json`, `settings.json`, `layout.json` в каталоге пользовательских данных, чтение и запись по требованию.
- Добавлены DTO и строгие мапперы `ConfigurationDocument`, `SettingsDocument`, `LayoutDocument` с проверкой идентификаторов, имён, уникальности id, ссылок на активную вкладку и лимитов `PersistenceLimits` (4 MiB на документ, глубина JSON 32, 512 вкладок, 512 категорий на вкладку, 4096 элементов на категорию).
- Версия схемы хранится в каждом документе (`schemaVersion`). `SchemaMigrationRunner` применяет миграции строго последовательно, отказывается строить цепочку с пропуском версии или дублем и не трогает документы из будущих версий приложения (`UnsupportedSchemaVersionException` → `PersistenceStatus.Unsupported`).
- Миграции принадлежат конкретному типу документа (`UserDataDocumentKind`): миграция настроек никогда не применяется к конфигурации или layout.
- Перед перезаписью миграцией создаётся резервная копия в `backups` (хранятся 3 последние). Если копию создать не удалось, документ не переписывается, а переходит в recovery.
- Запись атомарна: временный файл рядом с целью, `FileStream.Flush(flushToDisk: true)` и `File.Move(overwrite)`; превышение лимита записи отклоняется до касания существующего файла.
- Повреждённые документы изолируются перемещением в `recovery` (3 последние) и заменяются значениями по умолчанию. Если изолировать файл не удалось, он остаётся нетронутым, а значения по умолчанию применяются только в памяти.
- `JsonDocumentPipeline` — общий путь загрузки (лимит размера до разбора, разбор, миграции, маппинг, recovery) с единым отчётом об ошибках `PersistenceErrorReporter`.
- `DesktopLayoutPersistenceWriter` сохраняет layout по событию `DesktopChanged` с объединением изменений (400 мс) и `Flush`/`Dispose` при закрытии окна.
- `PersistingSettingsManager` загружает настройки при старте, сохраняет их при изменении и публикует `SettingsChanged`. Ошибка записи не отменяет изменение: она передаётся обработчику и попадает в лог.
- Привязка вкладок к мониторам: `DesktopTab.MonitorId`, `DesktopTab.WithMonitor`, `IDesktopManager.AssignTabToMonitor` и `GetTabsForMonitor`, а также framework-free сервис `MonitorLayoutBinding.Reconcile`.
- При загрузке layout привязки к недоступным мониторам снимаются, каждая снятая привязка логируется, а результат сверки сразу сохраняется. Пустой или недоступный список мониторов не считается основанием для снятия привязок.
- Валидация имени каталога данных вынесена в `UserDataDirectoryName` (Core) и используется `UserDataPaths` и маппером конфигурации: запрещены разделители, обход каталогов, зарезервированные имена устройств, точка/пробел в конце и длина более 64 символов.
- `App.xaml.cs`: конфигурация читается один раз из каталога по умолчанию, корневой каталог берётся из неё, layout/settings загружаются из него же, логирование старта и статус загрузки документов пишутся в лог.
- Добавлен тестовый проект `GlazeShell.Data.Tests` (46 тестов) и расширен `GlazeShell.Core.Tests` (`MonitorLayoutBindingTests`, `UserDataDirectoryNameTests`).
- Результат: 180/181 tests green в Debug и Release (`GlazeShell.Core.Tests` 99/99, `GlazeShell.Data.Tests` 46/46, `GlazeShell.Windows.Tests` 35 passed + 1 пропущенный `FocusBringsWindowToForegroundOrIsDeniedBySystem` как environment-tolerant). Debug и Release build — 0 warnings / 0 errors.

#### Fixed

- Исправлена двойная загрузка конфигурации при старте: при смене `DataDirectoryName` второй load из нового каталога создавал там документ по умолчанию и сбрасывал имя приложения.
- Исправлена атомарная запись: `FileStream.Flush(flushToDisk: true)` вызывался после `Dispose` писателя, из-за чего любая запись завершалась `ObjectDisposedException` и документ не сохранялся.
- Снятые привязки к мониторам теперь сохраняются, иначе они вычислялись бы заново при каждом запуске.

#### Changed

- `MonitorLayoutBinding.Reconcile` при пустом или недоступном списке мониторов возвращает layout без изменений: при отсутствии достоверных сведений привязки не снимаются.
- `IsValidIdentifier` отклоняет управляющие символы, как и `IsValidName`: идентификаторы попадают в диагностику и логи.
- `GlazeShell.Data` подключён к `GlazeShell.Core`; `GlazeShell.App` подключён к `GlazeShell.Data`.

#### Stage 6 — Tabs and Categories

- Реализован полный backend рабочего пространства в `DesktopManager` (Core): вкладки (`CreateTab`, `RemoveTab`, `RenameTab`, `MoveTab`, `ActivateTab`), категории (`CreateCategory`, `RemoveCategory`, `RenameCategory`, `MoveCategory`) и размещение приложений (`AddApplication`, `RemoveApplication`, `MoveApplication`).
- `MoveApplication` поддерживает перенос элемента между категориями с перенумерацией `Order` в исходной и целевой коллекциях; в пределах одной категории поведение совпадает с переупорядочиванием.
- `AddApplication` принимает необязательный `categoryId`; без него элемент попадает в первую категорию вкладки, а при отсутствии категорий лениво создаётся категория по умолчанию (`main`/«Основная»).
- Добавлен канонический порядок на всех трёх уровнях layout: `ApplicationCategory` получил поле `Order`, `Order` нормализуется в плотную последовательность `0..N-1` и совпадает с позицией в коллекции.
- Нормализация внешнего layout выполняется на границе менеджера: `SetLayout` и конструктор с `initialLayout` сортируют по `Order` (с сохранением исходного порядка при равенстве) и перенумеровывают вкладки, категории и элементы.
- `CreateTab` и `CreateCategory` принимают `int? order`: `null` добавляет объект в конец, явное значение вставляет по позиции с клампингом.
- Все мутации unified-проходят через `Mutate(Func<DesktopLayout, DesktopLayout?>)`: запись в состояние происходит только после успешного вычисления, поэтому исключение валидации не оставляет частично обновлённого layout; `DesktopChanged` публикуется вне lock.
- Мутации, не меняющие состояние (переименование в то же имя, перемещение в текущую позицию), возвращают `false` и не публикуют `DesktopChanged`.
- Добавлены `DesktopTab.With(...)`, `ApplicationCategory.With(...)` и `DesktopItem.WithOrder(int)` для построения изменённых immutable-моделей с сохранением валидации (по образцу `Application.WithMetadata`).
- Расширены `DesktopManagerTests`: вкладки (вставка по позиции, переименование, перемещение, append по умолчанию, клампинг), категории (CRUD, вставка, перемещение, append по умолчанию), размещение приложений (прицельная категория, дубликаты, перенумерация, перенос между категориями), нормализация layout и отсутствие публикации при no-op.
- Результат: 118/119 tests green в Debug и Release (`GlazeShell.Core.Tests` 83/83, `GlazeShell.Windows.Tests` 35 passed); `FocusBringsWindowToForegroundOrIsDeniedBySystem` пропускается как environment-tolerant (результат зависит от foreground lock текущей сессии и меняется между прогонами). Debug и Release build — 0 warnings / 0 errors.

#### Changed

- `IDesktopManager`: `AddItem`/`RemoveItem`/`MoveItem` переименованы в `AddApplication`/`RemoveApplication`/`MoveApplication` согласно плану §13. Ломающее изменение контракта; прямых потребителей кроме `DesktopManager` и тестов не было.
- `IDesktopManager` дополнен `RenameTab`, `MoveTab`, `CreateCategory`, `RemoveCategory`, `RenameCategory`, `MoveCategory` согласно плану §12/§13.
- `CreateTab`/`CreateCategory` изменили сигнатуру: `int order = 0` заменён на `int? order = null` (append). Прежнее значение по умолчанию вставляло каждый новый объект в начало коллекции.

#### Removed

- Удалено `DesktopTab.IsActive`: поле дублировало `DesktopLayout.ActiveTabId` и никогда не поддерживалось `DesktopManager`, из-за чего всегда оставалось `false`. Единственный источник истины — `DesktopLayout.ActiveTabId`.

#### Documentation

- `README.md`: добавлен статус Stage 6, обновлено описание desktop layout и known issues; в roadmap отмечено выполнение вкладок/категорий, привязка layout к мониторам отнесена к Stage 7.
- `ARCHITECTURE.md`: добавлен раздел «Решения Stage 6», обновлено описание `DesktopManager`; привязка layout к мониторам перенесена из Stage 6 в Stage 7 вместе с persistence.
- XAML не изменялся: Stage 6 добавляет только backend-контракт, подключение UI выполняет владелец проекта.

#### Stage 2 — Application Discovery

- Добавлен managed-парсер бинарных shortcut-файлов `ShellLinkData` (MS-SHLLINK): header, LinkInfo, StringData, relative path, byte-safe и exception-free API.
- Добавлен `ShellLinkResolver` с трёхуровневой стратегией: managed fast path → shell property store → `IShellLinkW`. Каждый `.lnk` с отсутствующим target пропускается с диагностикой.
- Исправлен interop: `IShellLinkW` наследует `IPersistFile`, удалены generic COM-методы, добавлены `out nint` handlers, добавлен `IShellItemArray`.
- `AppsFolderSource` переписан на `IShellItemArray` с P/Invoke (IID), STA-апартментом и корректным lifetimes COM-объектов.
- `StartMenuShortcutSource` переписан: bounded parallel scan (max CPU 1–8), детерминированная сортировка, рекурсивный обход, фильтрация reparse points, `.url`, `explorer.exe` и отсутствующих target.
- Добавлены `Ole32` (`CoInitializeEx`, `CoUninitialize`, `CoTaskMemFree`), `Shell32` (`SHCreateItemFromParsingName`) и `ComApartment` для STA COM.
- `ApplicationDiscoveryService` дополнен слиянием кандидатов, dedup по launch identity и лимитом предупреждений на источник.

#### Stage 3 — Application Launcher

- Реализован `WindowsApplicationLauncher`: MSIX через `IApplicationActivationManager`, Win32 через `ProcessStartInfo` (`UseShellExecute=false`), закрытие через `CloseMainWindow` без принудительного kill.
- Реализован `ProcessInspector` — поиск процессов по пути и каталогу установки с освобождением `Process` handles.
- Реализован `PackageInstallLocationResolver` — поиск установки MSIX через реестр AppxAllUserStore с immutable cache.
- Исправлен vtable `IApplicationActivationManager`: `GetApplicationUserModelId` принимает process handle, для PID используется `GetApplicationUserModelIdFromProcessId`.
- `ComApartment` балансирует `CoInitializeEx`/`CoUninitialize` на dedicated STA.

#### Stage 4 — Window Manager

- Исправлен `EventManager`: публикация диспетчеризуется по runtime-типу события (`GetType()`), поэтому подписчики на конкретный тип получают производные события, публикуемые через базовый `GlazeEvent`.
- Добавлен `NativeWindowEnumerator`: перечисление видимых top-level окон через `EnumWindows` с фильтрацией cloak-нутых (`DWMWA_CLOAKED`) и tool-window, чтение состояния, заголовка, процесса и пути exe по HWND.
- Добавлен `WindowEventMonitor`: `SetWinEventHook` (OUTOFCONTEXT, два диапазона: 0x0003–0x0017 системные события и 0x8000–0x8017 объектные) на dedicated-потоке с `GetMessage`-pump; публикует `WindowOpened`/`WindowClosed`/`ForegroundWindowChanged`/`WindowStateChanged` через `IEventManager`; опция `skipOwnProcess`.
- Реализован `WindowManager` (`IWindowManager`): `GetWindows`, `GetWindow`, `GetForegroundWindow`, фокус (best-effort с fallback через `AttachThreadInput`), свернуть/развернуть/восстановить через `ShowWindowAsync`, закрытие только через `WM_CLOSE`.
- Добавлены `Win32/User32.cs`, `Win32/Dwmapi.cs`, `Win32/Kernel32.cs`; P/Invoke user32 используют `CharSet.Unicode` без `ExactSpelling` для корректного разрешения W-суффиксов.
- UI: добавлен `AsyncCommand<T>`, `WindowListItemViewModel` и инлайн-панель окон в карточке приложения (заголовок, состояние, активное окно, кнопки «Фокус/Свернуть/Развернуть/Закрыть`); `MainViewModel` подписан на события окон и обновляет панель на UI-потоке через коалесированный refresh.

#### Stage 5 — Desktop Integration

- Расширен `IMonitorManager`: `GetMonitorForWindow`, `GetMonitorForPoint`, поиск по id и primary; расширен `IDesktopManager` операциями вкладок и элементов (`CreateTab`, `RemoveTab`, `ActivateTab`, `AddItem`, `RemoveItem`, `MoveItem`).
- Реализован `DesktopManager` (Core): framework-free in-memory манипуляции с `DesktopLayout` — вкладки, «Основная» категория, порядок элементов; публикует `DesktopChanged` через `IEventManager` при каждом мутирующем вызове.
- Добавлены display P/Invoke: `EnumDisplayMonitors`, `GetMonitorInfoW` (`MONITORINFOEXW`), `MonitorFromWindow`, `MonitorFromPoint`, `EnumDisplaySettingsW` (`DEVMODEW` с union через explicit layout), `GetDpiForSystem`/`GetDpiForWindow` (User32) и `GetDpiForMonitor` (`MDT_EFFECTIVE_DPI`, Shcore).
- Добавлен `NativeMonitorEnumerator`: перечисляет мониторы (id = HEX-представление `HMONITOR`), читает bounds, working area, primary-флаг, DPI scale factor, refresh rate (`dmDisplayFrequency`) и ориентацию (`dmDisplayOrientation`).
- Добавлен `MonitorEventMonitor`: hidden window на dedicated-потоке (`CreateWindowExW` + `GetMessage`-pump) перехватывает `WM_DISPLAYCHANGE` и уведомляет `MonitorManager`.
- Реализован `MonitorManager` (`IMonitorManager`): `Start`/`Dispose` по паттерну `WindowManager`, публикует `DisplayChanged` со свежим снимком мониторов.
- UI: панель «Мониторы» в футере — список (device name, ориентация, refresh rate, DPI, рабочие площади), badge «Основной/Дополнительный» и сводка `MonitorSummary`; `MainViewModel` подписан на `DisplayChanged` и обновляет панель на UI-потоке.
- Тесты: `DesktopManagerTests` (вкладки, активация, добавление/удаление/перемещение элементов, events) и `MonitorManagerTests` (environment-tolerant: перечисление, primary, геометрия, событие `DisplayChanged` через `HWND_BROADCAST`).
- Результат на момент Stage 5: 84/85 tests green (`GlazeShell.Core.Tests` 49/49, `GlazeShell.Windows.Tests` 35 passed, 1 environment-tolerant skip), Debug и Release build — 0 warnings / 0 errors.

#### UI

- Добавлен простой лаунчер UI: поиск, список приложений с virtualization, статусная строка, кнопки «Запустить/Закрыть/Перезапустить/Обновить».
- Добавлен `MainViewModel` с async обновлением, фильтрацией по названию/пути/AUMID и периодической проверкой запущенных процессов.
- Добавлена клавиатурная навигация: ↑/↓, Enter, Space, F5, Esc.
- Приложение теперь подключает `GlazeShell.Windows` и использует реальный composition root в `App.xaml.cs`.

#### Tests

- Управляемый writer `.lnk`-фикстур `ShellLinkBuilder` и `TestShortcutFactory` (без COM `IShellLinkW.Save`, который сломан в текущей среде).
- Добавлены `ShellLinkResolverTests`, `WindowsApplicationLauncherTests`, обновлены `AppsFolderSourceTests` (environment-tolerant) и `StartMenuShortcutSourceTests`.
- Добавлены window manager тесты: `Win32TestWindow` (окно-фикстура на pumping-потоке) и `WindowManagerTests` — перечисление, поиск, свернуть/развернуть/восстановить, закрытие, события открытия/закрытия и изменения состояния.
- Результат: 64/64 tests green либо `Inconclusive` по environment-зависимым приборам (`GlazeShell.Core.Tests` 32/32, `GlazeShell.Windows.Tests` 32/32: 31 passed, 1 environment-tolerant), Debug и Release build — 0 warnings / 0 errors.

- Создана Stage 0 Foundation: solution, слоистые проекты и минимальный WinUI 3 executable.
- Добавлены фиксированные настройки .NET SDK и NuGet package versions.
- Добавлена базовая конфигурация `schemaVersion: 1`.
- Добавлено централизованное JSON-lines файловое логирование.
- Добавлен MSTest test project и smoke test foundation.
- Добавлена обязательная архитектурная, security, performance и development документация.
- Инициализирован локальный Git repository.
- Добавлены доменные модели Core: applications, categories, desktop items, tabs, layout, settings, windows, monitors и themes.
- Добавлена единая валидация входных данных моделей через `ModelValidation`.
- Добавлены контракты интерфейсов `IApplicationManager`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IThemeManager`, `IEventManager` и `IMonitorManager`.
- Добавлены типизированные события оболочки с общим базовым `GlazeEvent`.
- Добавлен `EventManager` с disposable-подписками и изоляцией ошибок подписчиков.
- Добавлен `InMemorySettingsManager` с публикацией `SettingsChanged` только при фактическом изменении значений.
- Добавлены тесты моделей, `EventManager` и `InMemorySettingsManager`.
- Окно приложению заданы явный размер и центрирование через `AppWindow`; ранее размер определялся поведением XAML по умолчанию.
- Пустой `Grid` в `MainWindow` заменён стартовым экраном со статусом этапов.
- `Platform` для App-проекта принудительно устанавливается в `x64`, поэтому `dotnet run` работает без указания платформы.
- Добавлен `LogRedactor`: маскирование secrets (`password`, `token`, `api_key`, `authorization`, `Bearer` и другие) и имён пользователей в путях, плюс ограничение длины записи.
- Файловое логирование ограничено по размеру (1 MiB) с ротацией в три архива вместо неограниченного роста.
- `UserDataPaths` отклоняет зарезервированные имена устройств Windows, слишком длинные имена и имена с завершающей точкой или пробелом.
- `app.manifest` дополнен `longPathAware`, `SegmentHeap` и GUID совместимости с Windows 8.1.
- `.gitignore` дополнен шаблонами файлов с секретами.
- `docs/SECURITY.md` переписан: модель угроз, инвентарь данных и перечень принятых рисков.

### Known limitations

- Persistence layer и migrations ещё не реализованы.
- Desktop layout ещё не привязан к мониторам: `DesktopManager` оперирует in-memory состоянием, а выбранная раскладка не сохраняется между запусками. Привязка к мониторам запланирована на Stage 7 вместе с persistence.
- UI-панель вкладок и категорий не подключена: контракт `IDesktopManager` полный, но визуальное дерево не изменялось — его подключает владелец проекта.
- AppsFolder MSIX discovery на текущей машине ограничен `0x800401E5`; на штатных системах возвращает список пакетов.
- Окна MSIX-приложений не привязываются к карточкам: у MSIX `ExecutablePath` не заполнен, и его окна не отображаются в инлайн-панели.
- Core Stage 2/3/4 подключены к UI; настройки пока не сохраняются между запусками.
- Installer и release packaging ещё не подготовлены.
- Шифрование at rest не применяется: приложение пока не хранит секреты.
