using System.Globalization;
using System.Text;

namespace GlazeShell.Windows.Tests;

internal static class TestShortcutFactory
{
    public static string CreateTemporaryRoot()
    {
        var name = "glazeshell-tests-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var root = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(root);
        return root;
    }

    public static void DeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static string CreateShortcut(string shortcutPath, string targetPath, string? description = null, string? arguments = null)
    {
        var builder = ShellLinkBuilder.WithLinkInfo();
        builder.Name = description ?? "Glaze Shell test shortcut";
        builder.Arguments = arguments ?? string.Empty;
        builder.WorkingDirectory = Path.GetDirectoryName(targetPath);
        builder.IconLocation = targetPath;
        builder.WriteTo(shortcutPath, targetPath);
        return shortcutPath;
    }

    public static string SelfExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("The test host executable path is unavailable.");
            }

            return path;
        }
    }

    public static string NonExecutableScriptPath(string root)
    {
        var script = Path.Combine(root, "helper.cmd");
        File.WriteAllText(script, "@echo off", Encoding.UTF8);
        return script;
    }
}
