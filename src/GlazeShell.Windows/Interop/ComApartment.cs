using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using GlazeShell.Windows.Win32;

namespace GlazeShell.Windows.Interop;

internal static class ComApartment
{
    private const uint CoInitApartmentThreaded = 0x00000002;
    private const uint CoinitDisableOle1Dde = 0x00000004;

    internal static T Run<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        return Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
            ? Execute(action)
            : ExecuteOnDedicatedThread(() => Execute(action));
    }

    internal static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            EnsureInitialized();
            action();
            return;
        }

        ExecuteOnDedicatedThread<object?>(() =>
        {
            EnsureInitialized();
            action();
            return null;
        });
    }

    internal static string Describe(int hresult) =>
        string.Create(CultureInfo.InvariantCulture, $"0x{unchecked((uint)hresult):X8}");

    internal static void CoTaskMemFree(nint pointer)
    {
        if (pointer != 0)
        {
            Ole32.CoTaskMemFree(pointer);
        }
    }

    private static T Execute<T>(Func<T> action)
    {
        EnsureInitialized();
        return action();
    }

    private static T ExecuteOnDedicatedThread<T>(Func<T> action)
    {
        Exception? failure = null;
        var captured = default(T)!;

        var thread = new Thread(() =>
        {
            var initialized = Ole32.CoInitializeEx(0, CoInitApartmentThreaded | CoinitDisableOle1Dde);

            try
            {
                if (initialized < 0)
                {
                    Marshal.ThrowExceptionForHR(initialized);
                }

                captured = action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                if (initialized >= 0)
                {
                    Ole32.CoUninitialize();
                }
            }
        })
        {
            IsBackground = true,
            Name = "GlazeShell.Shell",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        return captured;
    }

    private static void EnsureInitialized()
    {
        var result = Ole32.CoInitializeEx(0, CoInitApartmentThreaded | CoinitDisableOle1Dde);

        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }
}
