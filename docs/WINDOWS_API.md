# Windows API

## Текущий статус

На Stage 2 и Stage 3 Glaze Shell использует официальные Win32 и COM API. P/Invoke и COM-интерфейсы изолированы в `GlazeShell.Windows` (каталоги `Interop`, `Shell`, `Win32`, `Applications`).

## Целевая платформа

- Target framework: `net10.0-windows10.0.19041.0`.
- Минимальная версия, заявленная для App и Windows Integration: Windows 10 1809 (`10.0.17763.0`).
- Перед использованием API необходимо проверить его availability для этой версии и для Windows 11.

## Используемые API

| Область | API | Каталог |
| --- | --- | --- |
| Shortcuts | бинарный формат MS-SHLLINK через managed `ShellLinkData` | `Shell/ShellLinkData.cs` |
| Shortcuts | `IShellLinkW` (Load/Get*), `IPersistFile` (Load) | `Shell/IShellLinkW.cs`, `Shell/IPersistFile.cs` |
| Shortcuts | `IShellItem`, `IShellItem2` property store (`PKEY_Link_TargetParsingPath`, `PKEY_Link_Arguments`, `PKEY_Link_Name`) | `Shell/IShellItem.cs` |
| AppsFolder | `SHCreateItemFromParsingName`, `IShellFolder`, `IShellItemArray`, `IID_IShellItemArray` `{56FDF344-FD6D-11D0-958A-006097C9A090}` | `Shell/AppsFolderSource.cs` |
| MSIX | `IApplicationActivationManager` (`ActivateApplication`, `GetApplicationUserModelIdFromProcessId`) | `Interop/IApplicationActivationManager.cs` |
| MSIX | реестр `AppxAllUserStore\Applications` для install location | `Applications/PackageInstallLocationResolver.cs` |
| Processes | `Process.GetProcesses`, `CloseMainWindow` | `Applications/WindowsApplicationLauncher.cs`, `Applications/ProcessInspector.cs` |
| COM | `CoInitializeEx`, `CoUninitialize`, `CoTaskMemFree`, `CoCreateInstance` | `Win32/Ole32.cs`, `Shell/ShellIdentifiers.cs` |

## Правила использования

- P/Invoke размещается только в `GlazeShell.Windows` и изолируется в `Win32` или `Interop`.
- Core не импортирует Windows API и не использует native handle types.
- Каждый API задокументирован: permissions, lifetime ресурсов, thread affinity и альтернатива.
- COM-объекты создаются в STA и освобождаются; `CoInitializeEx`/`CoUninitialize` балансируются на dedicated threads.
- Managed fast path (`ShellLinkData`) используется первым: он не требует COM и работает при нерабочем `CLSID_ShellLink`.
- Не используются undocumented API без отдельного обоснования и security review.

## Известные ограничения среды

- В текущем окружении `CLSID_ShellLink` зарегистрирован в `C:\Windows\System32\windows.storage.dll`, `IShellLinkW.Load` реальных `.lnk` возвращает `0x00000001`, `Save` — `0x80070002`.
- `IPropertyStore`/`IShellItem2` на реальных `.lnk` возвращают `0x80004002`; AppsFolder через `MatchOption.None` — `0x800401E5`.
- `SHGetPathFromIDListEx` с извлечённым из `.lnk` PIDL вызывал `0xC0000005`; native PIDL-резолв не используется, вместо него работает managed LinkInfo.
- Эти ограничения специфичны для данной машины; на штатных системах соответствующие fallback возвращают результат.
