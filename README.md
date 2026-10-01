# Glaze Shell

Glaze Shell — Windows-приложение для глубокой персонализации рабочего окружения поверх штатных возможностей Windows. Проект не изменяет системные файлы и не заменяет компоненты Windows.

## Текущий статус

Stage 0 — Foundation завершён. Создана минимальная запускаемая WinUI 3 оболочка, слоистая структура .NET-решения, базовая конфигурация, централизованное файловое логирование и тестовый проект.

Stage 1 — Core завершён. Реализованы доменные модели, контракты интерфейсов, события и базовые framework-free сервисы `GlazeShell.Core`.

Stage 2 — Application Discovery завершён. Реализовано обнаружение приложений из меню «Пуск» (`.lnk`) и пакетов MSIX/AppsFolder через официальные Windows API.

Stage 3 — Application Launcher завершён. Реализованы запуск, проверка состояния и закрытие приложений (Win32 + MSIX), а также простой UI лаунчера с поиском и горячими клавишами.

Stage 4 — Window Manager завершён. Реализовано перечисление видимых top-level окон, события окон (открытие, закрытие, foreground, изменение состояния), фокус, свернуть/развернуть/восстановить через `ShowWindowAsync` и закрытие через `WM_CLOSE`; в карточках запущенных приложений отображается инлайн-панель их окон.

Stage 5 — Desktop Integration завершён. Реализованы мониторы и DPI (перечисление, resolution, refresh rate, ориентация, scale factor, primary), display events через `WM_DISPLAYCHANGE`, desktop layout (вкладки и элементы) через `DesktopManager`; в футере отображается панель «Мониторы» с обновлением по событиям.

Stage 6 — Tabs and Categories завершён. Реализован полный backend рабочего пространства: вкладки (создание, удаление, переименование, перемещение, активация), категории (создание, удаление, переименование, перемещение) и размещение приложений (добавление, удаление, перенос между категориями) с единым каноническим порядком `Order` на трёх уровнях layout. `DesktopTab.IsActive` удалён как дублирующее поле — единственный источник активной вкладки `DesktopLayout.ActiveTabId`.

Stage 7 — Persistence завершён. Реализовано сохранение пользовательских данных в JSON (`config.json`, `settings.json`, `layout.json`): версия схемы в каждом документе, цепочка миграций с резервной копией перед перезаписью, атомарная запись, изоляция повреждённых документов в `recovery` и неприкосновенность документов из будущих версий приложения. Layout восстанавливается между запусками вместе с привязкой вкладок к мониторам: привязки к недоступным мониторам снимаются, а результат сверки сразу сохраняется. Автосохранение layout выполняет `DesktopLayoutPersistenceWriter`, объединяя частые изменения в одну запись.

Stage 8 — Theme System завершён. Реализован backend тем: `ThemeManager` хранит набор тем, выбирает действующую тему (по `UserSettings.ActiveThemeId`) и публикует `ThemeChanged`; `ThemeStore` читает пользовательские темы из каталога `themes` в каталоге данных приложения. Тема содержит только данные оформления (`metadata`, `colors`, `fonts`, `dimensions`, `icons`, `wallpaper`, `effects`): исполняемые ассеты, пути вне каталога темы и нечисловые выражения цвета отклоняются, непригодная тема отбрасывается по отдельности и не портит остальные, а при отсутствии тем применяется встроенная тема `glaze-default`. Решение о том, как отображать значения темы, остаётся за UI.

Stage 9 — Windows Events завершён. Источники системных событий запускаются из одной точки (`ShellEventCoordinator`): оконные события (`SetWinEventHook`), события дисплеев (hidden message window) и события процессов (`ProcessEventMonitor`). Отказ отдельного источника не прерывает запуск остальных — диагностика попадает в `ShellEventStatus.Diagnostics` и лог, а `Start()` возвращает флаг по каждому источнику. Состояние запущенных приложений обновляется по событиям `ProcessStarted`/`ProcessExited` вместо опроса каждые 3 секунды, а пачка событий одного запуска объединяется вместо пересчёта на каждое событие.

## Возможности

Реализовано на текущем этапе:

