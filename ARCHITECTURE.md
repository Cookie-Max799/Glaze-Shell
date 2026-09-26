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
│   │   └── Services/
│   ├── GlazeShell.Windows/
│   ├── GlazeShell.Data/
│   └── GlazeShell.Infrastructure/
├── tests/
│   └── GlazeShell.Core.Tests/
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

WinUI 3 executable и composition root приложения. Содержит окно с явно заданным размером и центрированием на экране, а также стартовый экран со статусом этапов. Инициализирует конфигурацию и базовый logger. Stage 1 не подключает Core-сервисы к UI.

### GlazeShell.Core

Независимый от UI и Win32 слой. На Stage 1 содержит доменные модели, контракты интерфейсов, события и базовые сервисы.

Модели:

- `Application`, `ApplicationCategory`, `DesktopItem`, `DesktopTab`, `DesktopLayout`;
- `UserSettings`;
- `WindowInfo`, `WindowType`, `WindowState`;
- `MonitorInfo`, `MonitorBounds`, `MonitorOrientation`;
- `Theme` с metadata, colors, fonts, dimensions, icons и анимацией;
- `ModelValidation` — единая валидация входных данных моделей.

Интерфейсы: `IApplicationManager`, `IWindowManager`, `IDesktopManager`, `ISettingsManager`, `IThemeManager`, `IEventManager`, `IMonitorManager`.

События: `WindowOpened`, `WindowClosed`, `ForegroundWindowChanged`, `ProcessStarted`, `ProcessExited`, `DisplayChanged`, `DesktopChanged`, `SettingsChanged`, `ApplicationChanged`.

Сервисы: `EventManager` — типизированная подписка и публикация с возвратом `IDisposable` для отписки; `InMemorySettingsManager` — хранение настроек в памяти без публикации дублирующих событий.

Идентификаторы окон, мониторов и приложений представлены строками, чтобы Core не зависел от `HWND`/`HMONITOR`. Соответствие строк и native handles устанавливается на уровне Windows Integration.

### GlazeShell.Windows

Слой Windows Integration. Содержит только проект с Windows target framework. P/Invoke и Shell API будут изолированы в `Win32`, `Shell`, `Windows` и `Interop` на соответствующих этапах.

### GlazeShell.Data

Слой хранения и сериализации. Структура подготовлена, но JSON persistence, layout и SQLite намеренно не реализованы до Stage 7.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest test project. Проверяет foundation configuration, инварианты моделей, семантику `EventManager` и публикацию `SettingsChanged` в `InMemorySettingsManager`.

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

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн, XAML-композиция и UX не являются частью Stage 0 и Stage 1.

## Следующие архитектурные изменения

Stages 2–5 добавят конкретные Windows implementations. Перед добавлением каждого P/Invoke будут проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
