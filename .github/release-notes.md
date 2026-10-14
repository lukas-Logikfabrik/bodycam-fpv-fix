## New in v1.2.2

- Text no longer outgrows its boxes on high-resolution screens with Windows scaling (reported on a 2880×1800 screen). Font sizes now follow the window size.
- The window opens at your Windows display scale and fits the screen it opens on.
- Starts in under half a second instead of about 2.5 seconds. Resizing no longer shows old text in new boxes for a moment and is about three times faster.
- App icon.
- Arm, Acro and button taps always end after 150 ms, also with radios that only send a report when something changes. Before, RB or LB could stay pressed while the sticks rested.
- If the ViGEmBus driver stops while the program runs, the program keeps reading the radio and shows why; press Start again once the driver is back.
- A damaged settings file is repaired instead of freezing the window. Settings are saved in one step, and an unreadable file is kept as `settings.json.bad`.
- Radios whose axes report negative values no longer jump from one end to the other at center.
- Maximize targets the monitor the window is on, also on a second monitor.
- RadioMaster Pocket confirmed working.

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

**Check the download.** Both files were built by GitHub Actions from the code at tag `{{TAG}}`. The program never accesses the internet.

- SHA-256 of `BodycamFpvFix-{{TAG}}.zip`: `{{ZIPSHA256}}`
- SHA-256 of `BodycamFpvFix.exe`: `{{SHA256}}`
- Build attestation: `gh attestation verify BodycamFpvFix.exe --repo lukas-Logikfabrik/bodycam-fpv-fix` (works for the zip as well)

The exe is not code-signed. Chrome may say the file is not commonly downloaded (choose **Keep**), and Windows SmartScreen may warn on first start (**More info → Run anyway**).
