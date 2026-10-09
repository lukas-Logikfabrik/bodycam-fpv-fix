Use a USB RC radio as an Xbox controller for the FPV drone in Bodycam.

**Download:** `BodycamFpvFix-{{TAG}}.zip` below (recommended): the program plus the official ViGEmBus driver installer. Extract the whole zip and start `BodycamFpvFix.exe`; no installation, Windows 10/11. If the ViGEmBus driver is already installed (for example by DS4Windows), the single `BodycamFpvFix.exe` is enough. How to set it up: [Guide](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/{{TAG}}/docs/GUIDE.md) · [Anleitung (Deutsch)](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/{{TAG}}/docs/ANLEITUNG.md)

> [!WARNING]
> Keep the Arm switch **off** whenever you are on foot. While Arm is on, the throttle stick goes to the game and your character walks backwards.

**Check the download.** Both files were built by GitHub Actions from the code at tag `{{TAG}}`. The program never accesses the internet.

- SHA-256 of `BodycamFpvFix-{{TAG}}.zip`: `{{ZIPSHA256}}`
- SHA-256 of `BodycamFpvFix.exe`: `{{SHA256}}`
- Build attestation: `gh attestation verify BodycamFpvFix.exe --repo lukas-Logikfabrik/bodycam-fpv-fix` (works for the zip as well)

The exe is not code-signed. Chrome may say the file is not commonly downloaded (choose **Keep**), and Windows SmartScreen may warn on first start (**More info → Run anyway**).
