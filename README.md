# RimController für RimWorld 1.6 auf macOS

**Status: technischer Prototyp für Etappe 1, keine vollständige Controller-Oberfläche.**
DualSense und DualShock 4 sind gleichberechtigte Referenzcontroller. Die native
Bridge muss zuerst auf einem echten Mac im Spiel abgenommen werden. Die Etappen
2–4 des Produktplans sind noch nicht implementiert.

## Vorhanden

- Objective-C-Bridge zum Apple GameController-Framework, universeller Build für
  arm64 und x86_64, versionierte C-Schnittstelle und absoluter Bibliothekspfad.
- Ein aktiver PlayStation-Controller; Gerätewechsel erzeugt eine neue Identität.
- C#-Eingaberouter: einmalige Verarbeitung je Frame, Wiederholung der Navigation,
  Deadzone, Empfindlichkeit, Y-Invertierung, kontextabhängige Belegungen und
  Touchpad-/R3-Auswahl anhand verfügbarer Eingaben.
- Neutralstellung nach Verbindungs-, Fokus- oder Kontextwechsel; keine automatische
  Fortsetzung nach Sicherheitsunterbrechung.
- Fensteridentität im Router und getesteter Kontextstapel mit Fokuswiederherstellung.
- Tutorial-Core für Zustandsbedingungen, Überspringen/Wiederholen und aktuelle
  Tastenhinweise und Profilanbindung; noch ohne Spieladapter oder konkrete Lektionen.
- Fokusfähiges Diagnosefenster unter Optionen → Mod-Einstellungen → RimController,
  einschließlich Rohwerten, Geräteinformationen, Belegungsvorschau und Zähler.
- Deutsche und englische Texte; Einstellungen über RimWorld-ModSettings.
- Automatisierte Logiktests, einschließlich C-/C#-Strukturlayout und Fehler-Fallback.

Das Fenster benötigt zum erstmaligen Öffnen Maus/Tastatur. Es steuert noch keine
Spielaktionen. Kamera-, Zoom- und Baubelegungen sind Router-Ausgaben für spätere
Adapter. Ein Maus-bedienbarer Belegungseditor speichert getrennte Profile für
DualSense und DualShock 4. Zum Bearbeiten muss das jeweilige Modell verbunden sein.
Die Anzeige einer Touchpad-Fähigkeit beweist noch keine funktionierenden Ereignisse.

## Build

Voraussetzungen: .NET SDK 8.0.427; für die native Bibliothek zusätzlich macOS 14+
und Xcode Command Line Tools. Keine Apple-Bibliotheken unter Linux emulieren.

```bash
bash scripts/build-managed.sh
# Auf dem Mac:
bash scripts/build-native.sh
```

Der verwaltete Build verwendet gepinnte NuGet-Lockfiles und ausschließlich
Compile-Referenzen aus `Krafs.Rimworld.Ref` 1.6.4871. `net472`/`netstandard2.0`
sind **vorläufige** Buildziele: Der Abgleich mit den tatsächlich installierten
1.6-Spielassemblies und der Mono-Laufzeit ist noch ausstehend. Spielassemblies
werden nicht in den Mod kopiert. Die nativen Binärdateien sind nicht eingecheckt.

Das Verzeichnis `mod` nach beiden Builds als `RimController` in das lokale
RimWorld-Mod-Verzeichnis kopieren und im Spiel aktivieren. Steam Input für
RimWorld deaktivieren. Die native Bibliothek liegt unter
`RimController/Native/macOS/librimcontroller.dylib`; der universelle Mach-O-Build
wird entsprechend der Prozessarchitektur geladen, auch unter Rosetta.

`RC_SIGN_IDENTITY` kann eine lokale Signing-Identität für den nativen Build setzen;
Standard ist eine Ad-hoc-Signatur. Das ist keine bestätigte Distributionssignatur.
Ein Release-ZIP wird erst nach erfolgreicher Mac-Abnahme erstellt. Das
Entwicklungspaket `RimController-Mac-Prototyp.zip` enthält Quellcode und C#-DLLs;
die native Bibliothek muss auf dem Mac gebaut werden. Siehe [Mac-Startanleitung](docs/start-mac.md).

## Abnahme und Ausbau

- [Mac-Abnahme](docs/mac-acceptance.md): reproduzierbare Schritte und getrennte Ergebnisse.
- [Funktionsinventar](docs/coverage.md): vorgesehene Ersatzansichten und Prüfabläufe.
- [Architektur](docs/architecture.md): Grenzen des Prototyps und nächste Integrationsschritte.

Die klassische Oberfläche bleibt vollständig verfügbar. Es gibt derzeit keine
Harmony-Patches und keine Unterdrückung von Vanilla-Fenstern. Der Prototyp fügt
Spielständen keine Pflichtkomponenten hinzu; tatsächliche Lade-/Deaktivierungstests
stehen dennoch aus.
