# Device presentation v1

`DeviceInfo.presentation` is an optional object with version=1, kind
(unknown/phone/tablet/computer), orientation (portrait/landscape), aspectRatio
(9:16, 9:19.5, 9:20, 4:3, 3:2, 16:9, 16:10), wallpaperSource
(none/wallpaper/userSelected), nullable wallpaperId and wallpaper.

These are display preferences, never instructions to rotate a physical screen.
No dimensions are inferred from device names. Missing presentation is no update;
wallpaperSource=none explicitly clears persistent and in-memory preview caches.
Legacy avatar is never automatically classified as a wallpaper.

Sharing defaults off globally and per peer. Publish metadata on connection and
settings changes. A receiver missing a matching SHA-256 content ID sends the
known DeviceInfo type with requestedWallpaperId; only a v1 peer may request an
image, and the sender checks both sharing permissions again. A response contains
the current ID and a thumbnail only if the requested ID still matches. Images
are JPEG/PNG, <=384 px per edge and <=64 KiB before Base64. Do not transmit file
paths, source image metadata, screenshots or dynamic-wallpaper frames. A legacy
peer cannot be forced to delete an old avatar cache.

Image acquisition uses existing Android wallpaper access or a user-picked image;
Windows uses the primary desktop wallpaper path, macOS uses public NSWorkspace
on the UI thread. Unsupported access returns a neutral placeholder. No new
system permission, privileged helper, pairing or file-transfer server is needed.

DeviceInfo extensions are partial updates: absent phoneNumbers keeps the existing
list, while [] explicitly clears it. Capability and presentation publishers reuse
the authenticated connection and existing settings owners; neither creates a new
transport or device identity.
