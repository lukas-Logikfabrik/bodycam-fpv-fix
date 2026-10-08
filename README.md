# Bodycam FPV Fix

Fly the FPV drone in **Bodycam** with a real RC radio instead of a gamepad.

Bodycam only understands Xbox controllers. Bodycam FPV Fix reads your radio over USB and turns it into a virtual Xbox controller with the stick layout that Bodycam's Acro mode expects: throttle and yaw on the left stick, pitch and roll on the right. Two switches on your radio arm the drone (RB) and toggle Acro mode (LB).

![Bodycam FPV Fix running with a BETAFPV LiteRadio](docs/screenshot-running.png)

**Guide:** [English](docs/GUIDE.md) · [Deutsch](docs/ANLEITUNG.md)

## Download

Get **`BodycamFpvFix.exe`** from the [latest release](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/releases/latest). It is a single file, needs no installation and runs on Windows 10 and 11.

## Quick start

1. Connect the radio by USB. If the radio asks for a USB mode, choose **USB Joystick (HID)**.
2. Start `BodycamFpvFix.exe`. If it says the ViGEmBus driver is missing, click **Install driver**. Windows asks for admin rights once.
3. Move the sticks. The bars should follow. If an axis is wrong or reversed, click **Learn** next to it and follow the blue prompt.
4. Click **Learn** next to **Arm (RB)** and flip your arm switch on. Do the same for **Acro mode (LB)**.
5. Click **Start** and leave the window open while you play.

In Bodycam: take out the drone, flip the Acro switch, put the throttle down and switch Arm on. Keep Arm on while you fly and switch it off when you are back on foot.

## Is this safe to run?

- **All source code is in this repository.** The exe in the releases is built by [GitHub Actions](.github/workflows/build.yml) from exactly this code, not on anyone's PC.
- **Every release has a SHA-256 checksum and a GitHub build attestation.** With the [GitHub CLI](https://cli.github.com/) you can check that your download was built by this repository:
  ```
  gh attestation verify BodycamFpvFix.exe --repo lukas-Logikfabrik/bodycam-fpv-fix
  ```
- **The program runs with normal user rights.** Admin rights are only needed once, to install the ViGEmBus driver. The program downloads the official, signed installer from the [ViGEmBus release page](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0) and checks its SHA-256 before starting it.
- **It reads your radio and nothing else.** No network access except that one driver download. It writes only its settings to `%APPDATA%\BodycamFpvFix` (and the driver installer to the temp folder, deleted afterwards).
- The exe is not code-signed, so Windows SmartScreen may warn on first start. Click **More info → Run anyway**.

## Which radios work?

Any radio that shows up on the PC as a USB joystick, for example:

| Radio | Status |
|---|---|
| BETAFPV LiteRadio (USB ID `0483:572B`, "BETAFPV Joystick") | **Tested**, works out of the box including the switch mapping |
| EdgeTX / OpenTX radios (RadioMaster TX16S, Boxer, Pocket, Zorro, TX12, Jumper, FrSky, ...) | Should work. Pick "USB Joystick (HID)" when you plug in, then use **Learn** |
| TBS Tango 2 / Mambo | Should work, use **Learn** |
| DJI FPV Remote Controller 2/3 in joystick mode | Should work, use **Learn** |

Only the BETAFPV LiteRadio has been tested so far. If you try another radio, please open an issue and say whether it worked.

## Bodycam settings that help

Bodycam's default drone rates are very high (RC Rate 2.0, which is about 1100°/s at full stick). In **Settings → Drone**, these values feel like a normal freestyle quad:

| Setting | Bodycam default | Suggested |
|---|---|---|
| Pitch / Roll / Yaw RC Rate | 2.0 | 1.0 |
| Super Rate | 0.64 – 0.69 | 0.65 |
| RC Expo | 0.22 – 0.24 | 0.15 |
| Camera tilt | 10° | 25° |

## Troubleshooting

| Problem | Fix |
|---|---|
| The drone or camera keeps rolling or turning by itself | The radio is not calibrated: one stick reports its end position while centered. The program shows a red warning. Calibrate the radio (see its manual), then click Stop and Start. |
| Bodycam shows 50 % throttle with the stick down | The arm switch is off, so the throttle is locked at center. Switch Arm on. |
| Left stick moves the drone forward/back, right stick moves the camera | The drone is in Bodycam's normal mode. Flip the Acro switch (LB). |
| Radio is not in the list | Switch the radio to USB joystick mode and click **Refresh**. |
| Nothing happens in the game | The status line must say "Running". Close other controller tools such as x360ce or DS4Windows so only one virtual controller exists. |

## How it works

The program reads the radio's HID reports directly (all axes and buttons), maps them and sends them to a virtual Xbox 360 controller created through the [ViGEmBus](https://github.com/nefarius/ViGEmBus) driver. What Bodycam expects, measured in the game:

- Left stick Y over its full travel is throttle: down = 0 %, center = 50 %, up = 100 %.
- Right stick Y negative is pitch forward.
- RB arms the drone, LB toggles Acro mode. The arm switch sends RB only when it is switched on, so radio and game stay in step after a crash.
- While the arm switch is off, the left stick is held at center so your character does not walk backwards on foot.

Settings are saved per radio in `%APPDATA%\BodycamFpvFix\settings.json`.

## Build from source

Requirements: Windows with .NET Framework 4.8 and Visual Studio 2022 or the Visual Studio Build Tools (for the C# compiler).

```
powershell -ExecutionPolicy Bypass -File build.ps1 -Version 1.0.0
```

The exe and its checksum land in `dist\`. The build downloads `Nefarius.ViGEm.Client` 1.21.256 from NuGet and checks its SHA-256.

## License

MIT, see [LICENSE](LICENSE). Uses the ViGEm.NET client (MIT) and the ViGEmBus driver (BSD-3-Clause) by Nefarius Software Solutions, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Both ViGEm projects are retired by their author but still work.

Bodycam FPV Fix is a fan project. It is not affiliated with Reissad Studio (Bodycam), BETAFPV or any other radio maker.