- обнаружение приложений меню «Пуск»: managed-парсер бинарных shortcut-файлов (MS-SHLLINK) с fallback на `IShellLinkW` и shell property store;
- обнаружение MSIX/Store-приложений через AppsFolder (`IShellItemArray`, `PKEY_AppUserModelID`);
- агрегация кандидатов, dedup по launch identity, предупреждения диагностики по каждому источнику;
- запуск: MSIX через `IApplicationActivationManager`, Win32 через `ProcessStartInfo`;
- проверка состояния и закрытие через `CloseMainWindow` (без принудительного kill); MSIX-пакет определяется по каталогу установки, извлечённому из пути процесса (`WindowsApps\<PackageFullName>`);
- UI лаунчера: поиск, список с virtualization, статус выполнения, клавиатурная навигация (↑/↓, Enter, Space, F5, Esc), обновление по событиям процессов вместо периодического опроса фоновых процессов;
- window manager: перечисление видимых top-level окон, события `WindowOpened`/`WindowClosed`/`ForegroundWindowChanged`/`WindowStateChanged` через `SetWinEventHook`, фокус с best-effort и fallback через `AttachThreadInput`, `ShowWindowAsync` (свернуть/развернуть/восстановить) и вежливое закрытие через `WM_CLOSE`;
- инлайн-панель окон в карточке запущенного приложения: заголовок, состояние, активное окно и кнопки «Фокус/Свернуть/Развернуть/Закрыть»; Win32-окна сопоставляются по пути исполняемого файла, MSIX — по имени семейства пакета;
- фильтрация окон: только видимые, не cloak-нутые (DWM `DWMWA_CLOAKED`) и не tool-windows;
- monitor/DPI integration: перечисление мониторов (`EnumDisplayMonitors`), bounds и working area, primary-флаг, scale factor (`GetDpiForMonitor`/`GetDpiForSystem`), refresh rate и ориентация (`EnumDisplaySettingsW`), `MonitorFromWindow`/`MonitorFromPoint`;
- display events: уведомление об изменении конфигурации дисплеев через hidden message window (`WM_DISPLAYCHANGE`, `WM_DEVICECHANGE`/`DBT_DEVNODES_CHANGED`, `WM_SETTINGCHANGE`/`SPI_SETWORKAREA` и `SPI_SETLOGICALDPIOVERRIDE`, `WM_DPICHANGED`) и публикация `DisplayChanged`; отбор сообщений вынесен в `DisplaySignal`, а пачка сигналов объединяется `EventCoalescer` (300 мс) вместо перечисления мониторов на каждое сообщение;
- windows events: единый `ShellEventCoordinator` запускает оконные, дисплейные и процессовые источники и владеет их жизненным циклом; отказ одного источника не блокирует остальные, диагностика возвращается в `ShellEventStatus` и пишется в лог;
- process events: `ProcessEventMonitor` публикует `ProcessStarted`/`ProcessExited` по разнице снимков списка процессов (интервал по умолчанию 1 с); первый снимок базовый, имя и путь читаются только для новых PID, а при отсутствии подписчиков перечисление не выполняется вовсе;
- desktop layout: полный backend рабочего пространства через `DesktopManager` — вкладки (`CreateTab`/`RemoveTab`/`RenameTab`/`MoveTab`/`ActivateTab`), категории (`CreateCategory`/`RemoveCategory`/`RenameCategory`/`MoveCategory`) и приложения (`AddApplication`/`RemoveApplication`/`MoveApplication`, в том числе перенос между категориями); событие `DesktopChanged`; канонический `Order` нормализуется на каждом уровне, layout нормализуется при установке;
- UI-панель вкладок и категорий не подключена: подключение визуального дерева выполняет владелец проекта.
- панель «Мониторы» в футере UI со сводкой, списком устройств (разрешение, DPI, refresh rate, ориентация) и автообновлением по display events;
- persistence: JSON-документы пользовательских данных (`config.json`, `settings.json`, `layout.json`) с версией схемы, цепочкой миграций, атомарной записью, резервными копиями и изоляцией повреждённых документов; лимиты размера и глубины разбора; пути берутся только из кода;
- привязка вкладок к мониторам: `DesktopTab.MonitorId`, `IDesktopManager.AssignTabToMonitor`, снятие привязок к недоступным мониторам при загрузке layout;
- theme system: `ThemeManager` (набор тем, выбор действующей темы, событие `ThemeChanged`), встроенная тема `glaze-default`, загрузка пользовательских тем из `themes/*.json` с проверкой цветов, размеров и безопасности ассетов; тема не может содержать исполняемый код или ссылаться на файлы вне своего каталога;
- UI-применение темы не подключено: значения темы отдаются через `IThemeManager`, а визуальное дерево и resources — зона владельца проекта;
- файловое логирование в `%LOCALAPPDATA%\GlazeShell\logs`.

