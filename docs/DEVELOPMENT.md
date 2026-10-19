# Разработка Glaze Shell

Инструкции по сборке, запуску, тестированию и отладке. Обзор проекта и возможности — в [README](../README.md), использование приложения — в [USAGE.md](USAGE.md), архитектура — в [ARCHITECTURE.md](ARCHITECTURE.md), стадии — в [ROADMAP.md](ROADMAP.md), релиз — в [RELEASE.md](RELEASE.md).

## Требования

| Компонент | Версия |
| --- | --- |
| ОС | Windows 10 версии 1809 (`10.0.17763.0`) или новее |
| .NET SDK | `10.0.401` или совместимый, разрешённый `global.json` |
| Windows App SDK | `2.5.1` |
| Microsoft.Windows.SDK.BuildTools | `10.0.26100.7705` |
| Target framework | `net10.0-windows10.0.19041.0` |
| Архитектура | `x64` |

Все версии NuGet-пакетов фиксируются централизованно в `Directory.Packages.props`; floating versions не используются.

Для полноценной разработки и отладки XAML рекомендуется Visual Studio с установленным workload **Windows application development** (включает Windows App SDK tooling и Windows 10/11 SDK).

## Установка и запуск

### 1. Предварительные требования

1. Установите [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0). Проверьте установку:

   ```powershell
   dotnet --version
   ```

   В репозитории действует `global.json`, который разрешает диапазон версий (`rollForward: latestPatch`); в выводе должно быть `10.0.4xx` или совместимый SDK.

