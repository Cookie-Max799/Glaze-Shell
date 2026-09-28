# Glaze Shell

Glaze Shell — Windows-приложение для глубокой персонализации рабочего окружения поверх штатных возможностей Windows. Проект не изменяет системные файлы и не заменяет компоненты Windows.

## Текущий статус

Stage 0 — Foundation завершён. Создана минимальная запускаемая WinUI 3 оболочка, слоистая структура .NET-решения, базовая конфигурация, централизованное файловое логирование и тестовый проект.

Stage 1 — Core завершён. Реализованы доменные модели, контракты интерфейсов, события и базовые framework-free сервисы `GlazeShell.Core`.

Stage 2 — Application Discovery завершён. Реализовано обнаружение приложений из меню «Пуск» (`.lnk`) и пакетов MSIX/AppsFolder через официальные Windows API.

Stage 3 — Application Launcher завершён. Реализованы запуск, проверка состояния и закрытие приложений (Win32 + MSIX), а также простой UI лаунчера с поиском и горячими клавишами.

## Возможности

Реализовано на текущем этапе:

- обнаружение приложений меню «Пуск»: managed-парсер бинарных shortcut-файлов (MS-SHLLINK) с fallback на `IShellLinkW` и shell property store;
- обнаружение MSIX/Store-приложений через AppsFolder (`IShellItemArray`, `PKEY_AppUserModelID`);
- агрегация кандидатов, dedup по launch identity, предупреждения диагностики по каждому источнику;
- запуск: MSIX через `IApplicationActivationManager`, Win32 через `ProcessStartInfo`;
- проверка состояния и закрытие через `CloseMainWindow` (без принудительного kill);
- UI лаунчера: поиск, список с virtualization, статус выполнения, клавиатурная навигация (↑/↓, Enter, Space, F5, Esc), периодическая проверка фоновых процессов;
- файловое логирование в `%LOCALAPPDATA%\GlazeShell\logs`.

Планируемые возможности:

- кастомизация рабочего стола;
- пользовательские вкладки рабочего пространства;
- категории и размещение приложений;
- управление окнами;
- поддержка нескольких мониторов и DPI;
- пользовательские темы и обои;
- системные события Windows;
- автозапуск и сохранение конфигурации;
- будущая система виджетов и расширений.

## Roadmap

1. Foundation — структура решения, сборка, логирование и базовая конфигурация. ✅
2. Core — доменные модели, интерфейсы и события. ✅
3. Application Discovery — обнаружение установленных приложений. ✅
4. Application Launcher — запуск, проверка состояния и закрытие приложений. ✅
5. Window Manager — управление окнами и события окон.
6. Desktop Integration — мониторы, DPI и display configuration.
7. Tabs and Categories — вкладки, категории и порядок элементов.
8. Persistence — JSON, layout, settings, schema migrations и recovery.
9. Theme System — безопасная загрузка данных тем без исполняемого кода.
10. Windows Events — централизованная event-driven модель.
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

При первом запуске создаётся каталог пользовательских данных в `%LOCALAPPDATA%\GlazeShell`. Приложение создаёт каталог логов; полноценное сохранение конфигурации запланировано на Stage 7.

## Development

Структура, правила сборки и Git workflow описаны в [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Архитектура

Описание слоёв и dependency flow находится в [ARCHITECTURE.md](ARCHITECTURE.md).

Документация по системным возможностям и безопасности:

- [docs/WINDOWS_API.md](docs/WINDOWS_API.md)
- [docs/SECURITY.md](docs/SECURITY.md)
- [docs/PERFORMANCE.md](docs/PERFORMANCE.md)

## Known issues

- Visual Studio/WinUI 3 template не установлен в текущем окружении; App project создан вручную.
- В текущей среде `CLSID_ShellLink` зарегистрирован не в `shell32.dll`, поэтому `IShellLinkW.Load` реальных `.lnk` возвращает `0x00000001`, а `Save` — `0x80070002`. Классические `.lnk` резолвятся managed-парсером `ShellLinkData` (LinkInfo/relative path); через property store и `IShellLinkW` — в зависимости от здоровья среды.
- `IShellItemArray` AppsFolder в текущей среде возвращает `0x800401E5`, поэтому MSIX-источник может сообщить warning вместо списка приложений. Это environment-specific limitation и не считается ошибкой продукта.
- `InMemorySettingsManager` не сохраняет настройки между запусками; persistence запланирован на Stage 7.
- Используется системный title bar, поэтому его оформление не следует тёмной палитре контента; кастомный title bar запланирован вместе с Theme System.
- Лицензия проекта ещё не выбрана владельцем проекта; файл `LICENSE` не предоставляет юридических прав до утверждения лицензии.

## Changelog

История изменений находится в [CHANGELOG.md](CHANGELOG.md).