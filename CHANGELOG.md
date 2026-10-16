# Changelog

Все значимые изменения Glaze Shell будут записываться в этом файле.

## [Unreleased]

### Added

- **MVP достигнут (Stage 0–13).** Инженерный фундамент оболочки завершён: слоистое ядро, discovery, launcher, window manager, мониторы/DPI, вкладки и layout, persistence, темы, событийная модель, lifecycle (single-instance и корректный shutdown), производительность и security hardening. В README добавлен раздел «Как пользоваться приложением» (поиск и обновление, карточки и действия, клавиатурная навигация, окна, панель «Мониторы», один экземпляр, данные и логи, ограничения текущего UI) и статус MVP в шапке; в продуктовом roadmap уточнено, что MVP относится к инженерному фундаменту, а продуктовый UI (dock, top bar, Glance и др.) — следующие стадии.
- Документация переструктурирована вокруг MVP, README укорочен с ~530 до ~180 строк за счёт переноса деталей в отдельные файлы: [docs/USAGE.md](docs/USAGE.md) (возможности MVP и инструкция по использованию: поиск, карточки и действия, клавиатура, окна, панель «Мониторов», данные и логи, ограничения текущего UI), [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) (требования, установка, сборка, запуск, скрипт `glaze.cmd`, тестирование, устранение неполадок, правила кода, Git workflow, известные ограничения) и [docs/ROADMAP.md](docs/ROADMAP.md) (технические стадии 0–13, статус по каждой стадии, продуктовый roadmap, терминология MVP). README оставлен короткой витриной: шапка с баннером и бейджем `status: MVP`, описание, быстрый старт, краткие возможности, краткое «Как пользоваться», ссылки на документацию и краткая веха-таблица Roadmap; удалены дублировавшие содержимое разделы «Установка и запуск», «Тестирование», «Разработка» и «Известные ограничения» (перенесены в DEVELOPMENT.md), полные «Возможности» и «Как пользоваться приложением» (перенесены в USAGE.md) и подробная таблица Roadmap (перенесена в ROADMAP.md).
- Продуктовый план: функция G1 (слой клавиатуры в стиле Mac) полностью убрана из спецификации — из README, `docs/FEATURES.md` (индексная таблица, раздел §G1, решение §9, чеклист §10) и `docs/features.json` (`keyboardLayerG1` и объект `G1`); число функций сокращено с 53 до 52. Заодно удалён дублировавшийся блок «Продуктовый roadmap» в README.
- Дизайн-система и спецификация возможностей: добавлены [docs/DESIGN.md](docs/DESIGN.md) (порт дизайн-системы Glaze Shell: цвета, материалы, тени, типографика, компоненты с размерами, движение, do/don’t) и [docs/FEATURES.md](docs/FEATURES.md) (спецификация 52 функций в стиле macOS для Windows с приоритетами/фазами, критериями готовности, Windows reality check, принятыми продуктовыми решениями). Машинный индекс — [docs/features.json](docs/features.json). Перенесены токены, иконки и макеты в `docs/design/`, в `docs/assets` добавлены `app.ico` и `logo-mark.svg`. В README добавлен раздел «Дизайн-система» и обновлена структура проекта; в ARCHITECTURE.md добавлены ссылки на дизайн-систему.
- Иконка приложения и визуальные ресурсы: добавлена папка `src/GlazeShell.App/Assets` с `app.ico` (multi-size 16/32/48/64/128/256), которая подключается в `GlazeShell.App.csproj` через `<ApplicationIcon Condition="Exists('Assets\app.ico')">` и попадает в ресурсы `GlazeShell.exe`; логотипы в двух темах (`logo-dark.png`, `logo-light.png`).
- Ресурсы для README/GitHub в `docs/assets`: баннер `banner-dark/light.png` (1280×640, шапка README и Social preview), логотипы `logo-dark/light.png` и квадратный `logo-tile.png`, а также `logo-mark.svg` и `app.ico`. В шапку README добавлен баннер через `<picture>` с `prefers-color-scheme`, поэтому GitHub показывает вариант под тему браузера пользователя.
- Отклик, навигация и выделение в UI: результат обновления теперь заметен (в статусе появляется отметка «обновлено чч:мм:сс», а кнопка «Обновить» блокируется, пока идёт сканирование), стрелки клавиатуры подносят выбранный элемент к видимой области (`ListView.ScrollIntoView`), а выбранная карточка получает более яркий фон (перекрытие кистей `ListViewItemBackground*`) и акцентную полосу слева.
- Анимация запуска приложения: пока выполняется `LaunchAsync`, значок состояния на карточке сменяется круговым индикатором (`ProgressRing`), что делает запуск видимым без изменения остального интерфейса.
- Принудительное завершение приложения (`IApplicationLauncher.TerminateAsync`, кнопка «Завершить принудительно» с диалогом подтверждения): завершает дерево процессов через `Process.Kill(entireProcessTree: true)` и добивает фоновые процессы из каталога установки. Расширение по каталогу ограничено несистемными каталогами (корень диска и `%WINDIR%` исключены). `RestartAsync` теперь прибегает к принудительному завершению как к fallback, когда вежливое закрытие не удалось, — иначе перезапуск поднимал второй экземпляр рядом с незакрытым первым.
- Связь окон с мониторами: `WindowInfo.MonitorId` заполняется через `MonitorFromWindow`; панель окон показывает монитор окна, панель мониторов — окна на каждом мониторе и схематичную карту (`MonitorMapLayout`, Core, с тестами `MonitorMapLayoutTests`).
- Добавлен регрессионный тест `ResolvesRealWindowsShortcutWithAnsiLinkInfo` и фикстура `Fixtures/real-linkinfo-ansi.lnk` (реальный ярлык Windows с ANSI `LinkInfo`) — проверка разбора `.lnk` на настоящем формате, а не только на builder-фикстуре. Managed-writer `ShellLinkBuilder` теперь пишет `LinkInfo` в реальном формате (ANSI `LocalBasePath`), благодаря чему тесты больше не маскируют ошибку декодирования; удалён временный пробник `TempRealShortcutProbe`.
- Копирование пути приложения из списка: на карточке появилась кнопка «Копировать», которая кладёт в буфер обмена полный путь к исполняемому файлу (для MSIX — AUMID или имя семейства пакета). Сам путь в карточке показывается сокращённо в виде «корень\первый каталог\…\последний каталог\файл» (например, `C:\Program Files\…\Acrobat\Acrobat.exe`): начало и конец пути видно, длинная середина скрыта. Пути с двумя каталогами и короче показываются целиком, поэтому длинная строка не занимает весь ряд, а копируется всё равно полный путь.
- Двухколоночный макет: список приложений и панель «Мониторы» теперь стоят рядом — приложения в широкой левой колонке, мониторы в фиксированной правой (~360px). Поиск и кнопка «Обновить» переехали над списком, строка состояния осталась внизу во всю ширину. Панель мониторов получила собственный скролл на всю высоту колонки. Начальный размер окна увеличен (1120×720 → 1280×760, минимум 960×520), чтобы колонки не сжимались.
- Приложения-лаунчеры (Squirrel и подобные) теперь определяются как запущенные и корректно закрываются. Приложение могло быть запущено через лаунчер из ярлыка (например, Discord: `Discord.exe --processStart Discord.exe`), который поднимает настоящий исполняемый файл из подкаталога каталога установки. Сопоставление процессов и окон вынесено в framework-free `ApplicationProcessMatcher` (Core): кроме точного пути учитывается каталог установки с ограничением по имени образа, а имя из аргумента `--processStart` добавляется в набор ожидаемых. Ограничение по имени образа не даёт зацепить чужие процессы из общего каталога (например, `git-bash.exe` у `git-cmd.exe`), а проверка безопасного каталога исключает системные процессы. Добавлены `ProcessInspector.FindProcessesByImageInDirectory`, тесты `ApplicationProcessMatcherTests` и регрессионный тест `IsRunningDetectsLauncherSpawnedProcessFromSubdirectory`.
- Описания приложений в карточке ограничены по длине (`BuildDescription`): описание из ярлыка или имя издателя обрезаются до 80 символов троеточием в конце, чтобы длинная строка не растягивала карточку.
- Добавлен скрипт быстрого запуска `glaze.cmd` в корне репозитория: собирает приложение и запускает собранный exe отдельным процессом (консоль закрывается). Конфигурация по умолчанию — `Debug`, можно передать `Release` первым аргументом (`glaze.cmd Release`).
- Пользовательские имена дисплеев: любой монитор можно переименовать из панели «Мониторы» (диалог «Сохранить»/«Сбросить»/«Отмена»), имя сохраняется в `settings.json` как карта `monitorNames` (идентификатор монитора → имя) и переживает перезапуск. По умолчанию дисплеи называются «MAIN DISPLAY» и «DISPLAY N» (слева направо и сверху вниз), а пустое имя возвращает дисплей к имени по умолчанию. Карта нормализуется без учёта регистра, обрезается по краям, ограничена 64 записями (`PersistenceLimits.MaxMonitorNames`), а идентификаторы и имена проверяются тем же валидатором, что и остальные документы. Логика хранения покрыта `UserSettingsTests` (Core) и тестами round-trip, лимита и валидации в `JsonUserDataStoreTests` (Data).
- Устойчивое завершение приложения: закрытие окна освобождает ресурсы по шагам (`App.Shutdown`), и сбой одной очистки (layout-writer, источники событий, single-instance lease) логируется как warning, не мешая остальным шагам и не удерживая процесс в памяти.

