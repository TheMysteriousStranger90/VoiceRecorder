using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VoiceRecorder.Services;

[SupportedOSPlatform("windows")]
internal sealed partial class WindowsInstanceService : IDisposable
{
    private const string MutexName = "Global\\AzioVoiceRecorder_SingleInstance";

    private Mutex? _mutex;
    private bool _disposed;
    private bool _ownsMutex;

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int dwProcessId);

    private const int SW_RESTORE = 9;
    private const int ASFW_ANY = -1;

    public bool TryStart()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out _ownsMutex);

            if (!_ownsMutex)
            {
                ActivateExistingWindow();
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error acquiring mutex: {ex.Message}");
            return true;
        }
    }

    private static void ActivateExistingWindow()
    {
        try
        {
            AllowSetForegroundWindow(ASFW_ANY);

            var currentProcessName = Process.GetCurrentProcess().ProcessName;
            var processes = Process.GetProcessesByName(currentProcessName);

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    var hWnd = process.MainWindowHandle;
                    if (hWnd != IntPtr.Zero)
                    {
                        if (IsIconic(hWnd))
                        {
                            ShowWindow(hWnd, SW_RESTORE);
                        }

                        SetForegroundWindow(hWnd);

                        Debug.WriteLine($"Activated existing window (PID: {process.Id})");
                        break;
                    }
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error activating existing window: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_ownsMutex && _mutex != null)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
            // Mutex has already been released or not owned
        }
        finally
        {
            _mutex?.Dispose();
            _mutex = null;
            _disposed = true;
        }
    }
}
