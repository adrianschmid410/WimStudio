# WIM Studio

A WPF application (.NET 8) for creating and managing custom Windows Imaging Format (WIM) files.

## Features

- **Capture Image**: Create a new WIM file from a directory
- **Append Image**: Append another image to an existing WIM
- **Get Image Info**: List all images in a WIM file
- **Delete Image**: Remove an image from the WIM
- **Export Image**: Export a single image to a new WIM (effective size reduction)
- **Mount/Unmount**: Mount WIM image for offline editing
- Select compression level (None / Fast / Maximum / Recovery)
- Verification and bootable flag configurable
- Live output and progress indicator

## Requirements

- **Windows** (DISM is part of Windows)
- **.NET 8 SDK** for building
- **Administrator rights** at runtime (enforced in the manifest)

## Notes on Creating WIM Files

1. The **source directory** is the root path of the contents (e.g., a mounted Windows Image drive such as `D:\`).
2. The **Bootable** option marks the WIM for WinPE/Recovery—this is only useful for bootable sources.
3. **Maximum** compression is significantly slower than **Fast**, but results in files that are about 30% smaller.
4. After `Delete-Image`, the file size remains unchanged. Only `Export-Image` to a new WIM frees up the storage space.

## Security

WIM operations write to system directories and require administrator privileges.
The app automatically requests these via the manifest. If it is launched without UAC elevation, a warning appears in the window.