#### Stage 0 — Foundation

- Создана Stage 0 Foundation: solution, слоистые проекты и минимальный WinUI 3 executable.
- Добавлены фиксированные настройки .NET SDK и NuGet package versions.
- Добавлена базовая конфигурация `schemaVersion: 1`.
- Добавлено централизованное JSON-lines файловое логирование.
- Добавлен MSTest test project и smoke test foundation.
- Добавлена обязательная архитектурная, security, performance и development документация.
- Инициализирован локальный Git repository.
- Окно приложению заданы явный размер и центрирование через `AppWindow`; ранее размер определялся поведением XAML по умолчанию.
- Пустой `Grid` в `MainWindow` заменён стартовым экраном со статусом этапов.
- `Platform` для App-проекта принудительно устанавливается в `x64`, поэтому `dotnet run` работает без указания платформы.
- Добавлен `LogRedactor`: маскирование secrets (`password`, `token`, `api_key`, `authorization`, `Bearer` и другие) и имён пользователей в путях, плюс ограничение длины записи.
- Файловое логирование ограничено по размеру (1 MiB) с ротацией в три архива вместо неограниченного роста.
- `UserDataPaths` отклоняет зарезервированные имена устройств Windows, слишком длинные имена и имена с завершающей точкой или пробелом.
- `app.manifest` дополнен `longPathAware`, `SegmentHeap` и GUID совместимости с Windows 8.1.
- `.gitignore` дополнен шаблонами файлов с секретами.
- `docs/SECURITY.md` переписан: модель угроз, инвентарь данных и перечень принятых рисков.

