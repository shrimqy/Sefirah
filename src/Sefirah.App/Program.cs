using Sefirah.App.Extensions;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.Storage;
using Windows.ApplicationModel.Activation;

namespace Sefirah.App;

public class Program
{
    private static nint redirectEventHandle = nint.Zero;

    [STAThread]
    public static void Main()
    {
        // Initialize the global System.Runtime.InteropServices.ComWrappers instance to use for WinRT
        WinRT.ComWrappersSupport.InitializeComWrappers();

        // Get current process
        var proc = Process.GetCurrentProcess();

        // Get app activation arguments
        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

        // Get current active PID
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
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);

            _ = new App();
        });
    }

    public static void RedirectActivationTo(AppInstance keyInstance, AppActivationArguments args)
    {
        // WINUI3: https://github.com/microsoft/WindowsAppSDK/issues/1709

        redirectEventHandle = InteropHelpers.CreateEvent(nint.Zero, true, false, null);

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
            if (args.Kind == ExtendedActivationKind.ShareTarget)
            {
                await thisApp.HandleShareTargetActivation(args.Data as ShareTargetActivatedEventArgs);
            }
            else
            {
                await thisApp.OnActivatedAsync(args);
            }
        }
    }
}