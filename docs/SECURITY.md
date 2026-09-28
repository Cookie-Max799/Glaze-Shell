# Security

## Текущая модель угроз

Glaze Shell Stage 1 — локальное desktop-приложение, работающее с обычными правами пользователя. Administrator privileges не требуются, сетевого взаимодействия, process launching и plugins на текущем этапе нет.

Защищаемые активы:

- пользовательские настройки и конфигурация;
- содержимое логов;
- будущие application references и credentials.

Предполагаемые нарушители: локальный процесс, запущенный от имени того же пользователя, и случайное раскрытие данных через логи, отчёты об ошибках или репозиторий. Модель угроз не покрывает защиту от rootkit, физического доступа к машине и от пользователя, который сам может читать свои файлы.

## Filesystem

Базовые пользовательские пути находятся в `%LOCALAPPDATA%\GlazeShell`, то есть в каталоге, доступном только текущему пользователю. Явные ACL не устанавливаются: `%LOCALAPPDATA%` уже защищён пользовательскими правами, а ручная установка `DirectorySecurity` добавила бы platform-specific риск без выигрыша в защите.

`UserDataPaths` отклоняет:

- пустые значения и значения из одних пробелов;
- `.` и `..`, а также имена с разделителями пути;
- зарезервированные имена устройств Windows (`CON`, `NUL`, `COM1`–`COM9`, `LPT1`–`LPT9` и другие), в том числе с расширением;
- имена длиннее 64 символов;
- имена, заканчивающиеся точкой или пробелом, которые Windows молча нормализует.

Stage 1 создаёт только каталог logs. Сериализация конфигурации, migrations, backup и recovery будут реализованы на Stage 7 с отдельной проверкой размера файла, схемы и целостности данных.

## Logging

Логи пишутся в `%LOCALAPPDATA%\GlazeShell\logs\glaze-shell.log` в формате JSON lines и содержат timestamp, level, component, message и exception text.

Меры против утечки данных:

- `LogRedactor` маскирует присваивания `password`, `passwd`, `pwd`, `secret`, `token`, `api_key`, `apikey`, `access_token`, `refresh_token`, `client_secret`, `authorization` и значения вида `Bearer <token>`, независимо от регистра и от JSON-стиля;
- `LogRedactor` заменяет имя пользователя в путях вида `C:\Users\<имя>\...` на `[REDACTED]`;
- текст каждой записи ограничен `LogRedactor.MaxTextLength` символами, чтобы одно исключение не раздувало файл;
- активный лог ограничен `FileLogger.DefaultMaxFileSizeBytes` (1 MiB) и ротируется в `.1`–`.3`; более старые архивы удаляются, поэтому логи не могут заполнить диск бесконечно.

Ошибки записи лога не пробрасываются в вызывающий код: `FileLogger` перехватывает `IOException` и `UnauthorizedAccessException`, чтобы невозможность записать диагностику не ломала работу оболочки.

Маскирование не является заменой правильной разработке: secrets не должны передаваться в logger в исходном виде. Redaction — второй рубеж, а не первый.

## Secrets

API keys, credentials и secrets не хранятся в source control, themes, logs или пользовательских configuration files без отдельного approved mechanism. `.gitignore` исключает `.env`, `.env.*`, `*.pfx`, `*.p12`, `*.snk`, `*.key`, `*.pem`, `*.crt`, `*.cer`, `*.secret` и `secrets.json`.

Шифрование at rest сейчас не применяется, потому что приложение не хранит секреты. Когда появится первый реальный credential, он должен заменяться на Windows DPAPI или Credential Manager, а не на собственную реализацию шифрования.

## Process execution

Запуск выполняется только для разрешённых пользователем application references из discovery (Start Menu `.lnk` и установленные MSIX-пакеты). Launcher не выполняет произвольные команды из конфигурации и не принимает команды из IPC.

- Win32-приложения запускаются через `ProcessStartInfo` с `UseShellExecute=false` и явным `FileName`: не используется SHELLEXECUTE-инъекция командной строки.
- MSIX-приложения запускаются через `IApplicationActivationManager.ActivateApplication` исключительно по `ApplicationUserModelId`, полученному из AppsFolder.
- Путь к target проверяется `File.Exists` до запуска; для отсутствующих файлов запуск не выполняется.
- Закрытие выполняется только через `CloseMainWindow`; принудительный `Kill` не применяется.
- `ProcessInspector` не раскрывает пути и не отвечает на управляющие входные данные: он принимает только уже провалидированные пути приложений.

## Window management

Window Manager оперирует только HWND, полученными через официальное перечисление `EnumWindows` с фильтрацией (видимые, не cloak-нутые, не tool-windows). Core получает строковые id; native handles не покидают Windows Integration.

- Фокус окна — best-effort через `SetForegroundWindow` с fallback `AttachThreadInput`; ключевое слово `SendInput` и синтетические события ввода не используются.
- Состояние изменяется только `ShowWindowAsync`; закрытие — только вежливый `WM_CLOSE` через `PostMessage`. Принудительное завершение процесса (`TerminateProcess`) не применяется никогда.
- Window Manager не исполняет код в чужих процессах: `SetWinEventHook` работает в OUTOFCONTEXT-режиме (callback в своём процессе), опция `WINEVENT_SKIPOWNPROCESS` исключает события собственного процесса.
- Пользователь управляет только окнами, которые система считает перечисляемыми top-level окнами; недоступность окна (например, foreground lock) возвращает `false`, а не работает в обход Windows.

## Themes and extensions

Theme data и extensions не должны содержать или автоматически выполнять `.exe`, `.bat`, `.cmd`, `.ps1` или `.dll`. Загрузка themes, IPC и plugins остаётся disabled до отдельного design и security review.

## Windows integration

Core не должен получать native handles. Все permissions, UAC, process и filesystem операции проходят через узкие interfaces и должны проверяться на Windows API layer.

`app.manifest` задаёт `asInvoker`, Per-Monitor V2 DPI awareness, `longPathAware` и `SegmentHeap`. SegmentHeap и `asInvoker` закрывают известные техники обхода DEP и повышения привилегий через UI.

Приложение поставляется как unpackaged: у него нет package identity, AppContainer и изоляции процесса. Это осознанный компромисс Stage 1 — до появления Installer следует понимать, что защита ограничена правами пользователя.

## Input validation

Все пути, identifiers, JSON поля и будущие IPC messages должны быть валидированы до использования. Недопустимые данные отклоняются с понятным результатом и безопасным log event.

## Принятые риски

- Diagnostics локальны и не шифруются: содержимое логов читается любым процессом пользователя. При необходимости выгрузки логов наружу требуется отдельная редакция данных.
- Пользователь с правами на свой `%LOCALAPPDATA%` может удалить или изменить настройки и логи; целостность конфигурации не защищается подписью.
- Unpackaged deployment не даёт AppContainer isolation; переход к MSIX рассматривается вместе с Installer.
- `EventManager` не изолирует исключение подписчика, если не задан `exceptionHandler`: ошибка подписчика доходит до вызывающего кода. Это осознанное поведение, чтобы ошибки не скрывались, но его нужно учитывать при добавлении внешних подписчиков.
