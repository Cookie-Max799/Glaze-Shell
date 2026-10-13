using GlazeShell.Core.Discovery;

namespace GlazeShell.Core.Interfaces;

public interface IApplicationLauncher
{
    Task<ApplicationLaunchResult> LaunchAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<bool> IsRunningAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<bool> CloseAsync(string applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Принудительно завершает приложение и его фоновые процессы (аналог «Снять задачу»).
    /// В отличие от <see cref="CloseAsync"/> здесь <c>TerminateProcess</c> применяется
    /// осознанно, поэтому вызывать метод следует только после явного подтверждения
    /// пользователя при неудаче вежливого закрытия.
    /// </summary>
    Task<bool> TerminateAsync(string applicationId, CancellationToken cancellationToken = default);

    Task<ApplicationLaunchResult> RestartAsync(string applicationId, CancellationToken cancellationToken = default);
}
