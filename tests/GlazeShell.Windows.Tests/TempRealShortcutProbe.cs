using System.Globalization;
using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class TempRealShortcutProbe
{
    [TestMethod]
    public void ProbeRealStartMenuShortcuts()
    {
        var roots = new[]
        {
            @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs",
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        };

        var sb = new System.Text.StringBuilder();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"MISSING {root}"));
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                var data = ShellLinkData.TryRead(file);
                var managedTarget = data?.ResolveTargetPath(file);
                var resolved = ShellLinkResolver.TryResolve(file, out var target, out var error);
                sb.AppendLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Path.GetFileName(file)} | data={data is not null} | managed={managedTarget ?? "<null>"} | resolve={resolved} target={target?.Path ?? "<null>"} strategy={target?.Strategy} err={error}"));
            }
        }

        File.WriteAllText(@"C:\Users\debil\AppData\Local\Temp\opencode\lnk-probe.txt", sb.ToString());
    }
}
