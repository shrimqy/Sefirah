using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Dialogs;

namespace Sefirah.ViewModels;

public sealed partial class CallsPageViewModel : BaseViewModel
{
    #region Services
    private readonly IPhoneLineService phoneLineService = Ioc.Default.GetRequiredService<IPhoneLineService>();
    private readonly IBluetoothPairingService bluetoothPairingService = Ioc.Default.GetRequiredService<IBluetoothPairingService>();
    private readonly IDeviceManager deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly CallLogRepository callLogRepository = Ioc.Default.GetRequiredService<CallLogRepository>();
    private readonly ContactRepository contactRepository = Ioc.Default.GetRequiredService<ContactRepository>();
    #endregion

    public ObservableCollection<CallLog> CallLogs { get; } = [];

    #region Properties

    public PairedDevice? ActiveDevice => deviceManager.ActiveDevice;
    public bool ShowCallLogEmpty => !IsLoadingCallLogs && CallLogs.Count == 0;
    public bool ShowCallLogList => !IsLoadingCallLogs && CallLogs.Count > 0;

    [ObservableProperty]
    public partial bool ShowCallingUnsupportedPanel { get; set; }

    [ObservableProperty]
    public partial bool ShowBluetoothPairingPanel { get; set; }

    [ObservableProperty]
    public partial bool ShowDialer { get; set; }

    [ObservableProperty]
    public partial bool ShowBluetoothEnablePanel { get; set; }

    [ObservableProperty]
    public partial bool ShowBluetoothAdapterNotFoundPanel { get; set; }

    [ObservableProperty]
    public partial bool IsCallingSetupError { get; set; }

    [ObservableProperty] 
    public partial string PhoneNumber { get; set; }

    partial void OnPhoneNumberChanged(string oldValue, string newValue)
    {
        if (!string.IsNullOrEmpty(newValue))
        {
            var contact = contactRepository.GetContactByPhoneNumber(newValue);

            if (contact is not null)
            {
                DialContactDisplayName = contact.DisplayName;
                DialContactAvatar = contact.Avatar;
                return;
            }
            ClearDialContactVisual();
        }
    }

    [ObservableProperty]
    public partial CallLog? SelectedCallLog { get; set; }

    [ObservableProperty]
    public partial string ContactSearchQuery { get; set; }

    [ObservableProperty]
    public partial string? DialContactDisplayName { get; set; }

    [ObservableProperty]
    public partial BitmapImage? DialContactAvatar { get; set; }

    public ObservableCollection<Contact> ContactSearchResults { get; } = [];

    private bool isLoadingCallLogs;
    public bool IsLoadingCallLogs
    {
        get => isLoadingCallLogs;
        private set
        {
            if (SetProperty(ref isLoadingCallLogs, value))
            {
                OnPropertyChanged(nameof(ShowCallLogEmpty));
                OnPropertyChanged(nameof(ShowCallLogList));
            }
        }
    }

    #endregion

    public CallsPageViewModel()
    {
        PhoneNumber = string.Empty;
        ContactSearchQuery = string.Empty;
        deviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
        phoneLineService.LineStatusChanged += OnLineStatusChanged;
        callLogRepository.CallLogUpdated += OnCallLogUpdated;
        LoadCallLogs();
        ApplyCallingLineStatus(phoneLineService.LineStatus);
    }


