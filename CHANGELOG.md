# Changelog

Все значимые изменения Glaze Shell будут записываться в этом файле.

## [Unreleased]

### Added

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

#### UI

- Добавлен простой лаунчер UI: поиск, список приложений с virtualization, статусная строка, кнопки «Запустить/Закрыть/Перезапустить/Обновить».
- Добавлен `MainViewModel` с async обновлением, фильтрацией по названию/пути/AUMID и периодической проверкой запущенных процессов.
- Добавлена клавиатурная навигация: ↑/↓, Enter, Space, F5, Esc.
- Приложение теперь подключает `GlazeShell.Windows` и использует реальный composition root в `App.xaml.cs`.

#### Tests

- Управляемый writer `.lnk`-фикстур `ShellLinkBuilder` и `TestShortcutFactory` (без COM `IShellLinkW.Save`, который сломан в текущей среде).
- Добавлены `ShellLinkResolverTests`, `WindowsApplicationLauncherTests`, обновлены `AppsFolderSourceTests` (environment-tolerant) и `StartMenuShortcutSourceTests`.
- Результат: 56/56 tests green (`GlazeShell.Core.Tests` 32/32, `GlazeShell.Windows.Tests` 24/24), Debug и Release build — 0 warnings / 0 errors.

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
- Window management, monitor/DPI integration и Windows event hooks ещё не реализованы.
- AppsFolder MSIX discovery на текущей машине ограничен `0x800401E5`; на штатных системах возвращает список пакетов.
- Core Stage 2/3 подключены к UI; настройки пока не сохраняются между запусками.
- Installer и release packaging ещё не подготовлены.
- Шифрование at rest не применяется: приложение пока не хранит секреты.
