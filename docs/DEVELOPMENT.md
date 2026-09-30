# Development

## Prerequisites

- Windows 10 1809 или новее.
- .NET SDK, указанный в `global.json`.
- NuGet access для restore.
- Для полноценной WinUI 3 разработки: Visual Studio с Windows App SDK tooling и Windows 10 SDK.
- Git.

Текущий проект собирает App в unpackaged режиме. Приложение использует self-contained Windows App SDK; при изменении deployment strategy нужно проверить размер, startup и runtime requirements.

## Restore and build

Из корня repository:

```powershell
dotnet restore
dotnet build --configuration Debug
dotnet build --configuration Release
```

Для полной диагностики restore:

```powershell
dotnet restore --verbosity normal
```

## Tests

```powershell
dotnet test
dotnet test --configuration Release
```

Тестовые проекты:

- `tests/GlazeShell.Core.Tests` — модели, `EventManager`, `InMemorySettingsManager`, discovery service, `DesktopManager`, `MonitorLayoutBinding`, `UserDataDirectoryName`, `ThemeManager`.
- `tests/GlazeShell.Data.Tests` — `JsonUserDataStoreTests`, `SchemaMigrationRunnerTests`, `PersistenceServicesTests`, `ThemeStoreTests`. Каждый тест работает в собственном временном каталоге (`TempUserData`), который удаляется после теста.
- `tests/GlazeShell.Windows.Tests` — `ShellLinkResolverTests`, `StartMenuShortcutSourceTests`, `AppsFolderSourceTests`, `WindowsApplicationLauncherTests`, `WindowManagerTests`, `MonitorManagerTests`.

`Windows.Tests` использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо COM `IShellLinkW.Save`, который в текущем окружении возвращает `0x80070002`.

`WindowManagerTests.RaisesWindowOpenedAndClosedEvents` зависит от доставки `EVENT_OBJECT_DESTROY` для собственного процесса и потому чувствителен к нагрузке и параллелизму прогонов: таймаут ожидания события истекает, когда тестовые сборки запускаются одновременно. Поведение воспроизводится и на commit до Stage 7, то есть не связано с persistence или темами; изолированный запуск `GlazeShell.Windows.Tests` проходит стабильно.

`AppsFolderSourceTests` и `MonitorManagerTests` являются environment-tolerant: на машинах, где API недоступен, тест проверяет наличие объясняющего warning или завершается `Inconclusive`, а не падает на списке приложений/мониторов.

Текущий статус: 220/221 tests green в Debug и Release (`Core.Tests` 121/121, `Data.Tests` 64/64, `Windows.Tests` 35 passed), Debug и Release build — 0 warnings / 0 errors. `FocusBringsWindowToForegroundOrIsDeniedBySystem` — единственный environment-tolerant skip: результат зависит от foreground lock текущей сессии и меняется между прогонами (в отдельных прогонах он проходит, давая 181/181).

## Запуск

```powershell
dotnet run --project .\src\GlazeShell.App\GlazeShell.App.csproj
```

Приложение использует `%LOCALAPPDATA%\GlazeShell\logs\glaze-shell.log` для startup log и `%LOCALAPPDATA%\GlazeShell` как каталог пользовательских данных: `config.json`, `settings.json`, `layout.json`, а также `backups` и `recovery`. Имя каталога данных задаётся полем `dataDirectoryName` в `config.json`; при его смене лог текущей сессии остаётся в исходном каталоге.

App project собирается только под `x64`. Если передать платформу явно, используйте `--arch x64`; значение `AnyCPU` в `.csproj` заменяется на `x64` автоматически.

## Debugging

- Используйте Visual Studio с Windows App SDK tooling для XAML и App lifecycle debugging.
- Для диагностики startup проверьте лог и Output window.
- Не изменяйте generated `bin` и `obj` файлы вручную.
- При добавлении P/Invoke сначала проверьте ownership и lifetime native handles.
- Core-модели и события добавляются в `src/GlazeShell.Core`; не помещайте туда UI, Win32 или инфраструктурные зависимости.
- Новые сервисы Core возвращают `IDisposable`, если удерживают подписки или ресурсы.

## Package policy

- Package versions задаются centrally в `Directory.Packages.props`.
- Не добавляйте floating versions.
- Перед добавлением dependency запишите назначение, лицензию, размер, влияние на startup и альтернативу BCL/Windows API.
- MSTest используется только для автоматического тестирования.

## Git workflow

1. Проверить `git status` и текущую ветку.
2. Изучить существующие изменения и документацию.
3. Реализовать одну логически завершённую задачу.
4. Добавить тесты и обновить документацию.
5. Выполнить build и tests.
6. Проверить diff.
7. Создать отдельный commit в стиле проекта.

Сообщения коммитов пишутся на русском языке в свободной форме — это фактический стиль истории проекта (см. `git log`). Требование conventional commits из исходного плана не применяется; каждое расхождение фиксируется в локальном `AGENT_PROMPT.md`.

Ориентиры по стадиям:

```text
Начальная стадия, пока доступна лишь консоль, в виде заглушки, без везуальных элементов
добавлен визуал ( заглушка ), исправлены проблемы с логами и секретами, обновлена система безопасности
реализованы Stage 2 и Stage 3: discovery, launcher, UI и тесты
реализован Stage 4: window manager, события окон, UI-панель окон и тесты
реализован Stage 5: desktop integration - мониторы, DPI, display events, desktop layout и тесты
реализован Stage 6: tabs, categories, размещение приложений, канонический порядок и тесты
реализован Stage 7: persistence (JSON-документы, schema migrations, recovery, автосохранение layout, привязка вкладок к мониторам) и Stage 8: theme system (реестр тем, выбор активной темы, загрузка пользовательских тем из `themes` без исполняемого кода) и тесты
```

## Release workflow

Release workflow будет добавлен на Stage 14. До его утверждения нельзя публиковать installer или собранные binaries как релиз.
