# PhotoDrop

Send photos from your phone to your PC over Wi-Fi. Open a page on your phone, tap one
button, pick your photos — they land in a folder on your PC.

No cloud, no account, no cable, nothing to install. One 12 MB Windows exe.

## Get it

Download `PhotoDrop.exe` from [Releases](../../releases/latest) and double-click it.

1. Windows asks about network access the first time — click **Allow**.
2. A setup page opens with a QR code. Scan it with your phone's camera (same Wi-Fi). You can save the webpage as an app from most phone web browsers to access it quickly.
3. Tap **Send photos**, pick some, done.

That's the only time you'll see the setup page. After that PhotoDrop sits quietly in
the system tray as a blue arrow icon. Photos go to a `PhotoDrop` folder inside your
Pictures folder unless you change it.

## Tray menu

Right-click the tray icon:

- **Show address for phone** — the QR code again
- **Open photo folder**
- **Choose folder…** — pick where photos are saved
- **Enable/Disable running at startup**
- **Edit settings file…**
- **Exit**

A notification pops up when photos arrive; click it to open the folder.

## Settings

`config.json` sits in `.photodrop` in your home folder (`%USERPROFILE%\.photodrop` on Windows,
`~/.photodrop` on macOS and Linux) and is created on first run.

| Key | Meaning |
| --- | --- |
| `SaveFolder` | Where photos go. Blank = `Pictures\PhotoDrop`. |
| `Port` | Default `8080`. |
| `Pin` | Blank = anyone on your Wi-Fi can send. Set one to require it. |
| `OrganizeByDate` | `false` puts each day in its own subfolder. Off by default. |
| `Introduced` | Set to `false` to see the setup page again. |

Restart PhotoDrop after editing (tray → Exit, then run it again).

## Notes

- **Local network only.** Nothing is exposed to the internet and nothing leaves your
  Wi-Fi. Don't port-forward it.
- Works with photos and videos. Large files stream straight to disk.
- Duplicate names become `IMG_0001 (1).jpg` — nothing gets overwritten.
- On iPhone, choose **Photo Library** in the picker. iOS usually converts HEIC to JPEG
  on upload.
- Phone can't connect? The setup page has a **Phone can't connect?** button that fixes
  the usual cause: a dismissed Windows Firewall prompt leaves a block rule behind.

## Build it yourself

Needs the .NET 8 SDK.

```
build.cmd
```

Output lands in `dist\PhotoDrop\`. `tools\make-icon.ps1` regenerates the icon artwork.

ASP.NET Core Kestrel serves the pages; the tray icon is raw Win32 (no UI framework, so
the trimmed self-contained exe stays around 12 MB instead of 77 MB).