Планируемые возможности:

- кастомизация рабочего стола;
- автозапуск приложения при входе в систему;
- будущая система виджетов и расширений.

## Roadmap

1. Foundation — структура решения, сборка, логирование и базовая конфигурация. ✅
2. Core — доменные модели, интерфейсы и события. ✅
3. Application Discovery — обнаружение установленных приложений. ✅
4. Application Launcher — запуск, проверка состояния и закрытие приложений. ✅
5. Window Manager — управление окнами и события окон. ✅
6. Desktop Integration — мониторы, DPI и display configuration. ✅
7. Tabs and Categories — вкладки, категории, размещение приложений и порядок элементов. ✅
8. Persistence — JSON, layout, settings, schema migrations и recovery. Включает привязку desktop layout к мониторам. ✅
9. Theme System — безопасная загрузка данных тем без исполняемого кода. ✅
10. Windows Events — централизованная event-driven модель. ✅
11. Application Lifecycle — startup, shutdown и single-instance.
12. Performance — profiling и оптимизация фоновых ресурсов.
13. Security — security review и hardening boundaries.
14. Testing и Release — расширенное покрытие и первый релиз.

## Требования

- Windows 10 версии 1809 или новее;
- .NET SDK 10.0.401 или совместимый SDK, разрешённый `global.json`;
- Windows App SDK 2.5.1;
- Microsoft.Windows.SDK.BuildTools 10.0.26100.7705;
- Visual Studio с Windows App SDK tooling рекомендуется для полноценной разработки и отладки.

Проект собирается в режиме unpackaged. На текущем этапе используется self-contained Windows App SDK; release-конфигурация будет оптимизирована отдельно.

## Установка и запуск

```text
git clone <repository-url>
cd "Glaze Shell"
dotnet restore
dotnet build
dotnet test
dotnet run --project src/GlazeShell.App/GlazeShell.App.csproj
```

