using System.Globalization;
using System.Text;

namespace GlazeShell.Windows.Tests;

internal sealed class ShellLinkBuilder
{
    private const int HeaderSize = 0x4C;
    private const int LinkInfoHeaderSize = 0x1C;

    private static readonly Encoding AnsiEncoding = ResolveAnsiEncoding();

    private const uint FlagHasLinkTargetIdList = 0x00000001;
    private const uint FlagHasLinkInfo = 0x00000002;
    private const uint FlagHasName = 0x00000004;
    private const uint FlagHasRelativePath = 0x00000008;
    private const uint FlagHasWorkingDir = 0x00000010;
    private const uint FlagHasArguments = 0x00000020;
    private const uint FlagHasIconLocation = 0x00000040;
    private const uint FlagIsUnicode = 0x00000080;

    private readonly bool _useRelativePath;
    private readonly bool _includeTarget;
    private readonly bool _includeTargetIdList;

    private ShellLinkBuilder(bool useRelativePath, bool includeTarget, bool includeTargetIdList = false)
    {
        _useRelativePath = useRelativePath;
        _includeTarget = includeTarget;
        _includeTargetIdList = includeTargetIdList;
    }

    public string? Name { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? IconLocation { get; set; }

    public int IconIndex { get; set; }

    public static ShellLinkBuilder WithLinkInfo() => new(false, true);

    public static ShellLinkBuilder WithLinkInfoAndTargetIdList() => new(false, true, includeTargetIdList: true);

    public static ShellLinkBuilder WithRelativePath() => new(true, true);

    public static ShellLinkBuilder WithoutTarget() => new(false, false);

    public void WriteTo(string shortcutPath, string targetPath, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        File.WriteAllBytes(shortcutPath, BuildBytes(targetPath, displayName));
    }

    public byte[] BuildBytes(string targetPath, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var name = Name ?? displayName ?? (_includeTarget ? Path.GetFileNameWithoutExtension(targetPath) : null);
        var iconPath = IconLocation ?? targetPath;
        var relativePath = _useRelativePath && _includeTarget ? targetPath : null;

        var flags = FlagIsUnicode
                    | (_includeTargetIdList ? FlagHasLinkTargetIdList : 0u)
                    | (!_includeTarget ? 0u : _useRelativePath ? 0u : FlagHasLinkInfo)
                    | FlagHasName
                    | (!_includeTarget || !_useRelativePath ? 0u : FlagHasRelativePath)
                    | (WorkingDirectory is null ? 0u : FlagHasWorkingDir)
                    | (Arguments is null ? 0u : FlagHasArguments)
                    | (iconPath is null ? 0u : FlagHasIconLocation);

        var body = new List<byte>();

        if (_includeTargetIdList)
        {
            body.AddRange(BuildTargetIdList());
        }

        if (_includeTarget && !_useRelativePath)
        {
            body.AddRange(BuildLinkInfo(targetPath));
        }

        AppendString(body, name);
        AppendString(body, relativePath);
        AppendString(body, WorkingDirectory);
        AppendString(body, Arguments);
        AppendString(body, iconPath);

        var file = new byte[HeaderSize + body.Count];
        var span = file.AsSpan();

        WriteUInt32(span, 0, HeaderSize);
        WriteGuid(span, 4, new Guid("00021401-0000-0000-C000-000000000046"));
        WriteUInt32(span, 0x14, flags);
        WriteUInt32(span, 0x18, 0x20);
        WriteUInt32(span, 0x38, unchecked((uint)IconIndex));
        WriteUInt32(span, 0x3C, 1);

        body.CopyTo(file, HeaderSize);
        return file;
    }

    private static byte[] BuildTargetIdList()
    {
        const int itemIdSize = 0x14;
        const int idListSize = itemIdSize + 2;

        var idList = new byte[2 + idListSize];
        WriteUInt16(idList, 0, idListSize);
        idList[2] = 0x1F;
        WriteUInt16(idList, 2 + itemIdSize, 0);
        return idList;
    }

    private static byte[] BuildLinkInfo(string localBasePath)
    {
        // Windows хранит LocalBasePath по ANSI-смещению в однобайтовой кодировке,
        // а CommonPathSuffix оставляет пустым (MS-SHLLINK, раздел LinkInfo).
        var basePathBytes = ToAnsi(localBasePath);
        var suffixBytes = new byte[] { 0 };

        var basePathOffset = LinkInfoHeaderSize;
        var suffixOffset = basePathOffset + basePathBytes.Length;
        var total = suffixOffset + suffixBytes.Length;

        var info = new byte[total];
        var span = info.AsSpan();

        WriteUInt32(span, 0x00, (uint)total);
        WriteUInt32(span, 0x04, LinkInfoHeaderSize);
        WriteUInt32(span, 0x08, 0x1);
        WriteUInt32(span, 0x0C, 0);
        WriteUInt32(span, 0x10, (uint)basePathOffset);
        WriteUInt32(span, 0x14, 0);
        WriteUInt32(span, 0x18, (uint)suffixOffset);

        basePathBytes.CopyTo(info.AsSpan(basePathOffset));
        suffixBytes.CopyTo(info.AsSpan(suffixOffset));

        return info;
    }

    private static byte[] ToAnsi(string value)
    {
        var bytes = AnsiEncoding.GetBytes(value);
        var result = new byte[bytes.Length + 1];
        bytes.CopyTo(result, 0);
        return result;
    }

    private static Encoding ResolveAnsiEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Encoding.Latin1;
        }
    }

    private static byte[] ToUnicode(string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);
        var result = new byte[bytes.Length + 2];
        bytes.CopyTo(result, 0);
        return result;
    }

    private static void AppendString(List<byte> target, string? value)
    {
        if (value is null)
        {
            return;
        }

        target.AddRange(ToUnicode(value));
    }

    private static void WriteUInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteUInt32(Span<byte> span, int offset, uint value)
    {
        span[offset] = (byte)value;
        span[offset + 1] = (byte)(value >> 8);
        span[offset + 2] = (byte)(value >> 16);
        span[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteGuid(Span<byte> span, int offset, Guid value)
    {
        var bytes = value.ToByteArray();
        bytes.CopyTo(span[offset..]);
    }
}