#### Stage 1 — Core

- Добавлены доменные модели Core: applications, categories, desktop items, tabs, layout, settings, windows, monitors и themes.
- Добавлена единая валидация входных данных моделей через `ModelValidation`.
- Добавлены контракты интерфейсов `IApplicationManager`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IThemeManager`, `IEventManager` и `IMonitorManager`.
- Добавлены типизированные события оболочки с общим базовым `GlazeEvent`.
- Добавлен `EventManager` с disposable-подписками и изоляцией ошибок подписчиков.
- Добавлен `InMemorySettingsManager` с публикацией `SettingsChanged` только при фактическом изменении значений.
- Добавлены тесты моделей, `EventManager` и `InMemorySettingsManager`.

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

##### Changed

- `IDesktopManager`: `AddItem`/`RemoveItem`/`MoveItem` переименованы в `AddApplication`/`RemoveApplication`/`MoveApplication` согласно плану §13. Ломающее изменение контракта; прямых потребителей кроме `DesktopManager` и тестов не было.
- `IDesktopManager` дополнен `RenameTab`, `MoveTab`, `CreateCategory`, `RemoveCategory`, `RenameCategory`, `MoveCategory` согласно плану §12/§13.
- `CreateTab`/`CreateCategory` изменили сигнатуру: `int order = 0` заменён на `int? order = null` (append). Прежнее значение по умолчанию вставляло каждый новый объект в начало коллекции.

##### Removed

- Удалено `DesktopTab.IsActive`: поле дублировало `DesktopLayout.ActiveTabId` и никогда не поддерживалось `DesktopManager`, из-за чего всегда оставалось `false`. Единственный источник истины — `DesktopLayout.ActiveTabId`.

##### Documentation

- `README.md`: добавлен статус Stage 6, обновлено описание desktop layout и known issues; в roadmap отмечено выполнение вкладок/категорий, привязка layout к мониторам отнесена к Stage 7.
- `ARCHITECTURE.md`: добавлен раздел «Решения Stage 6», обновлено описание `DesktopManager`; привязка layout к мониторам перенесена из Stage 6 в Stage 7 вместе с persistence.
- XAML не изменялся: Stage 6 добавляет только backend-контракт, подключение UI выполняет владелец проекта.

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

##### Fixed

- Исправлена двойная загрузка конфигурации при старте: при смене `DataDirectoryName` второй load из нового каталога создавал там документ по умолчанию и сбрасывал имя приложения.
- Исправлена атомарная запись: `FileStream.Flush(flushToDisk: true)` вызывался после `Dispose` писателя, из-за чего любая запись завершалась `ObjectDisposedException` и документ не сохранялся.
- Снятые привязки к мониторам теперь сохраняются, иначе они вычислялись бы заново при каждом запуске.

##### Changed

- `MonitorLayoutBinding.Reconcile` при пустом или недоступном списке мониторов возвращает layout без изменений: при отсутствии достоверных сведений привязки не снимаются.
- `IsValidIdentifier` отклоняет управляющие символы, как и `IsValidName`: идентификаторы попадают в диагностику и логи.
- `GlazeShell.Data` подключён к `GlazeShell.Core`; `GlazeShell.App` подключён к `GlazeShell.Data`.

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

#### Stage 11 — Performance

- Добавлены framework-free модели диагностики в `GlazeShell.Core/Diagnostics`: `PerformanceSnapshot` (working set, private memory, managed heap, thread/handle counts, total processor time, uptime, label, timestamp) с валидацией и инвариантным форматированием в лог; `PerformanceSession` с `PerformanceSessionSummary` и статическим `Summarize(start,end,processorCount)` — вычисляет elapsed, средний CPU% с нормировкой на число ядер и ограничением 100%, дельты памяти/потоков/handles.
- `EventLatency` (`GlazeShell.Core/Diagnostics/EventLatency.cs`): `EventLatencyStats` (count, total, min, max, average) и потокобезопасный `EventLatencyRecorder` (`Record`, `Snapshot`, `Reset`) для агрегирования задержек синхронной доставки событий.
- Добавлена инфраструктура диагностики в `GlazeShell.Infrastructure/Diagnostics`: `ProcessMetricsCollector.Capture(label)` собирает метрики текущего процесса через `System.Diagnostics.Process` (GC.ManagedHeapBytes берётся из `GC.GetTotalMemory(false)`); при сбое отдельных полей значения мягко падают в 0 (за исключением `OutOfMemoryException`/`StackOverflowException`). `PerformanceMonitor` (`IDisposable`) — ведёт baseline, запись снимков, опциональный периодический таймер idle-снимков (`StartSampling(TimeSpan)`) по умолчанию выключенный, логирует baseline, сессионное резюме и end-of-session; таймер одноразовый на тик и освобождается в `Dispose` (идемпотентно).
- Инструментирована синхронная доставка событий: `EventManager` принимает опциональный `EventLatencyRecorder? latency` и измеряет время вызова обработчиков в `Publish<TEvent>` (через `Stopwatch.GetTimestamp()`/`Stopwatch.GetElapsedTime()`), записывая только при наличии и только когда есть подписчики (не измеряет путь без подписчиков). Публикация работает с копией списка подписок, ошибки подписчиков изолируются без влияния на агрегирование латентности.
- `App.xaml.cs` переписан для интеграции профилирования без хранения disposable-полей в `App`: `PerformanceMonitor` и `EventLatencyRecorder` создаются локально в `OnLaunched`, передаются в `EventManager` и захватываются замыканием `window.Closed`. Baseline снимается сразу после `window.Activate()`. При закрытии добавлен шаг «performance summary»: логирует `Event dispatch latency: ...` и `PerformanceMonitor.LogSession()`, после чего монитор освобождается. Idle-профилирование включается через переменную окружения `GLAZESHELL_PERF_INTERVAL_MS` (если положительное целое число); включение логируется. Изменение также убирает CA1001 (нет disposable-полей у `App`).
- Добавлены тесты Stage 11 (`GlazeShell.Core.Tests`): `PerformanceSessionTests` (дельты, отрицательные дельты, zero elapsed, clamping к 100%, валидация processorCount, ToLogString), `EventLatencyRecorderTests` (empty, min/max/avg, reset, concurrent records), `ProcessMetricsCollectorTests` (читает метрики текущего процесса, валидирует label), `PerformanceMonitorTests` (baseline/session logging, без baseline не пишет ничего, проверка интервала, Dispose идемпотентность, после Dispose StartSampling бросает), а также добавлены проверки латентности в `EventManagerTests` (LatencyRecorder captures dispatch; Publication without subscribers is not measured). Повторно запущены все тесты: `GlazeShell.Core.Tests` — 187/187, остальные тестовые проекты без изменений — проходят (общий прогон 284 тестов в контексте предыдущих прогонов; Stage 11 добавил 22 новых теста).

- **Build/Quality:** решение собирается в Debug/Release без предупреждений. Форматирование числовых значений в логах инвариантное (CultureInfo.InvariantCulture), чтобы снимки лога не зависели от региональных настроек. `PerformanceMonitor` соответствует `IDisposable`, `EventLatencyRecorder` не создаёт таймеров/ресурсов. Добавлены необходимые `using System.Globalization` в модели диагностики.

#### Stage 12 — Security

- Общий обзор поверхности безопасности с закрытием границ, где проверка формально была, но обходилась. Модель угроз и набор внешних зависимостей не менялись; новых P/Invoke и native-привязок не добавлено.
- Чтение документов и тем переведено на один открытый дескриптор: добавлен `BoundedTextFile` (`GlazeShell.Data/Persistence`) с `TryRead(path, maxBytes, out text, out byteLength)`. Раньше связка `FileInfo.Length` + `File.ReadAllText` открывала файл дважды, и подмена или рост файла между проверкой и чтением обходила лимит. Теперь размер проверяется и содержимое читается на одном дескрипторе (`FileShare.ReadWrite | FileShare.Delete`, `FileOptions.SequentialScan`, буфер через `ArrayPool`), а число прочитанных байт ограничено длиной, снятой при открытии. `StreamReader` с `detectEncodingFromByteOrderMarks` сохраняет прежнее распознавание UTF-8/UTF-16 по BOM. Используется в `UserDataDirectory.ReadDocument` и `ThemeStore.ReadTheme`.
- `UserDataDirectory.ValidateFileName` приведён к уровню валидации имени каталога: кроме разделителей и `..` отклоняются зарезервированные имена устройств Windows (`CON`, `PRN`, `AUX`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9`, в том числе с расширением) и имена с завершающей точкой или пробелом, которые Windows молча нормализует.
- `LogRedactor` усилен: путь профиля маскируется и при обратном, и при прямом слэше, и в JSON-экранировании (`C:\\Users\\...`); значение секрета маскируется целиком, включая многословные и bracket-значения; список ключей дополнен (`private_key`, `secret_key`, `access_key`, `secretaccesskey`, `session_token`, `id_token`, `client_assertion`, `x-api-key`, `passphrase`, `cookie`, `set-cookie`, `credentials`); значения `Bearer` маскируются и с нестандартными символами; обрезка текста не разрывает суррогатную пару.
- Принудительное завершение по каталогу больше не распространяется на системную и общую область: корень диска, `C:\Windows` и любые его подкаталоги, а также общие корни `C:\Program Files`, `C:\Program Files (x86)` и `C:\ProgramData` исключены. Раньше корень `C:\Windows` не отсекался (проверка «под каталогом» не включала равенство), а `ProcessInspector.FindProcessesByDirectory` ищет рекурсивно, поэтому приложение из этих каталогов могло затронуть процессы всей системы. Подкаталог конкретной программы остаётся безопасным, поэтому расширение для фоновых процессов лаунчеров сохранено.
- Список запрещённых расширений ассетов темы (`ThemeAssetValidation.ForbiddenExtensions`) дополнен ярлыками, установщиками и элементами панели управления: `.url`, `.cpl`, `.pif`, `.scf`, `.chm`, `.cab`, `.msp`, `.msix`, `.appx`, `.psd1`, `.appref-ms`, `.application`.
- `WindowEventMonitor` снимает хуки потоком-владельцем: `Stop()` больше не вызывает `UnhookWinEvent` из потока остановки (это недействительно), а только публикует `WM_QUIT` и дожидается воркера; `UnhookAll` выполняется в `finally` воркера. Список хуков защищён блокировкой (`_hooksSync`), потому что поток запуска читает его для проверки готовности; `_threadId` объявлен `volatile`.
- Добавлены и расширены тесты Stage 12: `LogRedactorTests` (+7), `ThemeManagerTests` (запрет ярлыков, установщиков и control-ассетов), новые `BoundedTextFileTests` (6) и `UserDataDirectoryTests` (8), `WindowsApplicationLauncherTests` (+3: системный каталог, общий корень установки, положительный контроль — подкаталог приложения), новый `WindowEventMonitorTests` (перезапуск после остановки). Стаб `StubProcessInspector` теперь записывает запросы по каталогу и по имени образа.
- Результат: 357/357 tests green (`GlazeShell.Core.Tests` 195/195, `GlazeShell.Data.Tests` 82/82, `GlazeShell.Windows.Tests` 80/80 при изолированном запуске). Debug build — 0 warnings / 0 errors.

