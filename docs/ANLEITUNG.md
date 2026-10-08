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

Der virtuelle Xbox-Controller braucht den kostenlosen Treiber **ViGEmBus**. Fehlt er, lädt das Programm den offiziellen Installer selbst herunter und prüft ihn:

1. Die Windows-Admin-Abfrage bestätigen.
2. Den Installer durchklicken.
3. Die Zeile oben wird grün: *ViGEmBus driver: installed*. Falls nicht, Windows einmal neu starten.

Brichst du die Admin-Abfrage ab, bleibt der Knopf **Install driver (one admin prompt)** sichtbar, und du kannst es nochmal versuchen.

## 3. Sticks

![Reiter Sticks](screenshot-sticks.png)

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

### Kalibrieren

Die Kalibrierung macht die Mitte genau und nutzt den ganzen Stickweg, auch wenn die Funke etwas weniger als ihren Bereich sendet.

1. **Calibrate** drücken.
2. Beide Sticks loslassen, Gas ganz nach unten, **Next** drücken.
3. Beide Sticks ein paar Mal in jede Ecke und am Rand entlang führen, dann **Finish** drücken.

Neben den Knöpfen steht jetzt *Calibrated: ...*. **Reset calibration** löscht sie wieder. Läuft der Controller schon, danach einmal Stop und Start drücken.

Zeigt ein Stick in der Mitte trotzdem einen großen Wert (zum Beispiel Roll +100 %), sendet die Funke selbst ihren Anschlag. Dann zuerst die Funke kalibrieren (siehe ihre Anleitung) und hier danach noch einmal.

## 4. Arm und Acro

![Reiter Arm & Acro](screenshot-arm-acro.png)

| Funktion | Was sie in Bodycam macht |
|---|---|
| **Arm (RB)** | Schaltet die Drohne scharf: ein Druck auf RB, wenn der Schalter an geht. Solange er aus ist, hält das Programm das Gas in der Mitte, damit deine Figur nicht rückwärts läuft. |
| **Acro mode (LB)** | Bodycam schaltet den Acro-Modus mit LB um. Jedes Umlegen dieses Schalters ist ein Druck. |

Für beide: **Learn** drücken, dann den gewünschten Schalter umlegen (bei Arm auf **an**). Die Zeile zeigt danach den gelernten Eingang, zum Beispiel `Button 5` oder `Slider<1023`. **Clear** löscht ihn wieder.

> [!WARNING]
> **Arm muss aus sein, solange du zu Fuß bist.** Ist Arm an, geht der Gas-Stick ans Spiel, und Bodycam liest ihn auch zu Fuß: Mit Gas unten läuft deine Figur rückwärts, und zwar so lange, bis du Arm ausschaltest. Läuft der Controller und Arm ist an, zeigt das Programm unten eine rote Warnung.

## 5. Weitere Knöpfe (optional)

![Reiter Buttons](screenshot-buttons.png)

Der Reiter **Buttons** listet jeden Xbox-Knopf. **Learn** drücken, dann einen Schalter umlegen oder einen Knopf an der Funke drücken. Danach wählen, wie der Xbox-Knopf gedrückt wird:

| Modus | Wofür |
|---|---|
| Hold | Der Xbox-Knopf ist gedrückt, solange der Schalter an ist |
| Tap on every flip | Dinge, die das Spiel mit einem Druck umschaltet; jedes Umlegen ist ein Druck |
| Tap when switched on | Nur ein Druck, wenn der Schalter an geht |

Der Reiter **Raw input** zeigt alle Achsen und Knöpfe, die die Funke sendet, und welche Xbox-Knöpfe gerade rausgehen. Er hilft, wenn etwas nicht reagiert.

## 6. Start

**Start** drücken. Die Statuszeile meldet *Running: Bodycam sees an Xbox controller.*, der Knopf wird grün.

In den ersten zwei Sekunden die Sticks loslassen. Bei nicht kalibrierten Sticks misst das Programm in diesem Moment die Mitte; steht einer weit daneben, erscheint eine rote Warnung.

Das Fenster beim Spielen offen lassen; minimieren geht. Steckst du die Funke ab, wartet das Programm und verbindet sich danach von selbst wieder.

Mit **Start automatically** entfällt der Klick auf Start beim nächsten Mal.

## 7. In Bodycam fliegen

1. Drohne über das Tablet nehmen.
2. Den **Acro**-Schalter umlegen. Nur im Acro-Modus wirken die vier Sticks wie bei einem echten Quad. Im normalen Modus von Bodycam fährt der linke Stick vor und zurück, und der rechte bewegt die Kamera.
3. Gas ganz nach unten.
4. **Arm** einschalten und losfliegen. Arm während des Flugs an lassen.
5. Nach dem Flug (oder Absturz) Arm wieder **aus**, bevor du zu Fuß weitermachst, sonst läuft deine Figur rückwärts. Für die nächste Drohne bei Schritt 2 beginnen.

### Raten

Bodycam stellt die Drohnen-Raten sehr hoch ein. Unter **Einstellungen → Drohne** RC Rate **1.0**, Super Rate **0.65**, Expo **0.15** und Kamerawinkel **25°** probieren.

## Probleme

| Problem | Lösung |
|---|---|
| Figur läuft von selbst rückwärts | Der Arm-Schalter ist noch an, deshalb geht der Gas-Stick ans Spiel. Arm ausschalten, solange du zu Fuß bist. |
| Drohne oder Kamera rollt oder dreht von selbst | Im Reiter Sticks **Calibrate** ausführen. Zeigt ein Stick in der Mitte danach noch einen großen Wert, sendet die Funke selbst ihren Anschlag: Funke kalibrieren, dann hier erneut. |
| Bodycam zeigt 50 % Gas, obwohl der Stick unten ist | Der Arm-Schalter ist aus, das Gas ist in der Mitte gesperrt. Arm einschalten. |
| Linker Stick fährt vor/zurück, rechter bewegt die Kamera | Die Drohne ist im normalen Modus von Bodycam. Acro-Schalter umlegen (LB). |
| Funke steht nicht in der Liste | Funke in den USB-Joystick-Modus schalten. Das Programm findet sie innerhalb von zwei Sekunden; **Refresh** sucht sofort. |
| Im Spiel passiert nichts | Die Statuszeile muss „Running“ zeigen. Andere Controller-Programme wie x360ce oder DS4Windows schließen, damit nur ein virtueller Controller existiert. |
