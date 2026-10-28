# Etappe 1: Mac-Abnahme

Status aller Hardwareprüfungen: **offen**. Linux-C#-Tests ersetzen diese Abnahme nicht.

## Aufbau protokollieren

Datum, Tester, macOS-Version, Mac-Modell, Controller-Modell/Firmware, Verbindungsart,
Steam-/Spielversion, Unity-/Mono-Version, Mod-Commit, SDK und Xcode-Version notieren.
Den Prozess in der Aktivitätsanzeige als Apple/Intel identifizieren; 64-Bit allein
unterscheidet arm64 nicht von x86_64. Spielassemblies mit SHA-256 erfassen und mit
der verwendeten Referenzversion abgleichen. Kein Spielcode gehört ins Repository.

1. Beide Buildskripte aus README ausführen und Konsolenergebnis sichern.
2. `lipo -info mod/Native/macOS/librimcontroller.dylib` sowie
   `codesign --verify --strict mod/Native/macOS/librimcontroller.dylib` ausführen.
3. Mod installieren, Steam Input deaktivieren, Spiel starten und Diagnose öffnen.
4. Geräteidentität, Fähigkeiten und Rohwerte prüfen. Jeden Knopf einzeln drücken;
   Sticks in beide Richtungen und Trigger über den vollständigen Weg bewegen.
5. Kreuz halten: Zähler steigt genau einmal. Loslassen/drücken: genau ein weiterer
   Schritt. Navigation halten: verzögerte Wiederholung ohne Bestätigungswiederholung.
6. Alle Eingaben beim Öffnen halten: erst nach Neutralstellung darf Navigation starten.
7. Abziehen, Wiederverbinden, App-Wechsel und Wechsel zum zweiten Controller testen.
   Kein gehaltenes Ereignis darf erneut ausgeführt werden. Eine laufende Kolonie
   muss bei Sicherheitsunterbrechung pausieren und pausiert bleiben.
8. Gleichzeitige Controller prüfen: nur das ausgewählte Gerät wirkt bis zum Abbruch.
9. Touchpad-Klick gesondert nachweisen. Wird kein Touchpad angeboten, muss die
   Gameplay-Vorschau R3 zeigen. Wird ein Knopf angeboten, liefert aber keine Events,
   ist die Abnahme fehlgeschlagen; das wird nicht automatisch als Erfolg gewertet.
10. Mit fehlender Bibliothek und falscher Spielversion: klassische UI weiter bedienbar,
    keine wiederholten Ladeversuche oder unkontrollierte Spielaktionen.

| Prüfung | DualSense Bluetooth | DualSense Kabel* | DS4 Bluetooth | DS4 Kabel* |
|---|---|---|---|---|
| Laden / Identität / Architektur | offen | offen | offen | offen |
| Alle Knöpfe / Sticks / Trigger | offen | offen | offen | offen |
| Touchpad oder R3 | offen | offen | offen | offen |
| Halten / Wiederholung / Mehrfachzeichnen | offen | offen | offen | offen |
| Abziehen / Wechsel / App-Fokus | offen | offen | offen | offen |
| Keine Steam-Doppelaktionen | offen | offen | offen | offen |

\* Nur vom jeweiligen System unterstützte Kabelverbindungen; nicht unterstützt mit
Begründung dokumentieren, niemals als bestanden markieren. Apple- und Intel-/Rosetta-
Prozessladepfade getrennt protokollieren, soweit die Spielinstallation sie bietet.

Nach erfolgreichem technischen Nachweis: Fensterinventar gegen das installierte
Basisspiel vervollständigen und Aufwand neu schätzen. Erst danach HUD, Spieladapter,
Harmony-Unterdrückung und restliche Controller-Oberflächen implementieren.
