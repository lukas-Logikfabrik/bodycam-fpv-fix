## Fixed in v1.2.1

- Prevent zero-point font errors when minimizing or receiving a temporary zero-size window area.
- Preserve the last valid layout until the window is restored.
- Add minimize/restore coverage to the UI regression checks, now run in GitHub Actions.

## Interface introduced in v1.2.0

- Redesigned dark interface with a cleaner room background, condensed headings and sidebar navigation.
- Live left/right stick previews, assignment shortcuts and visible radio/ARM switch status.
- Improved Advanced diagnostics typography and buffered page rendering to reduce flicker.
- Improved small-window layout, warning wrapping and clearing of input indicators after disconnect.
- Existing radio mapping, calibration, virtual controller output and offline driver installation remain available.

The Acro indicator reports the physical radio switch, not the game's internal mode. The background and OFL-licensed font are embedded; no additional runtime download is needed.

Use a USB RC radio as an Xbox controller for the FPV drone in Bodycam.

**Download:** `BodycamFpvFix-{{TAG}}.zip` below (recommended): the program plus the official ViGEmBus driver installer. Extract the whole zip and start `BodycamFpvFix.exe`; no installation, Windows 10/11. If the ViGEmBus driver is already installed (for example by DS4Windows), the single `BodycamFpvFix.exe` is enough. How to set it up: [Guide](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/{{TAG}}/docs/GUIDE.md) · [Anleitung (Deutsch)](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/{{TAG}}/docs/ANLEITUNG.md)

> [!WARNING]
> Keep the Arm switch **off** whenever you are on foot. While Arm is on, the throttle stick goes to the game and your character walks backwards.

**Official downloads are only this release page and [Nexus Mods](https://www.nexusmods.com/bodycam/mods/66).** Copies from anywhere else do not come from us.

**Check the download.** Both files were built by GitHub Actions from the code at tag `{{TAG}}`. The program never accesses the internet.

- SHA-256 of `BodycamFpvFix-{{TAG}}.zip`: `{{ZIPSHA256}}`
- SHA-256 of `BodycamFpvFix.exe`: `{{SHA256}}` (the exe inside the zip is the same file)
- No extra tools needed: type `powershell` into the address bar of the folder with the exe, press Enter, run `(Get-FileHash .\BodycamFpvFix.exe).Hash` and compare.
- Build attestation: `gh attestation verify BodycamFpvFix.exe --repo lukas-Logikfabrik/bodycam-fpv-fix` (works for the zip as well)

The exe is not code-signed. Chrome may say the file is not commonly downloaded (choose **Keep**), and Windows SmartScreen may warn on first start (**More info → Run anyway**).
