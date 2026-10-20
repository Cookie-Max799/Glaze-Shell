using System.Runtime.InteropServices;

namespace GlazeShell.Windows.Audio;

/// <summary>Направление потока устройства (Core Audio, <c>EDataFlow</c>).</summary>
internal enum AudioDataFlow
{
    Render = 0,
    Capture = 1,
    All = 2,
}

/// <summary>Роль устройства по умолчанию (Core Audio, <c>ERole</c>).</summary>
internal enum AudioRole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2,
}

/// <summary>
/// Перечислитель звуковых устройств (<c>IMMDeviceEnumerator</c>). Объявлены только
/// используемые методы: у интерфейсов на <c>IUnknown</c> важен порядок слотов vtable,
/// а не полнота набора.
/// </summary>
[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(AudioDataFlow dataFlow, uint stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(
        AudioDataFlow dataFlow,
        AudioRole role,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice endpoint);

    [PreserveSig]
    int GetDevice(
        [MarshalAs(UnmanagedType.LPWStr)] string id,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);

    [PreserveSig]
    int RegisterEndpointNotificationCallback(nint client);

    [PreserveSig]
    int UnregisterEndpointNotificationCallback(nint client);
}

/// <summary>Звуковое устройство (<c>IMMDevice</c>).</summary>
[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(
        ref Guid interfaceId,
        uint classContext,
        nint activationParameters,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);

    [PreserveSig]
    int OpenPropertyStore(uint access, out nint properties);

    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

    [PreserveSig]
    int GetState(out uint state);
}

/// <summary>
/// Громкость конечного устройства (<c>IAudioEndpointVolume</c>). Объявлены методы до
/// <c>GetMute</c> включительно: порядок слотов vtable сохранён, поэтому вызовы попадают
/// в нужные функции.
/// </summary>
[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(nint client);

    [PreserveSig]
    int UnregisterControlChangeNotify(nint client);

    [PreserveSig]
    int GetChannelCount(out uint channelCount);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDb, nint eventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, nint eventContext);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDb);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(uint channel, float levelDb, nint eventContext);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint channel, float level, nint eventContext);

    [PreserveSig]
    int GetChannelVolumeLevel(uint channel, out float levelDb);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint channel, out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, nint eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
