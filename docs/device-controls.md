# Remote device controls v1

`DeviceInfo.controls` is an optional extension. It has `version: 1`, `ringerControl`,
`bluetoothControl` (`direct`, `local`, `denied`, `unsupported`), and nullable
`bluetoothEnabled`. Missing state means unknown, not off.

Only send `DeviceControlRequest` and `DeviceControlResult` after receiving version 1.
Requests contain a UUID `requestId`, `control` (`ringer` or `bluetooth`) and integer
`value` (ringer 0/1/2; Bluetooth 0/1). The target validates the paired connection and
its own per-peer opt-in. UUIDs are deduplicated with a bounded cache. Requests are
never queued across reconnects. Results are `applied`, `denied`, `unsupported`,
`invalid`, `local` or `unconfirmed`; `local` requires an explicit user interaction
on the target and is not success. Observe actual state before changing the UI.

Existing ringer messages remain available to legacy peers. New Bluetooth control
defaults to denied. Android never invokes the deprecated enable/disable APIs;
it presents a local confirmation before the system UI. macOS/Linux do not claim
Windows radio-control support. No privileged service or new OS tool is installed.

Device deployment, physical Bluetooth transitions and UI acceptance are separate
from compilation and protocol regression checks.

DeviceInfo extensions are partial updates: absent phoneNumbers keeps the existing
list, while [] explicitly clears it. Capability and presentation publishers reuse
the authenticated connection and existing settings owners; neither creates a new
transport or device identity.

The initial capability declaration travels in the existing full DeviceInfo packet.
Later capability-only updates are sent only to v1 peers, because legacy receivers
may treat an omitted avatar or phone-number list as a replacement.
