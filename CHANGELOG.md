# Changelog

Все значимые изменения Glaze Shell будут записываться в этом файле.

## [Unreleased]

### Added

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
- Win32 API, application discovery и window management ещё не реализованы.
- Стартовый экран — заглушка; функциональный UI не реализован.
- Core Stage 1 не подключён к UI; настройки не сохраняются между запусками.
- Installer и release packaging ещё не подготовлены.
- Шифрование at rest не применяется: приложение пока не хранит секреты.
