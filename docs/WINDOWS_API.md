# Windows API

## Stage 0 status

На Stage 0 Glaze Shell не вызывает Win32, Windows Shell API, Windows Runtime APIs или P/Invoke. Проект `GlazeShell.Windows` подготовлен как отдельный слой и ориентирован на Windows target framework.

## Целевая платформа

- Target framework: `net10.0-windows10.0.19041.0`.
- Минимальная версия, заявленная для App и Windows Integration: Windows 10 1809 (`10.0.17763.0`).
- Перед использованием API необходимо проверить его availability для этой версии и для Windows 11.

## Правила использования

- P/Invoke размещается только в `GlazeShell.Windows` и изолируется в `Win32` или `Interop`.
- Core не импортирует Windows API и не использует native handle types.
- Каждый API должен иметь документированные permissions, lifetime ресурсов, thread affinity и альтернативу.
- Предпочтение отдаётся поддерживаемым официальным API и Windows Runtime/COM abstractions.
- Handles, buffers, hooks и COM references освобождаются на всех error paths.
- Не используются undocumented API без отдельного обоснования и security review.

## Будущие области

Application discovery, process launching, window management, monitor configuration, DPI и shell events будут реализованы на следующих этапах. Конкретные API, permissions и ограничения будут зафиксированы здесь до включения P/Invoke в код.
