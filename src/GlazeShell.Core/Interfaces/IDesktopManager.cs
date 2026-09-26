using GlazeShell.Core.Models;

namespace GlazeShell.Core.Interfaces;

public interface IDesktopManager
{
    DesktopLayout GetLayout();

    void SetLayout(DesktopLayout layout);
}
