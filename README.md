<p align="center">
  <img src="icon.png" width="120" alt="Cache Cleaner icon">
</p>

<h1 align="center">Cache Cleaner for RedM &amp; FiveM</h1>

<p align="center">
  Clear the RedM and FiveM cache with one click.
</p>

---

Loading problems, missing textures or a server that will not start loading are often fixed by
clearing the game cache. **Cache Cleaner** does that safely: it only empties the cache folders and
keeps game files and settings.

## Features

- **RedM and FiveM** – shows the cache size of each game and lets you pick which one to clear
- **Safe** – clears only `cache`, `server-cache` and `server-cache-priv`;
  game files (`game-storage`) and in-game settings (`nui-storage`) are kept
- **Quick Clean shortcut** – creates a desktop shortcut that clears the chosen games with one
  double-click and a single confirmation, without opening the main window
- **Confirmation** – asks before anything is deleted
- **Progress bar** – shows how much has been deleted and how much space was freed
- **Running game check** – warns if RedM or FiveM is still open
- **No admin rights** – the cache is in your own user folder
- **Light & dark mode** – follows the Windows app theme

## Download

Get the latest version from [Releases](../../releases). There are two options:

| File | Description |
|---|---|
| `CC-Setup-x.y.z-x64.exe` | **Recommended.** Installs to your user folder (no admin rights), adds a Start menu entry and optionally a desktop icon. Uninstall via *Settings → Apps*. |
| `CacheCleaner-x.y.z-portable.exe` | Single file, no installation. Keep it in a fixed folder: Quick Clean shortcuts point to its location. |

Requirements: Windows 10 or 11 (64-bit). The .NET Framework 4.8 it needs is built into Windows.

The files are not code-signed yet, so Windows SmartScreen may show *"Windows protected your PC"*.
Click **More info → Run anyway** to start it.

## Usage

1. Close RedM and FiveM.
2. Start Cache Cleaner and tick the games you want to clean.
3. Click **Clear cache** and confirm.

The next time you join a server, its files are downloaded again.

**Quick Clean:** tick the games and click **Create shortcut**. A shortcut such as
*Quick Clean – RedM* appears on the desktop. Double-click it, confirm once, and the cache is cleared;
the window closes by itself when it is done.

## Building from source

- Visual Studio (or the .NET SDK) – open `Cache Cleaner.sln` and build
- Installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php) – compile `installer/setup.iss`
  after a Release build

Every push to `main` is built by GitHub Actions. Pushing a tag like `v1.0.0` creates a release with
the installer and the portable version attached.

## License

[MIT](LICENSE)

---

<p align="center">
  <sub>Not affiliated with or endorsed by Cfx.re, Rockstar Games or Take-Two Interactive.</sub><br>
  <sub>This project was written with the help of <a href="https://claude.ai">Claude</a> by Anthropic.</sub>
</p>
