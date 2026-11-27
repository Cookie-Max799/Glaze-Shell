using System.Security;
using Microsoft.Win32;

namespace GlazeShell.Windows.Switching;

public sealed record VirtualDesktopSlot(string Name, bool IsCurrent);

public static class VirtualDesktopStrip
{
    private static readonly VirtualDesktopSlot[] Fallback = [new("Текущий рабочий стол", true)];

    public static IReadOnlyList<VirtualDesktopSlot> Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops");

            if (key is null || key.GetValue("VirtualDesktopIDs") is not byte[] ids ||
                ids.Length == 0 || ids.Length % 16 != 0)
            {
                return Fallback;
            }

            var current = key.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } bytes
                ? new Guid(bytes)
                : Guid.Empty;
            var slots = new List<VirtualDesktopSlot>(ids.Length / 16);

            for (var offset = 0; offset < ids.Length; offset += 16)
            {
                var id = new Guid(ids.AsSpan(offset, 16));
                using var desktop = key.OpenSubKey($@"Desktops\{id:B}");
                var name = desktop?.GetValue("Name") as string;
                slots.Add(new VirtualDesktopSlot(
                    string.IsNullOrWhiteSpace(name) ? $"Рабочий стол {slots.Count + 1}" : name,
                    id == current || ids.Length == 16));
            }

            return slots;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Fallback;
        }
    }
}
