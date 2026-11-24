using System.Runtime.InteropServices;
using GlazeShell.Core.SystemStatus;

namespace GlazeShell.Windows.SystemStatus;

/// <summary>
/// Читает заряд подключённых беспроводных устройств через дерево устройств PnP.
/// </summary>
/// <remarks>
/// <para>
/// Windows публикует заряд Bluetooth-устройств в свойстве <c>DEVPKEY_Bluetooth_Battery</c>
/// узлов перечислителей <c>BTHENUM</c> и <c>BTHLE</c>. Такие узлы существуют, только пока
/// устройство подключено, поэтому сам факт наличия заряда служит и признаком подключения:
/// сопряжённые, но выключенные устройства в списке не появляются.
/// </para>
/// <para>
/// Одно устройство даёт несколько узлов (A2DP, «свободные руки», AVRCP, HID). Они
/// объединяются по контейнеру PnP, поэтому наушники показываются одной строкой. Имя
/// берётся у корневого узла <c>\DEV_…</c>: у него оно без суффиксов профилей.
/// Устройства без заряда (адаптеры, службы, служебные профили) пропускаются.
/// </para>
/// <para>
/// Ошибки чтения (служба PnP недоступна, узел исчез между запросами) не пробрасываются:
/// панель просто показывает список без этого устройства.
/// </para>
/// </remarks>
public static class DeviceBatteryProvider
{
    /// <summary>Перечислители Bluetooth-устройств: их узлы хранят заряд.</summary>
    private static readonly string[] BluetoothEnumerators = ["BTHENUM", "BTHLE"];

    /// <summary>
    /// Устройства с известным зарядом, отсортированные по имени.
    /// </summary>
    public static IReadOnlyList<WirelessDeviceInfo> GetDevices()
    {
        try
        {
            return GetDevicesCore();
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            return [];
        }
    }

    private static List<WirelessDeviceInfo> GetDevicesCore()
    {
        var size = 0u;
        if (CmError(CM_Get_Device_ID_List_SizeW(out size, null, CmGetIdListFilterPresent)))
        {
            return [];
        }

        // Размер возвращается в символах вместе с завершающим нулём.
        var buffer = new char[size + 1];
        if (CmError(CM_Get_Device_ID_ListW(null, buffer, (uint)buffer.Length, CmGetIdListFilterPresent)))
        {
            return [];
        }

        var nodes = new List<Node>();
        var classes = new Dictionary<Guid, List<string>>();

        foreach (var id in SplitIds(buffer))
        {
            if (CmError(CM_Locate_DevNodeW(out var node, id, CmLocateDevnodeNormal)))
            {
                continue;
            }

            var container = ReadGuid(node, ContainerIdKey);
            if (container == Guid.Empty || container == UncontainedDevice)
            {
                // Устройства без контейнера Windows складывает в один псевдоконтейнер:
                // группировать по нему нельзя, иначе мышь и клавиатура слились бы с
                // наушниками в одно «устройство».
                continue;
            }

            if (!classes.TryGetValue(container, out var containerClasses))
            {
                containerClasses = [];
                classes[container] = containerClasses;
            }

            if (ReadString(node, DeviceClassKey) is { Length: > 0 } deviceClass)
            {
                containerClasses.Add(deviceClass);
            }

            if (!IsBluetoothNode(id))
            {
                continue;
            }

            nodes.Add(new Node(
                id,
                container,
                ReadBattery(node),
                ReadString(node, FriendlyNameKey)));
        }

        return BuildDevices(nodes, classes);
    }

