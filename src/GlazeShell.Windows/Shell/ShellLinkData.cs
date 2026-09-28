using System.Text;

namespace GlazeShell.Windows.Shell;

internal sealed class ShellLinkData
{
    internal const int HeaderSize = 0x4C;

    private const uint FlagHasLinkTargetIdList = 0x00000001;
    private const uint FlagHasLinkInfo = 0x00000002;
    private const uint FlagHasName = 0x00000004;
    private const uint FlagHasRelativePath = 0x00000008;
    private const uint FlagHasWorkingDir = 0x00000010;
    private const uint FlagHasArguments = 0x00000020;
    private const uint FlagHasIconLocation = 0x00000040;
    private const uint FlagIsUnicode = 0x00000080;
    private const uint FlagForceNoLinkInfo = 0x00000100;
    private const uint FlagPreferEnvironmentPath = 0x00020000;

    private ShellLinkData()
    {
    }

    internal uint LinkFlags { get; private init; }

    internal string? Name { get; private init; }

    internal string? RelativePath { get; private init; }

    internal string? WorkingDirectory { get; private init; }

    internal string? Arguments { get; private init; }

    internal string? IconLocation { get; private init; }

    internal int IconIndex { get; private init; }

    internal string? LocalBasePath { get; private init; }

    internal string? CommonPathSuffix { get; private init; }

    internal bool HasDarwinData { get; private init; }

    internal bool PrefersEnvironmentPath => (LinkFlags & FlagPreferEnvironmentPath) != 0;

