# Development

## Prerequisites

- Windows 10 1809 или новее.
- .NET SDK, указанный в `global.json`.
- NuGet access для restore.
- Для полноценной WinUI 3 разработки: Visual Studio с Windows App SDK tooling и Windows 10 SDK.
- Git.

Текущий проект собирает App в unpackaged режиме. Stage 0 использует self-contained Windows App SDK; при изменении deployment strategy нужно проверить размер, startup и runtime requirements.

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

На текущем этапе тестируется foundation configuration. Windows integration tests появятся после появления Windows implementations.

## Запуск

```powershell
dotnet run --project .\src\GlazeShell.App\GlazeShell.App.csproj
```

Приложение использует `%LOCALAPPDATA%\GlazeShell\logs\glaze-shell.log` для startup log. Stage 0 не создаёт config.json автоматически; persistence будет реализован на Stage 7.

## Debugging

- Используйте Visual Studio с Windows App SDK tooling для XAML и App lifecycle debugging.
- Для диагностики startup проверьте лог и Output window.
- Не изменяйте generated `bin` и `obj` файлы вручную.
- При добавлении P/Invoke сначала проверьте ownership и lifetime native handles.

## Package policy

- Package versions задаются centrally в `Directory.Packages.props`.
- Не добавляйте floating versions.
- Перед добавлением dependency запишите назначение, лицензию, размер, влияние на startup и альтернативу BCL/Windows API.
- Stage 0 использует MSTest только для автоматического тестирования.

## Git workflow

1. Проверить `git status` и текущую ветку.
2. Изучить существующие изменения и документацию.
3. Реализовать одну логически завершённую задачу.
4. Добавить тесты и обновить документацию.
5. Выполнить build и tests.
6. Проверить diff.
7. Создать отдельный commit в стиле проекта.

Stage 0 использует commit:

```text
chore: initialize project architecture
```

## Release workflow

Release workflow будет добавлен на Stage 14. До его утверждения нельзя публиковать installer или собранные binaries как релиз.
