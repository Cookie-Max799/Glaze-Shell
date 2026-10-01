using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using GlazeShell.Core.Discovery;
using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Interop;
using GlazeShell.Windows.Shell;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Applications;

public sealed class WindowsApplicationLauncher : IApplicationLauncher
{
    private const uint NoErrorDialog = 0x00004004;
    private const int CloseTimeoutMilliseconds = 5000;
    private const int RestartDelayMilliseconds = 350;

    private static readonly TimeSpan ClosePollInterval = TimeSpan.FromMilliseconds(120);

    private readonly IApplicationManager _applications;
    private readonly IProcessInspector _processes;

    public WindowsApplicationLauncher(
        IApplicationManager applications,
        IProcessInspector processes)
    {
        ArgumentNullException.ThrowIfNull(applications);
        ArgumentNullException.ThrowIfNull(processes);

        _applications = applications;
        _processes = processes;
    }

    public async Task<ApplicationLaunchResult> LaunchAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        var application = await ResolveAsync(applicationId, cancellationToken).ConfigureAwait(false);

        if (application is null)
        {
            return ApplicationLaunchResult.Failure($"Unknown application '{applicationId}'.");
        }

        if (!string.IsNullOrWhiteSpace(application.ApplicationUserModelId))
        {
            return LaunchMsix(application, application.ApplicationUserModelId);
        }

        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            return await LaunchWin32Async(application, cancellationToken).ConfigureAwait(false);
        }

        return ApplicationLaunchResult.Failure($"Application '{application.Name}' has no launch identity.");
    }

    public async Task<bool> IsRunningAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        var application = await ResolveAsync(applicationId, cancellationToken).ConfigureAwait(false);

        if (application is null)
        {
            return false;
        }

        // Состояние процесса определяется по исполняемому файлу, а для MSIX — по каталогу
        // установки пакета, где живёт исполняемый файл приложения. Каталог определяется
        // разбором пути процесса (WindowsApps\<PackageFullName>), а не по значению из
        // реестра: для bundle-пакетов реестр указывает на neutral-вариант. Проверка по AUMID
        // не используется: она потребовала бы COM-вызова для каждого процесса (а
        // IApplicationActivationManager здесь создаётся на отдельном STA-потоке), что
        // несопоставимо дороже проверки по пути.
        return FindProcesses(application).Count > 0;
    }


    public async Task<bool> CloseAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        var application = await ResolveAsync(applicationId, cancellationToken).ConfigureAwait(false);

        if (application is null)
        {
            return false;
        }

        var processes = FindProcesses(application);

        if (processes.Count == 0)
        {
            return false;
        }

        var closed = false;

        foreach (var processId in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            closed |= TryClose(processId);
        }

        return closed;
    }

    public async Task<ApplicationLaunchResult> RestartAsync(string applicationId, CancellationToken cancellationToken = default)
    {
        await CloseAsync(applicationId, cancellationToken).ConfigureAwait(false);
        await WaitForExitAsync(applicationId, cancellationToken).ConfigureAwait(false);

        if (cancellationToken.IsCancellationRequested)
        {
            return ApplicationLaunchResult.Failure("Restart was cancelled.");
        }

        await Task.Delay(RestartDelayMilliseconds, cancellationToken).ConfigureAwait(false);
        return await LaunchAsync(applicationId, cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<int> FindProcesses(Application application)
    {
        if (!string.IsNullOrWhiteSpace(application.ExecutablePath))
        {
            return _processes.FindProcessesByPath(application.ExecutablePath);
        }

        if (!string.IsNullOrWhiteSpace(application.PackageFamilyName))
        {
            return _processes.FindProcessesByPackage(application.PackageFamilyName);
        }

        return Array.Empty<int>();
    }

    private async Task WaitForExitAsync(string applicationId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(CloseTimeoutMilliseconds);

        while (DateTime.UtcNow < deadline)
        {
            if (!await IsRunningAsync(applicationId, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(ClosePollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Application?> ResolveAsync(string applicationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        return await _applications.GetAsync(applicationId, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryClose(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.CloseMainWindow();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private static ApplicationLaunchResult LaunchMsix(Application application, string applicationUserModelId)
    {
        try
        {
            var processId = ComApartment.Run(() =>
            {
                var manager = ShellIdentifiers.CreateInstance<IApplicationActivationManager>(
                    in ShellIdentifiers.ClassApplicationActivationManager,
                    in ShellIdentifiers.InterfaceIApplicationActivationManager,
                    Ole32.ClsctxInProcServer | Ole32.ClsctxLocalServer);

                var arguments = string.IsNullOrWhiteSpace(application.Arguments) ? null : application.Arguments;
                var result = manager.ActivateApplication(applicationUserModelId, arguments, NoErrorDialog, out var pid);

                if (result < 0)
                {
                    Marshal.ThrowExceptionForHR(result);
                }

                return pid;
            });

            return ApplicationLaunchResult.Success(processId == 0 ? null : unchecked((int)processId));
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or NotSupportedException or ArgumentException)
        {
            return ApplicationLaunchResult.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"ActivateApplication failed for '{application.Name}': {exception.Message}"));
        }
    }

    private static async Task<ApplicationLaunchResult> LaunchWin32Async(Application application, CancellationToken cancellationToken)
    {
        var executablePath = application.ExecutablePath!;

        try
        {
            if (!File.Exists(executablePath))
            {
                return ApplicationLaunchResult.Failure(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Target '{executablePath}' does not exist."));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ApplicationLaunchResult.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"Target '{executablePath}' is not accessible: {exception.Message}"));
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = ResolveWorkingDirectory(application, executablePath),
            };

            if (!string.IsNullOrWhiteSpace(application.Arguments))
            {
                startInfo.Arguments = application.Arguments;
            }

            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return ApplicationLaunchResult.Failure($"'{application.Name}' did not start a process.");
            }

            return ApplicationLaunchResult.Success(process.Id);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or FileNotFoundException or NotSupportedException)
        {
            return ApplicationLaunchResult.Failure(string.Create(
                CultureInfo.InvariantCulture,
                $"Launching '{application.Name}' failed: {exception.Message}"));
        }
    }

    private static string ResolveWorkingDirectory(Application application, string executablePath)
    {
        if (!string.IsNullOrWhiteSpace(application.WorkingDirectory) && Directory.Exists(application.WorkingDirectory))
        {
            return application.WorkingDirectory;
        }

        var directory = Path.GetDirectoryName(executablePath);
        return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : Environment.CurrentDirectory;
    }
}