#### Stage 13 — Testing и Release

- Расширено покрытие доменной логики в `GlazeShell.Core`: новые `ApplicationIdentityTests` (ключи запуска для AUMID, пути и package family name, нормализация путей, детерминированность хэша), `ApplicationDiscoveryOptionsTests` и `ApplicationDiscoveryServiceTests` (кэш discovery, слияние кандидатов, отсев без цели, системные приложения, изоляция отказа источника, лимит предупреждений, повторные обновления без изменений, `ApplicationChanged`/`DiscoveryCompleted`, идемпотентное освобождение, валидация опций), `GlazeEventsTests` (события оболочки).
- Новые тесты в `GlazeShell.Data`: `PersistenceLimitsTests`, `LayoutDocumentMapperTests`, `SettingsDocumentMapperTests`, `ConfigurationDocumentMapperTests` (уникальность идентификаторов, лимиты, битые ссылки, нормализация значений), `MigrationResultTests`.
- Новые тесты в `GlazeShell.Windows`: `ProcessWatchOptionsTests` (границы интервалов) и `ProcessInspectorTests` (поиск по каталогу и по имени образа для текущего процесса, отрицательные проверки для чужого пути и package family name, валидация аргументов) — environment-tolerant, без зависимости от установленных программ.
- Добавлено версионирование релиза: `Directory.Build.props` теперь задаёт `Version` (`0.1.0`), `AssemblyVersion`, `FileVersion`, `Product`, `Company` и `Copyright` в одной точке для всех проектов решения.
- Добавлен `docs/RELEASE.md` — процесс и чеклист релиза: предварительные условия, обновление версии и документации, сборка Debug/Release, тесты, ручная проверка, diff без секретов, коммит и тег. CI не вводится, публикация бинарников не выполняется: приложение unpackaged, Installer и MSIX отсутствуют.
- Результат: 475/475 tests green (`GlazeShell.Core.Tests` 250/250, `GlazeShell.Data.Tests` 129/129, `GlazeShell.Windows.Tests` 96/96 при изолированном запуске). Debug и Release build — 0 warnings / 0 errors.

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

