using Vanara.PInvoke;
using static Vanara.PInvoke.Ole32;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell;
public class ShellRegistrar(
  IEnumerable<IClassFactoryOf> factories,
  ILogger logger
) {
  public IReadOnlyList<uint> Register() {
    logger.LogDebug("Register shell extensions");

    var cookies = factories
      .Select((factory) => {
        Register(
          factory.Type.GUID,
          factory,
          out var cookie
        ).ThrowIfFailed($"Failed to register {factory.Type}");
        return cookie;
      })
      .ToArray();

    return cookies;
  }

  private HRESULT Register(Guid rclsId, object factory, out uint cookie) =>
    CoRegisterClassObject(
      rclsId,
      factory,
      CLSCTX.CLSCTX_LOCAL_SERVER,
      REGCLS.REGCLS_MULTIPLEUSE,
      out cookie
    );

  //[Obsolete("Switch to this if it seems the STA and/or CoInitialize are necessary")]
  public void RegisterUntilCancelled(CancellationToken stoppingToken) {
    var thread = new Thread(() => {
      CoInitializeEx(default, COINIT.COINIT_APARTMENTTHREADED).ThrowIfFailed();

      var cookies = Register();

      var stopEvent = stoppingToken.WaitHandle.SafeWaitHandle.DangerousGetHandle();
      CoWaitForMultipleHandles(COWAIT_FLAGS.COWAIT_DISPATCH_CALLS, Kernel32.INFINITE, 1, [stopEvent], out _);

      Revoke(cookies);

      CoUninitialize();
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
  }

  public void Revoke(IEnumerable<uint> cookies) {
    logger.LogDebug("Unregister shell extensions");
    foreach (var cookie in cookies)
    {
      CoRevokeClassObject(cookie);
    }
  }
}
