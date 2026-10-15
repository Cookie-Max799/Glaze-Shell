using GlazeShell.Core.Interfaces;
using GlazeShell.Core.Models;
using GlazeShell.Windows.Applications;
using GlazeShell.Windows.Shell;

namespace GlazeShell.Windows.Tests;

[TestClass]
public sealed class WindowsApplicationLauncherTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _root = TestShortcutFactory.CreateTemporaryRoot();
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestShortcutFactory.DeleteRoot(_root);
    }

    [TestMethod]
    public async Task LaunchFailsForUnknownApplication()
    {
        var launcher = CreateLauncher(new FakeApplicationManager());

        var result = await launcher.LaunchAsync("missing");

        Assert.IsFalse(result.Started);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public async Task LaunchFailsWhenTargetIsMissing()
    {
        var manager = new FakeApplicationManager().Add(
            new Application("app:missing", "Missing", ApplicationType.Win32, Path.Combine(_root, "nope.exe")));

        var launcher = CreateLauncher(manager);

        var result = await launcher.LaunchAsync("app:missing");

        Assert.IsFalse(result.Started);
        Assert.Contains("does not exist", result.Error!);
    }

    [TestMethod]
    public async Task LaunchStartsAShortLivedProcess()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec");

        if (string.IsNullOrWhiteSpace(comspec) || !File.Exists(comspec))
        {
            Assert.Inconclusive("ComSpec is unavailable.");
            return;
        }

        var manager = new FakeApplicationManager().Add(
            new Application("app:exit", "Exit", ApplicationType.Win32, comspec, arguments: "/c exit 0"));

        var launcher = CreateLauncher(manager);

        var result = await launcher.LaunchAsync("app:exit");

        Assert.IsTrue(result.Started, result.Error);
        Assert.IsNotNull(result.ProcessId);
    }

    [TestMethod]
    public async Task IsRunningDetectsTheCurrentProcess()
    {
        var executable = TestShortcutFactory.SelfExecutablePath;
        var manager = new FakeApplicationManager().Add(new Application("app:self", "Self", ApplicationType.Win32, executable));

        var launcher = CreateLauncher(manager);

        var running = await launcher.IsRunningAsync("app:self");

        Assert.IsTrue(running);
    }

    [TestMethod]
    public async Task CloseReturnsFalseWhenNothingIsRunning()
    {
        var executable = TestShortcutFactory.SelfExecutablePath;
        var manager = new FakeApplicationManager().Add(new Application("app:idle", "Idle", ApplicationType.Win32, executable));

        var launcher = CreateLauncher(manager, new StubProcessInspector());

        Assert.IsFalse(await launcher.CloseAsync("app:idle"));
    }

    [TestMethod]
    public async Task TerminateStopsARunningProcessThatHasNoWindow()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec");

        if (string.IsNullOrWhiteSpace(comspec) || !File.Exists(comspec))
        {
            Assert.Inconclusive("ComSpec is unavailable.");
            return;
        }

        // Копия cmd.exe в уникальном каталоге: путь исполняемого файла не совпадает
        // с другими cmd.exe, поэтому инспектор найдёт ровно наш процесс, а живучесть
        // без окна (ping держит консоль) делает вежливое закрытие неприменимым — как раз
        // сценарий принудительного завершения.
        var copy = Path.Combine(_root, "glaze-terminate-test.exe");
        File.Copy(comspec, copy);

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(copy, "/c ping -n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Assert.IsNotNull(process);

        var application = new Application("app:terminate", "Terminate", ApplicationType.Win32, copy);
        var launcher = CreateLauncher(new FakeApplicationManager().Add(application));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await launcher.IsRunningAsync("app:terminate") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(await launcher.IsRunningAsync("app:terminate"), "The helper process should be detected by path.");

        var terminated = await launcher.TerminateAsync("app:terminate");

        Assert.IsTrue(terminated, "The process should be gone after forced termination.");
        Assert.IsTrue(process!.WaitForExit(5000), "The process should have exited.");
    }

    [TestMethod]
    public void ProcessInspectorFindsTheCurrentProcessByPath()
    {
        var inspector = new ProcessInspector();

        var matches = inspector.FindProcessesByPath(TestShortcutFactory.SelfExecutablePath);

        Assert.Contains(Environment.ProcessId, matches);
    }

    [TestMethod]
    public async Task IsRunningUsesThePackageLocationForMsixApplications()
    {
        var app = new Application(
            "msix:test",
            "Test",
            ApplicationType.Msix,
            packageFamilyName: "Contoso.Example_1234567890abc",
            applicationUserModelId: "Contoso.Example_1234567890abc!App");

        var inspector = new StubProcessInspector { ByPackage = [4242] };
        var launcher = CreateLauncher(new FakeApplicationManager().Add(app), inspector);

        Assert.IsTrue(await launcher.IsRunningAsync("msix:test"));
    }

    [TestMethod]
    public async Task IsRunningDetectsLauncherSpawnedProcessFromSubdirectory()
    {
        var comspec = Environment.GetEnvironmentVariable("ComSpec");

        if (string.IsNullOrWhiteSpace(comspec) || !File.Exists(comspec))
        {
            Assert.Inconclusive("ComSpec is unavailable.");
            return;
        }

        // Лаунчер лежит в корне каталога установки и не работает, а настоящий процесс с тем
        // же именем образа запущен из подкаталога — как у Squirrel (Discord). Точное
        // совпадение пути не находит процесс, поэтому срабатывает расширение по каталогу.
        const string imageName = "glaze-launcher-probe.exe";
        var appDirectory = Path.Combine(_root, "App");
        var nestedDirectory = Path.Combine(appDirectory, "app-1.0.0");
        Directory.CreateDirectory(nestedDirectory);

        var runningExecutable = Path.Combine(nestedDirectory, imageName);
        File.Copy(comspec, runningExecutable);

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(runningExecutable, "/c ping -n 30 127.0.0.1")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        Assert.IsNotNull(process);

        var application = new Application("app:launcher", "Launcher", ApplicationType.Win32, Path.Combine(appDirectory, imageName));
        var launcher = CreateLauncher(new FakeApplicationManager().Add(application));

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!await launcher.IsRunningAsync("app:launcher") && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.IsTrue(await launcher.IsRunningAsync("app:launcher"), "The launcher-spawned process should be detected by directory.");

        var terminated = await launcher.TerminateAsync("app:launcher");

        Assert.IsTrue(terminated, "The launcher-spawned process should be stopped.");
        Assert.IsTrue(process!.WaitForExit(5000), "The process should have exited.");
    }

    [TestMethod]
    public async Task DirectoryExpansionIsNotUsedForSystemDirectory()
    {
        // Корень Windows не считается «подкаталогом» самого себя, но при этом объединяет
        // все системные процессы: расширение по каталогу здесь зацепило бы систему целиком.
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        if (string.IsNullOrWhiteSpace(windows))
        {
            Assert.Inconclusive("The Windows directory is unavailable.");
            return;
        }

        var inspector = new StubProcessInspector { ByImageInDirectory = [1], ByDirectory = [1] };
        var application = new Application(
            "app:system",
            "System",
            ApplicationType.Win32,
            Path.Combine(windows, "notepad.exe"));
        var launcher = CreateLauncher(new FakeApplicationManager().Add(application), inspector);

        Assert.IsFalse(await launcher.IsRunningAsync("app:system"));
        Assert.IsEmpty(inspector.ImageInDirectoryRequests);
    }

    [TestMethod]
    public async Task DirectoryExpansionIsNotUsedForSharedInstallRoot()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        if (string.IsNullOrWhiteSpace(programFiles))
        {
            Assert.Inconclusive("Program Files is unavailable.");
            return;
        }

        var inspector = new StubProcessInspector { ByImageInDirectory = [1], ByDirectory = [1] };
        var application = new Application(
            "app:shared",
            "Shared",
            ApplicationType.Win32,
            Path.Combine(programFiles, "app.exe"));
        var launcher = CreateLauncher(new FakeApplicationManager().Add(application), inspector);

        Assert.IsFalse(await launcher.TerminateAsync("app:shared"));
        Assert.IsEmpty(inspector.DirectoryRequests);
        Assert.IsEmpty(inspector.ImageInDirectoryRequests);
    }

    [TestMethod]
    public async Task DirectoryExpansionIsUsedForApplicationSubdirectory()
    {
        // Подкаталог конкретной программы остаётся безопасным: фоновые процессы
        // приложения из подкаталога (AcroCEF и подобные) по-прежнему находятся.
        var inspector = new StubProcessInspector();
        var application = new Application(
            "app:subdirectory",
            "Subdirectory",
            ApplicationType.Win32,
            Path.Combine(_root, "App", "app.exe"));
        var launcher = CreateLauncher(new FakeApplicationManager().Add(application), inspector);

        Assert.IsFalse(await launcher.TerminateAsync("app:subdirectory"));
        Assert.Contains(Path.Combine(_root, "App"), inspector.DirectoryRequests);
    }

    private static WindowsApplicationLauncher CreateLauncher(
        FakeApplicationManager manager,
        IProcessInspector? inspector = null)
    {
        return new WindowsApplicationLauncher(
            manager,
            inspector ?? new ProcessInspector());
    }
}

