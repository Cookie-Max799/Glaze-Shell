# Performance

## Baseline

Приложение не создаёт hooks или фоновые service процессы. Startup создаёт WinUI window, startup log record и запускает однократное сканирование приложений в фоне.

Stage 1 добавил только framework-free Core: модели, контракты и in-memory сервисы, которые не создают потоков, таймеров или внешних ресурсов.

`EventManager` не хранит события и не запускает фоновую обработку: доставка выполняется синхронно в потоке publisher, а `Publish` работает с копией списка подписок, поэтому подписка и отписка не требуют блокировки на стороне вызывающего.

## Stage 2 — Discovery

- `StartMenuShortcutSource` использует параллельное сканирование с ограничением `min(max(1, Environment.ProcessorCount), 8)` потоков; результат детерминированно сортируется.
- Для каждого `.lnk` сначала выполняется managed fast path (`ShellLinkData`) без COM; fallback на `IShellLinkW`/property store выполняется только для нерезолвнутых файлов.
- AppsFolder сканируется через один STA-вызов `IShellItemArray`; COM-объекты освобождаются в `finally`.
- `ApplicationDiscoveryService` кэширует снапшот на `CacheDuration` (5 минут) и использует single-flight lock, поэтому повторный `DiscoverAsync` в течение окна не сканирует диск.
- Результат сканирования (десятки shortlinks) собирается за время меньше секунды на штатных системах.

## Stage 3 — Launcher

- `PackageInstallLocationResolver` один раз строит immutable cache из реестра AppxAllUserStore и не обращается к реестру на каждый запрос.
- `ProcessInspector` итерирует процессы один раз, освобождая каждый `Process` через `using`, и не копирует `Process[]` бесконечно.
- UI проверяет фоновые процессы не чаще, чем раз в 3 секунды, и только после завершения первичного сканирования.
- Периодический polling оправдан техническим ограничением: .NET не предоставляет событие «процесс запущен» для произвольных exe.

## Design principles

- Использовать Windows events и callbacks вместо постоянных polling loops.
- Polling допускается только при documented technical limitation, с минимальным интервалом и измеримым обоснованием.
- Не создавать background service без конкретной потребности.
- Не доставлять события из фонового потока в UI без явного маршалинга на UI thread.
- Проверять idle CPU, RAM, startup time, thread count, Win32 handles, GDI/USER handles и allocations.
- Освобождать event hooks, native handles, subscriptions и disposable services на всех exit paths.
- Не выполнять тяжёлую disk/network операцию на UI thread.
- Не добавлять rendering или animation logic до появления соответствующего UI design.

## Planned measurements

Stage 11 выполнит профилирование и зафиксирует baseline для:

- запуска приложения;
- idle CPU и RAM;
- количества потоков и handles;
- поведения при monitor/DPI changes;
- event callback latency;
- утечек resources после create/destroy сценариев.

Результаты измерений и найденные bottlenecks будут добавлены в этот документ.