    private void OnLineStatusChanged(object? sender, CallingLineStatus status)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(() => ApplyCallingLineStatus(status));
    }

    private void OnActiveDeviceChanged(object? sender, PairedDevice? activeDevice)
    {
        OnPropertyChanged(nameof(ActiveDevice));

        App.MainWindow.DispatcherQueue.EnqueueAsync(() => 
        {
            LoadCallLogs();
            ClearDialContactVisual();
        });
    }


    private void ClearDialContactVisual()
    {
        DialContactDisplayName = null;
        DialContactAvatar = null;
    }

    private void OnCallLogUpdated(object? sender, (string deviceId, CallLog callLog) args)
    {
        var (deviceId, callLog) = args;
        if (!string.Equals(deviceManager.ActiveDevice?.Id, deviceId, StringComparison.Ordinal)) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            var existing = CallLogs.FirstOrDefault(log => log.CallLogId == callLog.CallLogId);
            if (existing is not null)
            {
                var existingIndex = CallLogs.IndexOf(existing);
                CallLogs.RemoveAt(existingIndex);
                InsertOrdered(callLog);
                return;
            }

            InsertOrdered(callLog);

            OnPropertyChanged(nameof(ShowCallLogEmpty));
            OnPropertyChanged(nameof(ShowCallLogList));
        });
    }

    private async void LoadCallLogs()
    {
        SelectedCallLog = null;
        if (ActiveDevice is null)
        {
            CallLogs.Clear();
            return;
        }

        try
        {
            IsLoadingCallLogs = true;
            var logs = await callLogRepository.GetCallLogsAsync(ActiveDevice.Id);
            CallLogs.Clear();
            CallLogs.AddRange(logs);
        }
        finally
        {
            IsLoadingCallLogs = false;
        }
    }

    private void InsertOrdered(CallLog callLog)
    {
        var index = 0;
        while (index < CallLogs.Count && CallLogs[index].TimestampMillis >= callLog.TimestampMillis)
        {
            index++;
        }

        CallLogs.Insert(index, callLog);
    }

    private void ApplyCallingLineStatus(CallingLineStatus status)
    {
        if (ActiveDevice is null) return;

        ShowBluetoothEnablePanel = false;
        ShowBluetoothAdapterNotFoundPanel = false;
        ShowBluetoothPairingPanel = false;
        ShowDialer = false;
        IsCallingSetupError = false;
        ShowCallingUnsupportedPanel = false;

        switch (status)
        {
            case CallingLineStatus.NotSupported:
                ShowCallingUnsupportedPanel = true;
                break;
            case CallingLineStatus.BluetoothAdapterNotFound:
                ShowBluetoothAdapterNotFoundPanel = true;
                break;
            case CallingLineStatus.BluetoothAdapterOff:
                ShowBluetoothEnablePanel = true;
                ShowBluetoothPairingPanel = false;
                break;

            case CallingLineStatus.NotLinked:
            case CallingLineStatus.DeviceNotPaired:
            case CallingLineStatus.TransportMissing:
                ShowBluetoothEnablePanel = false;
                ShowBluetoothPairingPanel = true;
                break;

            case CallingLineStatus.RegistrationAccessDenied:
                IsCallingSetupError = true;
                break;

            case CallingLineStatus.Ready:
                ShowDialer = true;
                break;
        }
    }


    public void SearchContacts(string? searchText)
    {
        ContactSearchResults.Clear();
        if (string.IsNullOrWhiteSpace(searchText)) return;
        ContactSearchResults.AddRange(contactRepository.SearchContacts(searchText));
    }

    public void ApplyContactToDialer(Contact contact)
    {
        PhoneNumber = contact.Address;
        DialContactDisplayName = contact.DisplayName;
        DialContactAvatar = contact.Avatar;
        ContactSearchQuery = string.Empty;
        ContactSearchResults.Clear();
    }

    public void ApplySearchQueryAsNumber(string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        ClearDialContactVisual();
        PhoneNumber = q;
        ContactSearchQuery = string.Empty;
        ContactSearchResults.Clear();
    }

    public async Task RetryCallingSetupAsync()
    {
        await phoneLineService.RefreshStateAsync();
    }

    public async Task EnableBluetoothAsync()
    {
        if (!await bluetoothPairingService.TryEnableBluetoothAsync()) return;

        //await bluetoothPairingService.RefreshAsync();
    }

    public async Task DialAsync()
    {
        await phoneLineService.DialAsync(PhoneNumber);
    }

    public void ToggleSelectingCallLog(CallLog callLog)
    {
        if (SelectedCallLog == callLog)
        {
            callLog.IsSelected = false;
            SelectedCallLog = null;
        }
        else
        {
            SelectedCallLog?.IsSelected = false;
            SelectedCallLog = null;
            callLog.IsSelected = true;
            SelectedCallLog = callLog;
        }
    }

    public async Task DialSelectedCallLogAsync(CallLog callLog)
    {
        var number = callLog.PhoneNumber;
        if (string.IsNullOrWhiteSpace(number)) return;

        await phoneLineService.DialAsync(number);
    }

    [RelayCommand]
    private void AppendDialKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return;
        ClearDialContactVisual();
        PhoneNumber += key;
    }

    [RelayCommand]
    private void RemoveLastDialDigit()
    {
        if (PhoneNumber.Length == 0) return;
        ClearDialContactVisual();
        PhoneNumber = PhoneNumber[..^1];
    }

    [RelayCommand]
    private void ClearDial()
    {
        ClearDialContactVisual();
        PhoneNumber = string.Empty;
    }

    [RelayCommand]
    private async Task BeginPairBluetoothAsync()
    {
        var root = App.MainWindow.Content?.XamlRoot;
        if (root is null || ActiveDevice is null) return;

        if (!bluetoothPairingService.IsBluetoothRadioOn && !await bluetoothPairingService.TryEnableBluetoothAsync())
        {
            Logger.Warn("Bluetooth is unavailable or could not be turned on.");
            return;
        }

        var setupDialog = new BluetoothSetupDialog(ActiveDevice) { XamlRoot = root };
        await setupDialog.ShowAsync();
    }

}
