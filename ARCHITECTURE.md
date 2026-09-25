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

WinUI 3 executable и composition root приложения. На Stage 0 содержит только минимальное окно без визуального дизайна, инициализирует конфигурацию и базовый logger.

### GlazeShell.Core

Независимый от UI и Win32 слой. На Stage 0 содержит базовую конфигурацию со `schemaVersion`; доменные модели, интерфейсы, services и events будут добавлены на Stage 1.

### GlazeShell.Windows

Слой Windows Integration. На Stage 0 содержит только проект с Windows target framework. P/Invoke и Shell API будут изолированы в `Win32`, `Shell`, `Windows` и `Interop` на соответствующих этапах.

### GlazeShell.Data

Слой хранения и сериализации. На Stage 0 структура подготовлена, но JSON persistence, layout и SQLite намеренно не реализованы до Stage 7.

### GlazeShell.Infrastructure

Реализации, которые зависят от ОС и внешней среды: логирование, user-data paths, diagnostics и startup. Stage 0 содержит минимальный файловый logger и безопасное вычисление путей в `%LOCALAPPDATA%`.

### GlazeShell.Core.Tests

MSTest smoke test foundation. Проверяет базовую конфигурацию и будет расширяться по мере добавления Core behavior.

## Решения Stage 0

- Решение использует `GlazeShell.slnx`, поддерживаемый установленным .NET 10 SDK.
- Для App используется ручной unpackaged WinUI 3 scaffold, поскольку в окружении отсутствует WinUI template.
- `GlazeShell.Windows` уже ориентирован на Windows TFM, но не содержит системных вызовов.
- Пользовательские данные будут храниться в `%LOCALAPPDATA%\GlazeShell`; на этом этапе создаётся только logs path.
- Версии NuGet-пакетов фиксируются через `Directory.Packages.props` и не используют floating versions.
- `Core/Configuration` добавлен как объективное исключение из базовой структуры: базовая схема конфигурации должна быть доступна UI и Infrastructure без Win32-зависимостей.

## Границы UI

UI может использовать модели, интерфейсы Application Services, events и state, не зная о native handles и Win32. Визуальный дизайн, XAML-композиция и UX не являются частью Stage 0.

## Следующие архитектурные изменения

Stage 1 добавит доменные модели и интерфейсы. Stages 2–5 добавят конкретные Windows implementations. Перед добавлением каждого P/Invoke будет проверены поддержка Windows, permissions, lifetime ресурсов и альтернативы.
