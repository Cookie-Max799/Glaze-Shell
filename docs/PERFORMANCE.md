# Performance

## Stage 0 baseline

Stage 0 не выполняет window/process polling, не создаёт hooks, background services или лишние процессы. Startup создаёт только WinUI window и одну startup log record. Profile и benchmarks ещё не выполнялись.

## Design principles

- Использовать Windows events и callbacks вместо постоянных polling loops.
- Polling допускается только при documented technical limitation, с минимальным интервалом и измеримым обоснованием.
- Не создавать background service без конкретной потребности.
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