### Documentation

- Упрощена и синхронизирована документация структуры проекта: разделы «Структура проекта» (README.md) и «Структура» (docs/ARCHITECTURE.md) приведены в соответствие с фактическим деревом — добавлены `glaze.cmd`, каталог `GlazeShell.Windows/SystemEvents`, тестовые подпроекты и локальные (gitignored) `assets-src/` и `AGENTS.md`, устранено расхождение между README и ARCHITECTURE.
- Текст `AGENTS.md` синхронизирован с `.gitignore`: файл локален и в репозиторий не попадает; удалено неактуальное упоминание `AGENT_PROMPT.md`.
- Удалены накопленные каталоги `tests/**/TestResults/` — артефакты локальных прогонов тестов, уже покрытые `.gitignore`.
- Добавлена лицензия MIT (`LICENSE`), раздел «Лицензия» в README обновлён с заглушки «лицензия не выбрана» на ссылку на MIT.

### Fixed

- Точка запуска приложения больше не остаётся светлой после закрытия. Сопоставление события выхода процесса с приложением теперь сверяет имя процесса со всеми образами приложения (включая имя из аргумента `--processStart`), а не только с файлом запуска: у приложений-лаунчеров настоящий процесс носит другое имя, и раньше его завершение не пересчитывало состояние. Кроме того, кнопки «Закрыть» и «Завершить принудительно» теперь берут состояние из фактического опроса (`IsRunningAsync`), а не из флага операции, поэтому точка гаснет, даже если приложение завершилось уже после таймаута ожидания.
- Исправлены константы оконных событий `EVENT_OBJECT_CLOAKED`/`EVENT_OBJECT_UNCLOAKED`: в `User32` они были сдвинуты на единицу (`0x8016`/`0x8017` вместо `0x8017`/`0x8018`), из-за чего диапазон object-хуков не совпадал с реальными событиями; диапазон установки hook приведён к `EVENT_OBJECT_CREATE`…`EVENT_OBJECT_UNCLOAKED`.
- Исправлен заголовок окна в событии закрытия: при чтении уже разрушающегося окна возвращалась заглушка «(безымянное окно)», поэтому `WindowClosed` теперь использует заголовок, запомненный при открытии окна.
- Панель «Мониторы» больше не рисует схематичную карту при единственном мониторе: карта вырождалась в один большой прямоугольник и напрасно занимала место. Карта показывается только при двух и более экранах (`HasMonitorMap`).
- Исправлен разбор строк `LinkInfo` в ярлыках `.lnk` (`ShellLinkData`). По MS-SHLLINK строки `LocalBasePath`/`CommonPathSuffix` лежат на ANSI-офсетах (`0x10`/`0x18`) и всегда закодированы в ANSI, а Unicode-варианты доступны только при `LinkInfoHeaderSize >= 0x24` (офсеты `0x1C`/`0x20`). Прежний код декодировал эти строки как Unicode по флагу `IsUnicode` из `LinkFlags`, поэтому пути реальных ярлыков (у которых `LinkInfoHeaderSize = 0x1C`) превращались в мусор: `TryResolveManaged` формально возвращал неверный непустой путь, fallback на COM и property store не выполнялся, и все ярлыки отбрасывались как неразрешимые. В результате discovery возвращал пустой список приложений. Теперь `CommonPathSuffixOffset` читается по правильному офсету (`0x18` вместо `0x14`), `LocalBasePath` и `CommonPathSuffix` объединяются, а ANSI-строки декодируются в кодировке системы (`CultureInfo.CurrentCulture.TextInfo.ANSICodePage` через `CodePagesEncodingProvider`, с fallback на Latin1).
- Исправлено ложное «Не удалось закрыть» при закрытии приложения. Прежний `CloseAsync` возвращал результат `CloseMainWindow`, который для части приложений (например, Acrobat) возвращает `false`, хотя окно закрывается, и не ждал завершения процесса. Теперь после вежливого запроса (`CloseMainWindow` и, если главное окно не найдено, `WM_CLOSE` по всем top-level окнам процесса) метод дожидается фактического завершения процесса (до `CloseTimeoutMilliseconds`) и сообщает «закрыто» только когда приложение действительно завершилось. Принудительное завершение (`Kill`/`TerminateProcess`) по-прежнему не применяется.
- Предупреждения discovery (неразрешённые ярлыки, отказ AppsFolder) теперь пишутся в лог уровнем warning через подписку `App.xaml.cs` на `DiscoveryCompleted`, а не теряются.
- Исправлено определение состояния MSIX-приложений. Прежний `PackageInstallLocationResolver` читал реестр `AppxAllUserStore\Applications`, ожидая ключи вида `PackageFamilyName!AppId` с вложенным ключом `Package`, тогда как в актуальной схеме ключ называется полным именем пакета (`Name_Version_Architecture_PublisherId`), а значение `Path` лежит прямо в ключе. Из-за этого индекс всегда оставался пустым, `ResolveInstallLocation` возвращал `null`, и все MSIX-приложения (на тестовой машине — 126 из 152) безошибочно числились незапущенными.
- Каталог установки из реестра для bundle-пакетов указывает на `neutral`-вариант, тогда как на диске установлен вариант конкретной архитектуры (на тестовой машине — 44 из 52 пакетов). Поэтому MSIX-пакеты теперь определяются разбором пути процесса `WindowsApps\<PackageFullName>` через `PackageIdentity`, а не по значению из реестра. `PackageInstallLocationResolver` и контракт `IPackageLocationResolver` удалены как неиспользуемые и вводящие в заблуждение.
- `MainViewModel` больше не требует `ExecutablePath` для сопоставления процессов и окон: MSIX-приложения без пути к исполняемому файлу сопоставляются по имени семейства пакета. Раньше события `ProcessStarted`/`ProcessExited` и перечисление окон вообще не касались MSIX-приложений, поэтому их состояние и список окон не менялись.
- `MonitorManager.Start()` публикует текущую конфигурацию мониторов сразу. Раньше список мониторов оставался пустым до первого системного изменения дисплеев.
- `MainViewModel.RefreshAsync` вызывает `RefreshWindows()` после первичного сканирования: окна, открытые до запуска Glaze Shell, не порождают событий, поэтому список окон не заполнялся.