    internal static ShellLinkData? TryRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        byte[] bytes;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists || info.Length < HeaderSize)
            {
                return null;
            }

            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }

        return TryParse(bytes);
    }

    internal static ShellLinkData? TryParse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length < HeaderSize || ReadUInt32(bytes, 0) != HeaderSize)
        {
            return null;
        }

        var flags = ReadUInt32(bytes, 0x14);
        var unicode = (flags & FlagIsUnicode) != 0;
        var offset = HeaderSize;

        if ((flags & FlagHasLinkTargetIdList) != 0)
        {
            if (!TrySkipIdList(bytes, ref offset))
            {
                return null;
            }
        }

        string? localBasePath = null;
        string? commonPathSuffix = null;

        if ((flags & FlagHasLinkInfo) != 0 && (flags & FlagForceNoLinkInfo) == 0)
        {
            ReadLinkInfo(bytes, ref offset, unicode, out localBasePath, out commonPathSuffix);
        }

        string? name = null;
        string? relativePath = null;
        string? workingDirectory = null;
        string? arguments = null;
        string? iconLocation = null;

        if ((flags & FlagHasName) != 0)
        {
            name = ReadString(bytes, ref offset, unicode);
        }

        if ((flags & FlagHasRelativePath) != 0)
        {
            relativePath = ReadString(bytes, ref offset, unicode);
        }

        if ((flags & FlagHasWorkingDir) != 0)
        {
            workingDirectory = ReadString(bytes, ref offset, unicode);
        }

        if ((flags & FlagHasArguments) != 0)
        {
            arguments = ReadString(bytes, ref offset, unicode);
        }

        if ((flags & FlagHasIconLocation) != 0)
        {
            iconLocation = ReadString(bytes, ref offset, unicode);
        }

        return new ShellLinkData
        {
            LinkFlags = flags,
            Name = Clean(name),
            RelativePath = Clean(relativePath),
            WorkingDirectory = Clean(workingDirectory),
            Arguments = Clean(arguments),
            IconLocation = Clean(iconLocation),
            IconIndex = unchecked((int)ReadUInt32(bytes, 0x38)),
            LocalBasePath = Clean(localBasePath),
            CommonPathSuffix = Clean(commonPathSuffix),
            HasDarwinData = DetectDarwinData(bytes, offset),
        };
    }

    internal string? ResolveTargetPath(string? shortcutPath)
    {
        if (!string.IsNullOrWhiteSpace(LocalBasePath))
        {
            return ExpandEnvironment(LocalBasePath);
        }

        if (!string.IsNullOrWhiteSpace(RelativePath))
        {
            var resolved = ResolveRelative(shortcutPath, RelativePath);

            if (resolved is not null)
            {
                return resolved;
            }
        }

        if (!string.IsNullOrWhiteSpace(Name) && HasDarwinData)
        {
            return null;
        }

        return null;
    }

    internal static string ExpandEnvironment(string path)
    {
        if (path.IndexOf('%') < 0)
        {
            return path;
        }

        try
        {
            return Environment.ExpandEnvironmentVariables(path);
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static string? ResolveRelative(string? shortcutPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(shortcutPath))
        {
            return null;
        }

        try
        {
            var directory = Path.GetDirectoryName(shortcutPath);

            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Trim();
            return Path.GetFullPath(Path.Combine(directory, normalized));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool TrySkipIdList(byte[] bytes, ref int offset)
    {
        if (offset + 2 > bytes.Length)
        {
            return false;
        }

        var size = ReadUInt16(bytes, offset);

        if (size < 2 || offset + size > bytes.Length)
        {
            return false;
        }

        offset += size;
        return true;
    }

    private static void ReadLinkInfo(
        byte[] bytes,
        ref int offset,
        bool unicode,
        out string? localBasePath,
        out string? commonPathSuffix)
    {
        localBasePath = null;
        commonPathSuffix = null;

        if (offset + 0x1C > bytes.Length)
        {
            return;
        }

        var linkInfoSize = ReadUInt32(bytes, offset);
        var localBasePathOffset = ReadUInt32(bytes, offset + 0x10);
        var commonPathSuffixOffset = ReadUInt32(bytes, offset + 0x14);

        if (linkInfoSize < 0x1C || linkInfoSize > bytes.Length - offset)
        {
            return;
        }

        if (localBasePathOffset != 0 && offset + (int)localBasePathOffset < bytes.Length)
        {
            localBasePath = ReadString(bytes, offset + (int)localBasePathOffset, unicode);
        }

        if (commonPathSuffixOffset != 0 && offset + (int)commonPathSuffixOffset < bytes.Length)
        {
            commonPathSuffix = ReadString(bytes, offset + (int)commonPathSuffixOffset, unicode);
        }

        offset += (int)linkInfoSize;
    }

    private static bool DetectDarwinData(byte[] bytes, int offset)
    {
        if (offset + 4 > bytes.Length)
        {
            return false;
        }

        var signature = ReadUInt32(bytes, offset);
        return signature == 0xA0000001;
    }

    private static string? ReadString(byte[] bytes, int offset, bool unicode) =>
        unicode
            ? Encoding.Unicode.GetString(bytes, offset, FindTerminator(bytes, offset, 2) - offset)
            : Encoding.Latin1.GetString(bytes, offset, FindTerminator(bytes, offset, 1) - offset);

    private static string? ReadString(byte[] bytes, ref int offset, bool unicode)
    {
        if (offset >= bytes.Length)
        {
            offset = bytes.Length;
            return null;
        }

        var start = offset;
        var end = FindTerminator(bytes, start, unicode ? 2 : 1);
        var value = unicode
            ? Encoding.Unicode.GetString(bytes, start, end - start)
            : Encoding.Latin1.GetString(bytes, start, end - start);
        offset = end + (unicode ? 2 : 1);
        return value;
    }

    private static int FindTerminator(byte[] bytes, int offset, int width)
    {
        var limit = bytes.Length - width;

        for (var index = offset; index <= limit; index += width)
        {
            if (width == 1)
            {
                if (bytes[index] == 0)
                {
                    return index;
                }
            }
            else if (bytes[index] == 0 && bytes[index + 1] == 0)
            {
                return index;
            }
        }

        return Math.Max(offset, bytes.Length - width);
    }

    private static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim('\0', ' ', '\t', '\r', '\n');
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));

    private static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
}
