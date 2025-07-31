using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop.SyncRoot;
public static class SyncRootEventsExtensions
{
    private static (SyncRootCallback? Callback, CldApi.CF_CALLBACK_TYPE Type)[] ToPairs(this SyncRootEvents source) =>
        [
            (source.FetchData, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_FETCH_DATA),
            (source.CancelFetchData, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_CANCEL_FETCH_DATA),
            (source.FetchPlaceholders, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_FETCH_PLACEHOLDERS),
            (source.CancelFetchPlaceholders, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_CANCEL_FETCH_PLACEHOLDERS),
            (source.OnOpenCompletion, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_FILE_OPEN_COMPLETION),
            (source.OnCloseCompletion, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_FILE_CLOSE_COMPLETION),
            (source.OnRename, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_RENAME),
            (source.OnRenameCompletion, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_RENAME_COMPLETION),
            (source.OnDelete, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_DELETE),
            (source.OnDeleteCompletion, CldApi.CF_CALLBACK_TYPE.CF_CALLBACK_TYPE_NOTIFY_DELETE_COMPLETION),
        ];

    public static CldApi.CF_CALLBACK_REGISTRATION[] ToRegistrationArray(this SyncRootEvents source) =>
        source.ToPairs()
            .Where((x, t) => x.Callback is not null)
            .Select((x) => new CldApi.CF_CALLBACK_REGISTRATION
            {
                Type = x.Type,
                Callback = x.Callback!.Invoke,
            })
            .Append(CldApi.CF_CALLBACK_REGISTRATION.CF_CALLBACK_REGISTRATION_END)
            .ToArray();
}