2. Установите [Git](https://git-scm.com/downloads) (для клонирования).
3. Для запуска достаточно SDK и рантайма Windows App SDK, который поставляется self-contained вместе с приложением. Visual Studio нужна только для разработки и отладки.

### 2. Клонирование

```powershell
git clone https://github.com/Cookie-Max799/Glaze-Shell.git
cd "Glaze-Shell"
```

### 3. Восстановление пакетов

```powershell
dotnet restore
```

NuGet восстанавливает `Microsoft.WindowsAppSDK` и `Microsoft.Windows.SDK.BuildTools`. Первый restore может занять несколько минут.

### 4. Сборка

```powershell
# Debug
dotnet build --configuration Debug

# Release
dotnet build --configuration Release
```

Проект приложения собирается только под `x64`: значение `AnyCPU` заменяется на `x64` автоматически. Если вы передаёте платформу явно, используйте `--arch x64`:

```powershell
dotnet build --configuration Release --arch x64
```

Ожидаемый результат — `0 warnings / 0 errors`. Проект собирается в режиме **unpackaged** (без MSIX-упаковки) с self-contained Windows App SDK.

### 5. Запуск

Самый простой способ — запуск из репозитория:

```powershell
dotnet run --project .\src\GlazeShell.App\GlazeShell.App.csproj
```

Или через скрипт быстрого запуска:

```powershell
glaze.cmd Debug
glaze.cmd Release
```

Альтернативно можно запустить собранный исполняемый файл:

```powershell
.\src\GlazeShell.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\GlazeShell.exe
```

> Для Release замените `Debug` на `Release` в пути.

Из Visual Studio: откройте `GlazeShell.slnx`, выберите проект `GlazeShell.App` как стартовый, платформу `x64` и нажмите F5.

### 6. Проверка тестами

```powershell
dotnet test
```

Подробности и известные особенности — в разделе [Тестирование](#тестирование).

### 7. Данные и логи приложения

Каталог пользовательских данных, его структура и содержимое описаны в [USAGE.md](USAGE.md#данные-и-логи).

## Скрипт быстрого запуска

В корне репозитория лежит `glaze.cmd`: двойной клик по нему собирает и запускает приложение (консоль закрывается, приложение продолжает работать). Конфигурация по умолчанию — `Debug`, можно передать `Release`:

```text
glaze.cmd
glaze.cmd Release
```

## Тестирование

```powershell
dotnet test
dotnet test --configuration Release
```

Тестовые проекты:

- `tests/GlazeShell.Core.Tests` — модели и события (`CoreModel`, `GlazeEvents`), `EventManager`, `EventCoalescer`, `EventLatencyRecorder`, `InMemorySettingsManager`, `ApplicationProcessMatcher`, `ApplicationIdentity`, `ApplicationDiscoveryOptions`, `ApplicationDiscoveryService` (на фейковых источниках), `DesktopManager`, `MonitorLayoutBinding`, `MonitorMapLayout`, `UserDataDirectoryName`, `UserDataPaths`, `UserSettings`, `ThemeManager`, диагностика (`PerformanceSession`, `PerformanceMonitor`, `ProcessMetricsCollector`), `LogRedactor`, `FileLogger`.
- `tests/GlazeShell.Data.Tests` — `JsonUserDataStoreTests`, `SchemaMigrationRunnerTests`, `PersistenceServicesTests`, `ThemeStoreTests`, `BoundedTextFileTests`, `UserDataDirectoryTests`, `PersistenceLimitsTests`, мапперы документов (`LayoutDocumentMapperTests`, `SettingsDocumentMapperTests`, `ConfigurationDocumentMapperTests`) и `MigrationResultTests`; каждый тест работает в собственном временном каталоге (`TempUserData`), который удаляется после теста.
- `tests/GlazeShell.Windows.Tests` — `ShellLinkResolverTests`, `StartMenuShortcutSourceTests`, `AppsFolderSourceTests`, `WindowsApplicationLauncherTests`, `ProcessInspectorTests`, `ProcessWatchOptionsTests`, `WindowManagerTests`, `WindowEventMonitorTests`, `MonitorManagerTests`, `DisplaySignalTests`, `ProcessEventMonitorTests`, `ShellEventCoordinatorTests`, `SingleInstanceHelperTests`, `TaskbarHiderTests`.

`Windows.Tests` использует управляемый writer `.lnk`-фикстур `ShellLinkBuilder` вместо COM `IShellLinkW.Save`, который в текущем окружении возвращает `0x80070002`.

Текущий статус: 510 tests (`Core.Tests` 269/269, `Data.Tests` 140/140, `Windows.Tests` 101/101 при изолированном запуске) в Debug и Release; build — 0 warnings / 0 errors. В общем прогоне допустим единственный провал — environment-tolerant flake `WindowManagerTests.RaisesWindowOpenedAndClosedEvents` (см. ниже); изолированный запуск `GlazeShell.Windows.Tests` даёт 101/101.

Остальные environment-tolerant тесты не падают из-за недоступных API, а завершаются `Inconclusive` с объясняющим warning: `AppsFolderSourceTests` и `MonitorManagerTests`. `WindowManagerTests.RaisesWindowOpenedAndClosedEvents` зависит от доставки `EVENT_OBJECT_DESTROY` для собственного процесса и потому чувствителен к параллелизму прогонов: таймаут ожидания события истекает, когда тестовые сборки запускаются одновременно. Поведение воспроизводится и на commit до Stage 7, то есть не связано с persistence или темами; изолированный запуск `GlazeShell.Windows.Tests` проходит стабильно.

Изолированный запуск одного проекта:

```powershell
dotnet test .\tests\GlazeShell.Windows.Tests\GlazeShell.Windows.Tests.csproj --configuration Release
```

## Устранение неполадок

| Симптом | Причина и решение |
| --- | --- |
| Приложения MSIX не обнаруживаются | `IShellItemArray` AppsFolder недоступен. Классические `.lnk` из меню «Пуск» продолжают работать. |
| `.lnk` не резолвятся через COM, `IShellLinkW.Save` возвращает `0x80070002` | Известная особенность среды. Резолв идёт managed-парсером `ShellLinkData`; создание фикстур использует managed-writer. |
| Фокус окна не срабатывает | Фокусировка best-effort: Windows может отклонить `SetForegroundWindow` из-за foreground lock. Это ограничение системы, а не ошибка. |
| Второй запуск сразу завершается | Так и задумано: работает уже запущенный экземпляр, его окно поднимается, а новый процесс закрывается. Закройте существующее окно, чтобы запустить заново. |

## Правила кода

- Для XAML и App lifecycle используйте Visual Studio с Windows App SDK tooling; для диагностики startup проверьте лог и Output window.
- Не изменяйте generated `bin` и `obj` вручную.
- При добавлении P/Invoke сначала проверьте ownership и lifetime native handles; сам P/Invoke размещается только в `GlazeShell.Windows`.
- Core-модели и события добавляются в `src/GlazeShell.Core`; не помещайте туда UI, Win32 или инфраструктурные зависимости.
- Новые сервисы Core возвращают `IDisposable`, если удерживают подписки или ресурсы.
- Package versions задаются централизованно в `Directory.Packages.props`; floating versions не добавляются. Перед добавлением dependency фиксируются назначение, лицензия, размер, влияние на startup и альтернатива BCL/Windows API. MSTest используется только для автоматического тестирования.

## Git workflow

1. Проверить `git status` и текущую ветку.
2. Изучить существующие изменения и документацию.
3. Реализовать одну логически завершённую задачу.
4. Добавить тесты и обновить документацию.
5. Выполнить build и tests.
6. Проверить diff.
7. Создать отдельный commit в стиле проекта.

Сообщения коммитов пишутся на русском языке в свободной форме — это фактический стиль истории проекта (см. `git log`). Процесс выпуска версии, версионирование и чеклист релиза описаны в [RELEASE.md](RELEASE.md). Автоматический CI не вводится: сборку и публикацию выполняют локально по чеклисту. Приложение поставляется как unpackaged, Installer и MSIX отсутствуют, поэтому собранный `GlazeShell.exe` не публикуется как готовый релиз для конечных пользователей.

## Известные ограничения

- Visual Studio/WinUI 3 template не установлен в текущем окружении; App project создан вручную.
- В текущей среде `CLSID_ShellLink` зарегистрирован не в `shell32.dll`, поэтому `IShellLinkW.Load` реальных `.lnk` возвращает `0x00000001`, а `Save` — `0x80070002`. Классические `.lnk` резолвятся managed-парсером `ShellLinkData` (LinkInfo/relative path); через property store и `IShellLinkW` — в зависимости от здоровья среды.
- `IShellItemArray` AppsFolder в текущей среде возвращает `0x800401E5`, поэтому MSIX-источник может сообщить warning вместо списка приложений. Это environment-specific limitation и не считается ошибкой продукта.
- Окна MSIX-приложений сопоставляются с карточками по имени семейства пакета, извлечённому из пути процесса окна (`WindowsApps\<PackageFullName>`); каталог установки из реестра для bundle-пакетов указывает на `neutral`-вариант и для этой цели не используется. Побочный эффект: пакет с фоновым сервисом без окна показывается запущенным, пока сервис жив.
- Фокусировка окна — best-effort: при отказе `SetForegroundWindow` (foreground lock) используется fallback через `AttachThreadInput`; в окружениях с жёстким foreground lock команда может не сработать.
- Используется системный title bar, поэтому его оформление не следует тёмной палитре контента; кастомный title bar запланирован вместе с Theme System.
