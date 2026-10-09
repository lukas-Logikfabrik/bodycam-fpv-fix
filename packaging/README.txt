Bodycam FPV Fix
===============

Fly the drone in Bodycam with a real FPV radio (RC transmitter) instead of a gamepad.

Start
-----
1. Extract this whole zip into a folder (right-click the zip -> "Extract All...").
   Do not start the program from inside the zip.
2. Connect your radio by USB. If it asks for a USB mode, choose "USB Joystick (HID)".
3. Start BodycamFpvFix.exe.
   First start only: if the ViGEmBus driver is missing, the program starts the official
   driver installer from the folder "driver". Confirm the Windows admin prompt once and
   click through the installer. If the program asks for it, restart Windows.
4. Tab "Sticks": click "Calibrate" once. Tab "Arm & Acro": click "Learn" for both switches.
5. Click "Start" and leave the window open while you play.

In Bodycam: take out the drone, flip the Acro switch, put the throttle down, switch Arm on.

IMPORTANT: keep the Arm switch OFF whenever you are on foot. While Arm is on, the throttle
stick goes to the game and your character walks backwards.

Full guide (English):  https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/main/docs/GUIDE.md
Anleitung (Deutsch):   https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/blob/main/docs/ANLEITUNG.md
Source code and help:  https://github.com/lukas-Logikfabrik/bodycam-fpv-fix

What is in this zip
-------------------
BodycamFpvFix.exe                         the program (no installation, no internet access)
driver\ViGEmBus_1.22.0_x64_x86_arm64.exe  official, signed ViGEmBus driver installer by
                                          Nefarius Software Solutions (BSD 3-Clause License)
LICENSE.txt                               MIT License of Bodycam FPV Fix
THIRD-PARTY-NOTICES.txt                   licenses of the included third-party parts

Windows SmartScreen may warn on the first start because the exe is new and not code-signed:
click "More info" -> "Run anyway". The source code and a build attestation are on GitHub.

Bodycam FPV Fix is a fan project and is not affiliated with Reissad Studio or any radio maker.