    private static bool IsBluetoothNode(string instanceId)
    {
        var separator = instanceId.IndexOf('\\');
        if (separator <= 0)
        {
            return false;
        }

        var prefix = instanceId[..separator];
        return BluetoothEnumerators.Contains(prefix, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Собирает устройства из узлов одного контейнера: заряд — наибольший среди узлов,
    /// где он известен, имя — у корневого узла <c>\DEV_…</c>, тип — по классам PnP
    /// всех узлов контейнера и по имени.
    /// </summary>
    private static List<WirelessDeviceInfo> BuildDevices(
        IReadOnlyList<Node> nodes,
        Dictionary<Guid, List<string>> classes)
    {
        var result = new List<WirelessDeviceInfo>();

        foreach (var group in nodes.GroupBy(node => node.Container))
        {
            var members = group.ToList();

            // Устройство без заряда (адаптер, служебный профиль, сопряжённое, но
            // выключенное) в строке не нужно: строка — это про заряд.
            var batteries = members
                .Where(node => node.Battery is not null)
                .Select(node => node.Battery!.Value)
                .ToList();

            if (batteries.Count == 0)
            {
                continue;
            }

            var name = PickName(members);
            var kind = WirelessDeviceClassifier.Classify(
                name,
                members.Select(node => node.InstanceId).ToList(),
                classes.TryGetValue(group.Key, out var containerClasses) ? containerClasses : null);

            result.Add(new WirelessDeviceInfo(
                group.Key.ToString("D"),
                name,
                Math.Clamp(batteries.Max(), 0, 100),
                kind));
        }

        return result
            .OrderBy(device => device.Name, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>
    /// Имя устройства. У корневого узла <c>\DEV_…</c> оно без суффиксов профилей
    /// («Ear (2)», а не «Ear (2) Hands-Free AG»), поэтому он предпочтительнее.
    /// </summary>
    private static string PickName(IReadOnlyList<Node> members)
    {
        foreach (var member in members)
        {
            if (member.InstanceId.Contains("\\DEV_", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(member.Name))
            {
                return member.Name.Trim();
            }
        }

        // Запасной вариант — самое короткое имя: у профилей к названию добавляются
        // описания («Hands-Free AG», «AVRCP Transport»), и они длиннее.
        var fallback = members
            .Select(node => node.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name!.Length)
            .FirstOrDefault();

        return fallback ?? "Bluetooth-устройство";
    }

    /// <summary>
    /// Заряд устройства в процентах. Значения вне 0–100 Windows использует для
    /// «заряд неизвестен», и такие узлы пропускаются.
    /// </summary>
    private static int? ReadBattery(nint node)
    {
        var (type, buffer) = ReadProperty(node, BluetoothBatteryKey, 16);
        if (buffer is null)
        {
            return null;
        }

        int? value = type switch
        {
            CmDevpropTypeByte => buffer[0],
            CmDevpropTypeUInt32 when buffer.Length >= 4 => BitConverter.ToInt32(buffer, 0),
            _ => null,
        };

        return value is >= 0 and <= 100 ? value : null;
    }

    private static Guid ReadGuid(nint node, DevPropKey key)
    {
        var (type, buffer) = ReadProperty(node, key, 16);
        return type == CmDevpropTypeGuid && buffer is { Length: >= 16 }
            ? new Guid(buffer.AsSpan(0, 16))
            : Guid.Empty;
    }

    private static string? ReadString(nint node, DevPropKey key)
    {
        var (type, buffer) = ReadProperty(node, key, 512);
        if (type != CmDevpropTypeString || buffer is null)
        {
            return null;
        }

        var text = System.Text.Encoding.Unicode.GetString(buffer, 0, buffer.Length);
        var end = text.IndexOf('\0');
        return end < 0 ? text : text[..end];
    }

    /// <summary>
    /// Читает свойство узла. Свойства может не быть вовсе — тогда Windows возвращает
    /// отказ, и значение считается неизвестным. Если же буфер мал, Windows сообщает
    /// нужный размер, и чтение повторяется с ним.
    /// </summary>
    private static (uint Type, byte[]? Buffer) ReadProperty(nint node, DevPropKey key, int hintSize)
    {
        var size = (uint)Math.Max(hintSize, 16);
        var buffer = new byte[size];

        var result = CM_Get_DevNode_PropertyW(node, ref key, out var type, buffer, ref size, 0);
        if (result == 0)
        {
            return (type, buffer);
        }

        // Отказ с ненулевым требуемым размером означает «буфер мал»: повторяем чтение.
        if (size > buffer.Length)
        {
            buffer = new byte[size];
            result = CM_Get_DevNode_PropertyW(node, ref key, out type, buffer, ref size, 0);
            if (result == 0)
            {
                return (type, buffer);
            }
        }

        return (type, null);
    }

    private static IEnumerable<string> SplitIds(char[] buffer)
    {
        var start = 0;
        while (start < buffer.Length)
        {
            var end = Array.IndexOf(buffer, '\0', start);
            if (end < 0)
            {
                break;
            }

            if (end > start)
            {
                yield return new string(buffer, start, end - start);
            }

            // Двойной нуль — конец списка.
            if (end + 1 < buffer.Length && buffer[end + 1] == '\0')
            {
                break;
            }

            start = end + 1;
        }
    }

    private static bool CmError(int code) => code != 0;

    /// <summary>Узел PnP: контейнер устройства, заряд (если известен) и имя.</summary>
    private sealed record Node(string InstanceId, Guid Container, int? Battery, string? Name);

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public DevPropKey(Guid fmtId, uint pid)
        {
            FmtId = fmtId;
            Pid = pid;
        }

        public Guid FmtId;
        public uint Pid;
    }

    private const uint CmGetIdListFilterPresent = 0x0000_0100;
    private const uint CmLocateDevnodeNormal = 0x0000_0000;

    private const uint CmDevpropTypeByte = 0x0000_0003;
    private const uint CmDevpropTypeUInt32 = 0x0000_0007;
    private const uint CmDevpropTypeGuid = 0x0000_000D;
    private const uint CmDevpropTypeString = 0x0000_0012;

    /// <summary><c>DEVPKEY_Bluetooth_Battery</c>: заряд устройства в процентах.</summary>
    private static DevPropKey BluetoothBatteryKey => new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);

    /// <summary><c>DEVPKEY_Device_ContainerId</c>: объединяет профили одного устройства.</summary>
    private static DevPropKey ContainerIdKey => new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);

    /// <summary><c>DEVPKEY_Device_FriendlyName</c>.</summary>
    private static DevPropKey FriendlyNameKey => new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);

    /// <summary><c>DEVPKEY_Device_Class</c>: класс драйвера («Mouse», «Keyboard», «MEDIA»).</summary>
    private static DevPropKey DeviceClassKey => new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 9);

    /// <summary>
    /// Псевдоконтейнер устройств, у которых своего контейнера нет: группировать по нему
    /// нельзя.
    /// </summary>
    private static readonly Guid UncontainedDevice = new("00000000-0000-0000-FFFF-FFFFFFFFFFFF");

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_List_SizeW(out uint size, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint bufferSize, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out nint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(
        nint devInst,
        ref DevPropKey key,
        out uint propertyType,
        [Out] byte[]? buffer,
        ref uint bufferSize,
        uint flags);

    /// <summary>Ожидаемые отказы: API отсутствует или среда ограничена. Остальное — ошибка кода.</summary>
    private static bool IsExpected(Exception exception) =>
        exception is DllNotFoundException
            or EntryPointNotFoundException
            or InvalidOperationException
            or NotSupportedException;
}