Команды PowerShell:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project .\src\GlazeShell.App\GlazeShell.App.csproj
```

При первом запуске создаётся каталог пользовательских данных в `%LOCALAPPDATA%\GlazeShell`: в нём появляются `config.json`, `settings.json`, `layout.json` и каталог логов. Имя каталога данных берётся из `config.json`, поэтому путь к данным задаётся пользователем без правки кода.

## Документация

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — слои, dependency flow, решения по этапам, использованные Windows API и производительность.
- [docs/SECURITY.md](docs/SECURITY.md) — модель угроз, границы доверия и принятые риски.
- [CHANGELOG.md](CHANGELOG.md) — история изменений.

## Разработка

### Требования

- Windows 10 версии 1809 или новее;
- .NET SDK, указанный в `global.json`;
- NuGet access для restore;
- для полноценной WinUI 3 разработки: Visual Studio с Windows App SDK tooling и Windows 10 SDK;
- Git.

Приложение собирается в режиме unpackaged и использует self-contained Windows App SDK; при изменении deployment strategy нужно проверить размер, startup и runtime requirements.

### Сборка и тесты

```powershell
dotnet build --configuration Debug
dotnet build --configuration Release
dotnet test
dotnet test --configuration Release
```

Тестовые проекты:

- `tests/GlazeShell.Core.Tests` — модели, `EventManager`, `InMemorySettingsManager`, discovery service, `DesktopManager`, `MonitorLayoutBinding`, `UserDataDirectoryName`, `ThemeManager`.
- `tests/GlazeShell.Data.Tests` — `JsonUserDataStoreTests`, `SchemaMigrationRunnerTests`, `PersistenceServicesTests`, `ThemeStoreTests`; каждый тест работает в собственном временном каталоге (`TempUserData`), который удаляется после теста.
- `tests/GlazeShell.Windows.Tests` — `ShellLinkResolverTests`, `StartMenuShortcutSourceTests`, `AppsFolderSourceTests`, `WindowsApplicationLauncherTests`, `WindowManagerTests`, `MonitorManagerTests`.

`Windows.Tests` использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо COM `IShellLinkW.Save`, который в текущем окружении возвращает `0x80070002`.

Текущий статус: 274 tests (`Core.Tests` 146/146, `Data.Tests` 64/64, `Windows.Tests` 63/64) в Debug и Release; build — 0 warnings / 0 errors. Единственный провал — environment-tolerant flake `WindowManagerTests.RaisesWindowOpenedAndClosedEvents` (см. ниже): при его прохождении — 274/274.

Остальные environment-tolerant тесты не падают из-за недоступных API, а завершаются `Inconclusive` с объясняющим warning: `AppsFolderSourceTests` и `MonitorManagerTests`. `WindowManagerTests.RaisesWindowOpenedAndClosedEvents` зависит от доставки `EVENT_OBJECT_DESTROY` для собственного процесса и потому чувствителен к параллелизму прогонов: таймаут ожидания события истекает, когда тестовые сборки запускаются одновременно. Поведение воспроизводится и на commit до Stage 7, то есть не связано с persistence или темами; изолированный запуск `GlazeShell.Windows.Tests` проходит стабильно.

### Запуск и данные приложения

App project собирается только под `x64` (значение `AnyCPU` в `.csproj` заменяется автоматически); при передаче платформы явно используйте `--arch x64`.

Приложение использует `%LOCALAPPDATA%\GlazeShell\logs\glaze-shell.log` для startup log и `%LOCALAPPDATA%\GlazeShell` как каталог пользовательских данных: `config.json`, `settings.json`, `layout.json`, каталоги `backups`, `recovery` и `themes`. Имя каталога данных задаётся полем `dataDirectoryName` в `config.json`; при его смене лог текущей сессии остаётся в исходном каталоге.

### Отладка и правила кода

- Для XAML и App lifecycle используйте Visual Studio с Windows App SDK tooling; для диагностики startup проверьте лог и Output window.
- Не изменяйте generated `bin` и `obj` вручную.
- При добавлении P/Invoke сначала проверьте ownership и lifetime native handles; сам P/Invoke размещается только в `GlazeShell.Windows`.
- Core-модели и события добавляются в `src/GlazeShell.Core`; не помещайте туда UI, Win32 или инфраструктурные зависимости.
- Новые сервисы Core возвращают `IDisposable`, если удерживают подписки или ресурсы.
- Package versions задаются centrally в `Directory.Packages.props`; floating versions не добавляются. Перед добавлением dependency фиксируются назначение, лицензия, размер, влияние на startup и альтернатива BCL/Windows API. MSTest используется только для автоматического тестирования.

### Git workflow

1. Проверить `git status` и текущую ветку.
2. Изучить существующие изменения и документацию.
3. Реализовать одну логически завершённую задачу.
4. Добавить тесты и обновить документацию.
5. Выполнить build и tests.
6. Проверить diff.
7. Создать отдельный commit в стиле проекта.

Сообщения коммитов пишутся на русском языке в свободной форме — это фактический стиль истории проекта (см. `git log`). Release workflow появится на Stage 14; до его утверждения нельзя публиковать installer или собранные binaries как релиз.

## Known issues

- Visual Studio/WinUI 3 template не установлен в текущем окружении; App project создан вручную.
- В текущей среде `CLSID_ShellLink` зарегистрирован не в `shell32.dll`, поэтому `IShellLinkW.Load` реальных `.lnk` возвращает `0x00000001`, а `Save` — `0x80070002`. Классические `.lnk` резолвятся managed-парсером `ShellLinkData` (LinkInfo/relative path); через property store и `IShellLinkW` — в зависимости от здоровья среды.
- `IShellItemArray` AppsFolder в текущей среде возвращает `0x800401E5`, поэтому MSIX-источник может сообщить warning вместо списка приложений. Это environment-specific limitation и не считается ошибкой продукта.

- Окна MSIX-приложений сопоставляются с карточками по имени семейства пакета, извлечённому из пути процесса окна (`WindowsApps\<PackageFullName>`); каталог установки из реестра для bundle-пакетов указывает на `neutral`-вариант и для этой цели не используется. Побочный эффект: пакет с фоновым сервисом без окна показывается запущенным, пока сервис жив.
- Фокусировка окна — best-effort: при отказе `SetForegroundWindow` (foreground lock) используется fallback через `AttachThreadInput`; в окружениях с жёстким foreground lock команда может не сработать.
- Используется системный title bar, поэтому его оформление не следует тёмной палитре контента; кастомный title bar запланирован вместе с Theme System.
- Лицензия проекта ещё не выбрана владельцем проекта; файл `LICENSE` не предоставляет юридических прав до утверждения лицензии.
