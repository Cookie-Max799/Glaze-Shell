# Security

## Current security posture

Glaze Shell Stage 0 запускается с обычными правами пользователя. Administrator privileges не требуются. UAC будет рассматриваться только для отдельной операции, для которой действительно нет безопасной непривилегированной альтернативы.

## Filesystem

Базовые пользовательские пути находятся в `%LOCALAPPDATA%\GlazeShell`. `UserDataPaths` проверяет имя каталога конфигурации и отклоняет пустые значения, специальные имена и недопустимые символы.

Stage 0 создаёт только каталог logs. Сериализация конфигурации, migrations, backup и recovery будут реализованы на Stage 7 с отдельной проверкой размера файла, схемы и целостности данных.

## Process execution

Process launching ещё не реализован. Будущий launcher должен использовать разрешённые пользователем application references, проверять пути и не запускать произвольные команды из конфигурации без явного действия пользователя.

## Themes and extensions

Theme data и extensions не должны содержать или автоматически выполнять `.exe`, `.bat`, `.cmd`, `.ps1` или `.dll`. Загрузка themes, IPC и plugins остаётся disabled до отдельного design и security review.

## Logging

Логи содержат timestamp, level, component, message и exception text. Passwords, tokens, API keys и другие secrets не должны передаваться в logger. При расширении logging необходимо добавить redaction policy для структурированных данных и path sanitization.

## Windows integration

Core не должен получать native handles. Все permissions, UAC, process и filesystem операции проходят через узкие interfaces и должны проверяться на Windows API layer.

## Input validation

Все пути, identifiers, JSON поля и будущие IPC messages должны быть валидированы до использования. Недопустимые данные отклоняются с понятным результатом и безопасным log event.

## Secrets

API keys, credentials и secrets не хранятся в source control, themes, logs или пользовательских configuration files без отдельного approved mechanism.