### Changed

- `MainViewModel` больше не опрашивает состояние запущенных приложений каждые 3 секунды. Состояние обновляется по событиям `ProcessStarted`/`ProcessExited`: приложения сопоставляются по полному пути к исполняемому файлу, при его отсутствии — по имени образа, пересчитываются только затронутые приложения, а пачка событий одного запуска объединяется (`EventCoalescer`, 250 мс). Однократный полный пересчёт остался в `RefreshAsync` и задаёт базовую линию для приложений, запущенных до Glaze Shell.
- `WindowsApplicationLauncher.IsRunningAsync` больше не определяет состояние MSIX-приложения по AUMID собственного процесса оболочки (`Environment.ProcessId`). Состояние пакета определяется по процессам в его каталоге установки; если каталог не разрешён, состояние считается неизвестным, а не «не запущено» в отчёт об остановке. Проверка AUMID потребовала бы COM-вызова для каждого процесса, что несопоставимо дороже проверки по пути.
- Документация сведена к двум файлам, чтобы её было проще читать на GitHub: `docs/ARCHITECTURE.md` (слои, решения по этапам, таблица использованных Windows API, производительность) и `docs/SECURITY.md` (модель угроз и принятые риски). Раздел «Разработка» перенесён в `README.md`; `docs/DEVELOPMENT.md`, `docs/WINDOWS_API.md`, `docs/PERFORMANCE.md` и корневой `ARCHITECTURE.md` удалены. Содержание сохранено полностью, изменены только пути, уровни заголовков и формулировки под фактическое состояние репозитория.
- `docs/ARCHITECTURE.md`: таблица Windows API дополнена реально используемыми API Stage 5/6 (`EnumDisplayMonitors`, `EnumDisplaySettingsW`, `MonitorFromWindow`, `MonitorFromPoint`, `GetDpiForMonitor`, `GetDpiForSystem`, `GetDpiForWindow`, `WM_DISPLAYCHANGE`, `SHGetKnownFolderPath`/`SHGetKnownFolderItem`) и исправлена ссылка на `Shell/IPersistFile.cs`: интерфейс объявлен в `Shell/IShellLinkW.cs`, отдельного файла нет.
- Из структуры репозитория убраны пустые каталоги-заглушки с `.gitkeep`: `assets`, `GlazeShell.Data/Database`, `GlazeShell.Infrastructure/Diagnostics`, `GlazeShell.Infrastructure/Startup`, `GlazeShell.Windows/Windows`, а также устаревшие `.gitkeep` в непустых `GlazeShell.Windows/Interop`, `Shell` и `Win32`. Git не хранит пустые каталоги, поэтому файлы-заглушки только мешали читать дерево репозитория; каталоги появятся вместе с содержимым.
- Документация приведена в порядок перед первым слиянием: `CHANGELOG.md` перестроен по возрастанию стадий (Stage 0→10) под единым разделом `Added` без дублей с сохранением всего текста, а шапка `README.md` дополнена переключаемым по теме пользователя логотипом (`<picture>`) и центрированными заголовком и бейджами.

### Known limitations

- Persistence layer и migrations ещё не реализованы.
- Desktop layout ещё не привязан к мониторам: `DesktopManager` оперирует in-memory состоянием, а выбранная раскладка не сохраняется между запусками. Привязка к мониторам запланирована на Stage 7 вместе с persistence.
- UI-панель вкладок и категорий не подключена: контракт `IDesktopManager` полный, но визуальное дерево не изменялось — его подключает владелец проекта.
- AppsFolder MSIX discovery на текущей машине ограничен `0x800401E5`; на штатных системах возвращает список пакетов.
- Окна MSIX-приложений не привязываются к карточкам: у MSIX `ExecutablePath` не заполнен, и его окна не отображаются в инлайн-панели.
- Core Stage 2/3/4 подключены к UI; настройки пока не сохраняются между запусками.
- Installer и release packaging ещё не подготовлены.
- Шифрование at rest не применяется: приложение пока не хранит секреты.
