# Anleitung: Die Bodycam-Drohne mit einer Funke fliegen

Diese Anleitung führt dich von der neuen Funke bis zum ersten Flug. Das dauert etwa fünf Minuten. Das Programm selbst ist auf Englisch; die Begriffe stehen hier deshalb so, wie du sie im Fenster siehst.

## 1. Funke anschließen

1. Funke einschalten und mit einem USB-**Daten**kabel an den PC stecken (manche billigen Kabel laden nur).
2. Zeigt die Funke ein Menü für den USB-Modus (EdgeTX, OpenTX), **USB Joystick (HID)** wählen. Eine BETAFPV LiteRadio macht das von selbst.

Windows führt die Funke jetzt als Gamecontroller. Prüfen kannst du das mit `Win + R` → `joy.cpl`.

## 2. Bodycam FPV Fix starten

1. `BodycamFpvFix.exe` aus dem [neuesten Release](https://github.com/lukas-Logikfabrik/bodycam-fpv-fix/releases/latest) laden und starten.
2. Warnt Windows SmartScreen vor einer unbekannten App: **Weitere Informationen → Trotzdem ausführen**. Die exe ist nicht signiert; woher sie stammt, prüfst du mit `gh attestation verify` (siehe [README](../README.md#is-this-safe-to-run)).
3. Oben bei **Radio** sollte deine Funke ausgewählt sein. Sonst aus der Liste wählen oder **Refresh** drücken.

### Der Treiber (nur beim ersten Mal)

Der virtuelle Xbox-Controller braucht den kostenlosen Treiber **ViGEmBus**. Fehlt er, zeigt das Programm **Install driver (one admin prompt)**:

1. Auf den Knopf klicken. Das Programm lädt den offiziellen Installer und prüft ihn.
2. Die Windows-Admin-Abfrage bestätigen und den Installer durchklicken.
3. Die Zeile wird grün: *ViGEmBus driver: installed*. Falls nicht, Windows einmal neu starten.

## 3. Sticks prüfen

![Hauptfenster](screenshot-running.png)

Jeden Stick bewegen und auf die Balken schauen:

| Zeile | Stick (Mode 2) | Balken soll |
|---|---|---|
| Throttle (Gas) | linker Stick hoch/runter | unten 0 %, oben 100 % |
| Yaw (Gieren) | linker Stick links/rechts | rechts = plus |
| Pitch (Nicken) | rechter Stick hoch/runter | hoch (vorne) = plus |
| Roll (Rollen) | rechter Stick links/rechts | rechts = plus |

Bewegt sich ein Balken mit dem falschen Stick oder in die falsche Richtung:

1. In dieser Zeile **Learn** drücken.
2. Tun, was der blaue Text sagt (zum Beispiel *Push the RIGHT stick fully UP and hold it* = rechten Stick ganz nach oben und halten), und den Stick kurz halten.
3. Die Zeile zeigt jetzt die richtige Achse, **invert** ist passend gesetzt.

Der graue Kasten *Raw input from the radio* zeigt alle Achsen und Knöpfe, die die Funke sendet. Er hilft, wenn etwas gar nicht reagiert.

## 4. Die zwei Schalter festlegen

| Funktion | Was sie in Bodycam macht |
|---|---|
| **Arm (RB)** | Schaltet die Drohne scharf. Solange der Schalter aus ist, hält das Programm das Gas in der Mitte, damit deine Figur nicht rückwärts läuft. |
| **Acro mode (LB)** | Bodycam schaltet den Acro-Modus mit LB um. Jedes Umlegen dieses Schalters ist ein Druck. |

Für beide: **Learn** drücken, dann den gewünschten Schalter umlegen (bei Arm auf **an**). Die Zeile zeigt danach den gelernten Eingang, zum Beispiel `Button 5` oder `Slider<1023`. **Clear** löscht ihn wieder.

## 5. Start

**Start** drücken. Die Statuszeile meldet *Running: Bodycam sees an Xbox controller.*, der Knopf wird grün.

In den ersten zwei Sekunden die Sticks loslassen: Das Programm misst die Mittelstellung. Steht ein Stick weit daneben, erscheint eine rote Warnung. Dann die Funke kalibrieren und Stop und Start drücken.

Das Fenster beim Spielen offen lassen; minimieren geht. Steckst du die Funke ab, wartet das Programm und verbindet sich danach von selbst wieder.

Mit **Start automatically** entfällt der Klick auf Start beim nächsten Mal.

## 6. In Bodycam fliegen

1. Drohne über das Tablet nehmen.
2. Den **Acro**-Schalter umlegen. Nur im Acro-Modus wirken die vier Sticks wie bei einem echten Quad. Im normalen Modus von Bodycam fährt der linke Stick vor und zurück, und der rechte bewegt die Kamera.
3. Gas ganz nach unten.
4. **Arm** einschalten und losfliegen. Arm während des Flugs an lassen.
5. Nach dem Flug (oder Absturz) Arm wieder aus. Für die nächste Drohne bei Schritt 2 beginnen.

### Raten

Bodycam stellt die Drohnen-Raten sehr hoch ein. Unter **Einstellungen → Drohne** RC Rate **1.0**, Super Rate **0.65**, Expo **0.15** und Kamerawinkel **25°** probieren.

## Probleme

| Problem | Lösung |
|---|---|
| Drohne oder Kamera rollt oder dreht von selbst | Die Funke ist nicht kalibriert: Ein Stick meldet in der Mitte seinen Anschlag. Das Programm zeigt dann eine rote Warnung. Funke kalibrieren (siehe ihre Anleitung), danach Stop und Start. |
| Bodycam zeigt 50 % Gas, obwohl der Stick unten ist | Der Arm-Schalter ist aus, das Gas ist in der Mitte gesperrt. Arm einschalten. |
| Linker Stick fährt vor/zurück, rechter bewegt die Kamera | Die Drohne ist im normalen Modus von Bodycam. Acro-Schalter umlegen (LB). |
| Funke steht nicht in der Liste | Funke in den USB-Joystick-Modus schalten und **Refresh** drücken. |
| Im Spiel passiert nichts | Die Statuszeile muss „Running“ zeigen. Andere Controller-Programme wie x360ce oder DS4Windows schließen, damit nur ein virtueller Controller existiert. |
