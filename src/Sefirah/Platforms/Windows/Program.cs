using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Sefirah.Platforms.Windows.Interop;
using Sefirah.Platforms.Windows.Services;
using Windows.System;
using AppInstance = Microsoft.Windows.AppLifecycle.AppInstance;

namespace Sefirah.Platforms.Windows;
internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize the global System.Runtime.InteropServices.ComWrappers instance to use for WinRT
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Get current process
        var proc = Process.GetCurrentProcess();

        // Get app activation arguments
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // Only the hosted app process has package identity containing ".Hosted."
        var isHostedAppProcess = Package.Current.Id.Name.Contains(".Hosted.", StringComparison.OrdinalIgnoreCase);

        if (isHostedAppProcess)
        {
            HandleHostedAppLaunch(args);
            return;
        }

        // Get active process PID
        var activePid = ApplicationData.Current.LocalSettings.Values.Get("INSTANCE_ACTIVE", -1);

        // Get current active PID's instance
        var instance = AppInstance.FindOrRegisterForKey(activePid.ToString());

        // If that instance is not current window's own, the app will redirect to that instance
        // so that the app would not create more than one window
        if (!instance.IsCurrent)
        {
            RedirectActivationTo(instance, activatedArgs);

            // End process
            return;
        }

        // Get this current instance
        var currentInstance = AppInstance.FindOrRegisterForKey((-proc.Id).ToString());

        if (currentInstance.IsCurrent)
            currentInstance.Activated += OnActivated;

        // Set this current active process's PID
        ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = -proc.Id;

        // Start WinUI
        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);

            _ = new App();
        });
    }

    public static void RedirectActivationTo(AppInstance keyInstance, AppActivationArguments args)
    {
        // WINUI3: https://github.com/microsoft/WindowsAppSDK/issues/1709

        IntPtr redirectEventHandle = InteropHelpers.CreateEvent(IntPtr.Zero, true, false, null);

        Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().Wait();
            InteropHelpers.SetEvent(redirectEventHandle);
        });

        uint CWMO_DEFAULT = 0;
        uint INFINITE = 0xFFFFFFFF;

        _ = InteropHelpers.CoWaitForMultipleObjects(CWMO_DEFAULT, INFINITE, 1, [redirectEventHandle], out uint handleIndex);
    }

    private static async void OnActivated(object? sender, AppActivationArguments args)
    {
        if (App.Current is App thisApp)
        {
            await thisApp.OnActivatedAsync(args);
        }
    }

    private static void HandleHostedAppLaunch(string[] args)
    {
        var package = GetHostedAppPackage(args);
        if (string.IsNullOrEmpty(package)) return;
        var uri = new Uri($"sefirah://{package}");
        Launcher.LaunchUriAsync(uri).AsTask().GetAwaiter().GetResult();
    }

    private static string? GetHostedAppPackage(string[] args)
    {
        var prefix = WindowsAppShortcutService.HostedPackageParamPrefix;
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return arg[prefix.Length..].Trim();
        }
        return null;
    }
}
