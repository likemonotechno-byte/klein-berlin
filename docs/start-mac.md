# Start auf dem Mac

Dieses ZIP ist ein Entwicklungspaket. Es enthält geprüfte C#-DLLs, aber **keine
native Mac-Bibliothek** und keine vollständige Controller-Oberfläche. Zum Testen
werden RimWorld 1.6 (Steam), macOS 14+ und ein DualSense oder DualShock 4 benötigt.

1. ZIP entpacken. Im Terminal in den Ordner `RimController-Mac-Prototyp` wechseln.
2. Xcode Command Line Tools installieren, falls noch nicht vorhanden:
   `xcode-select --install`. Anschließend `bash scripts/build-native.sh` ausführen.
   Das Skript erstellt arm64 und x86_64 und prüft die lokale Signatur. Diese Schritte
   wurden in der Linux-Buildumgebung nicht ausgeführt.
3. Die C#-DLLs sind unter `mod/1.6/Assemblies` enthalten. Zum eigenen Neuaufbau .NET
   SDK 8.0.427 installieren und `bash scripts/build-managed.sh` ausführen; NuGet-Zugriff
   ist beim ersten Restore nötig. Die Framework-/Referenzwahl ist noch vorläufig.
4. In Steam über RimWorld → Verwalten → Lokale Dateien durchsuchen das Spiel finden.
   Den kompletten Ordner `mod` als `RimController` in den Mods-Ordner der Installation
   kopieren. Unter `RimController/Native/macOS` muss die gerade gebaute dylib liegen.
5. Steam Input für RimWorld deaktivieren. Controller in macOS verbinden. Mod im Spiel
   aktivieren; Optionen → Mod-Einstellungen → RimController öffnen.
6. Das Diagnosefenster zeigt Modell, Gerätekennung, Eingaben und wirksame Belegung.
   Die erste Öffnung erfolgt per Maus. Alle Eingaben loslassen, bevor navigiert wird.
7. Unter Controller-Profile das verbundene Modell und den Bedienkontext wählen,
   dann Aktion und Taste wählen. Belegte Tasten werden erklärt und abgelehnt. Für
   einen Tausch zuerst eine Aktion auf eine freie Taste legen. Zurücksetzen gilt
   ausschließlich für die Belegungen des ausgewählten Modells.
8. Belegung ändern, Spiel neu starten und im Diagnosefenster prüfen, ob die Auswahl
   erhalten ist. DS4 und DualSense getrennt testen. Bei Touchpad-Ausfall ohne gemeldete
   Fähigkeit wird R3 verwendet. Bei gemeldeter Fähigkeit ohne Ereignisse ist der
   Hardwaretest fehlgeschlagen; eine automatische Erkennung ist nicht implementiert.

Gemeinsame Achseneinstellungen und Profile liegen in RimWorlds ModSettings, nicht
im Spielstand. Tutorial-Einstellung und Speicheranbindung sind vorhanden, echte
Spiellektionen fehlen noch. Die klassischen Spieloberflächen bleiben verfügbar.

## Abnahme

Das vollständige [Mac-Protokoll](mac-acceptance.md) enthält Prüfungen zu Bluetooth,
unterstützten Kabelverbindungen, Rosetta, Abziehen, Wiederverbinden und App-Wechsel.
Darstellung, tatsächlicher ModSettings-Rundlauf und Bibliotheksladen müssen im Spiel
geprüft werden. Die Linux-Tests bestätigen diese Punkte nicht.

## Archiv prüfen oder neu erstellen

`SHA256SUMS.json` enthält die SHA-256-Werte aller anderen Archivdateien.
`BUILD-STATUS.json` dokumentiert den verwalteten Build und offene Mac-Prüfungen.
Nach einem eigenen nativen Build ändern sich Dateien bewusst; das Manifest
beschreibt den ursprünglichen Archivinhalt. Das Manifest im ZIP beweist keine
vertrauenswürdige Herkunft, weil es gemeinsam mit dem Inhalt verändert werden kann.

Vor automatischem Entpacken und Ausführen benötigt der Prüfer einen unabhängig
bekannten SHA-256-Wert des gesamten ZIPs, etwa vom vertrauenswürdigen Herausgeber
über einen authentifizierten Kanal oder aus einem selbst ausgeführten lokalen Build.
Den erwarteten Wert nicht aus einem unbekannten ZIP oder dessen Begleitdateien beziehen.
Aus einem vertrauenswürdigen Checkout ausführen:

```bash
python3 scripts/verify-prototype.py /Pfad/RimController-Mac-Prototyp.zip --trusted-sha256 <vertrauenswürdiger-64-stelliger-SHA256-Wert>
```

Ohne passenden vertrauenswürdigen Wert stoppt die Prüfung vor dem Entpacken und
vor sämtlichen Build-/Testbefehlen. Dann aus vertrauenswürdigen lokalen Quellen
neu bauen. Der Prüfer führt nach erfolgreicher Authentifizierung den entpackten
Build aus und vergleicht beide DLLs vollständig mit den Neubauten. Buildpfade
und automatisch eingebettete Git-Metadaten werden bei der Kompilierung vereinheitlicht,
damit ein Checkout und ein temporär entpacktes ZIP identische DLLs ergeben. Mit älteren Paketen ohne diese Vereinheitlichung
oder einer anderen SDK-Version kann der Vergleich fehlschlagen; solche Pakete aus
vertrauenswürdigen Quellen mit dem gepinnten SDK neu erstellen.

Ein neues Entwicklungspaket kann mit Python 3.9+ und installiertem .NET SDK erstellt
werden: `python3 scripts/package-prototype.py --output /gewünschter/Pfad/RimController-Mac-Prototyp.zip`.
Es enthält ausschließlich `scripts/package-files.txt`, ergänzt um Status und Prüfsummen.
Das Skript führt zuvor Build und Tests aus. Native Binärdateien werden auch dann
nicht automatisch ins Entwicklungspaket aufgenommen.
