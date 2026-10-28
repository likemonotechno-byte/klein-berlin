# Architektur und Integrationsgrenzen

`GCController → rc_snapshot ABI 1 → NativeBridge → InputRouter.Update → Diagnosefenster`

Native Abfrage und Routing laufen auf dem Unity-Hauptthread in `Update`, nie in
`OnGUI`. Zeichnen darf mehrere Male je Frame stattfinden. Der Router liefert bei
wiederholter Frame-ID keine Aktionen. Das Diagnosefenster verarbeitet Bestätigungen
im Update; Mausaktionen bleiben im normalen Verse-UI-Ereignisfluss.

Das einzige aktive Fenster ist momentan das oberste Diagnosefenster. Der Router
prüft zusätzlich die Identität der Fensterinstanz. Ein Wechsel zwischen zwei modalen
Fenstern setzt deshalb gehaltene Eingaben und Wiederholung zurück. Die bestehende
Update-API bleibt durch einen optionalen Besitzerparameter kompatibel.

`InputContextStack` ist die getestete Grundlage für weitere Controller-Fenster:
oberster Scope erhält Eingaben, jeder Scope behält seinen Fokus, entfernte Eltern
schließen kein darüberliegendes Kind. Die Verse-Anbindung verwendet bislang direkt
die Identität des obersten Diagnosefensters; allgemeine Fensterregistrierung und
Modal-/Radial-/Zielprioritäten müssen beim UI-Ausbau angeschlossen werden.

`Tutorial<TState>` nimmt beobachtete Zustände über einen späteren Spieladapter an.
Pro Beobachtung wird höchstens eine Lektion abgeschlossen. Klassischer Modus und
deaktiviertes Tutorial unterdrücken Hinweise und Fortschritt. Überspringen und
Abschließen sind getrennte Zustände; Wiederholen setzt eine Lektion zurück.
Hinweise lösen Tasten erst bei Anzeige aus der aktuellen Belegung auf. Export/Restore
liefert losgelöste Profildaten, toleriert unbekannte IDs und ungültige Statuswerte.
Profile werden nun versioniert als XML-String in bestehenden ModSettings gespeichert.
`ControllerProfiles.LoadTutorial/SaveTutorial` verbinden Export/Restore mit den
Profildaten; die gleichnamigen Methoden des Mods übernehmen auch den Schreibvorgang.
Spielzustandsadapter, echte Lektionen und die Einbindung ins Vanilla-Tutorial fehlen.

`ControllerProfile` hält Änderungen je Modell. Die wirksame Belegung wird aus den
aktuellen Fähigkeiten und gültigen Änderungen aufgebaut. Ungültige Einträge erzeugen
übersetzte Hinweise. Nicht verfügbare Änderungen bleiben für spätere Verbindungen
im Profil, wirken aber aktuell nicht. Konflikte werden vollständig geprüft, sodass
auch Tastenvertauschungen wiederhergestellt werden können. Die Profilrevision und
Änderungen der direkten Bindings-API erzwingen Neutralstellung im Router.
Der Maus-Editor erlaubt diskrete Aktionen; die analogen Kamera-/Cursorachsen sind
weiterhin durch gemeinsame Empfindlichkeit und Y-Invertierung einstellbar.
Unbekannte Profilversionen verwenden Standardwerte mit Hinweis; beim nächsten
Speichern werden die aktuellen Werte im Format 1 geschrieben. Alte Einstellungen
ohne Profilfeld behalten die bisherigen Achsenwerte und erhalten Standardprofile.

Die native Bibliothek bleibt für die Prozesslebensdauer geladen. Keine GameController-
Objekte oder C++-/Objective-C-Ausnahmen dürfen die C-Grenze passieren. Der Snapshot
hat 188 Bytes mit Pack=4; Name liegt an Offset 60. Ein ABI-Fehler deaktiviert den
Backend-Poll dauerhaft. Apple-Aufrufe sind noch nicht auf macOS kompiliert/geprüft.

## Nächste Schritte nach der Mac-Abnahme

1. Installierte Assemblies und tatsächliche Fensterklassen erfassen; Buildziele bestätigen.
2. Harmony-Version pinnen, Pflicht-Patches prüfen, registrierten Ersatzansichten
   jeweils eine sichere Aktivierungsbedingung geben. Nie pauschal Vanilla unterdrücken.
3. Spieladapter für Lesen und Ausführen bestehender Spielaktionen, inklusive erneuter
   Zielprüfung unmittelbar vor Ausführung; keine duplizierten Spielregeln.
4. `IControllerUiAdapter`, Modal-/Radial-/Zielprioritäten, Fokuswiederherstellung und
   expliziter Moduswechsel mit Abbruch aktiver Vorschauen.
5. HUD und spielbarer Ausschnitt; danach Inventar vollständig abarbeiten.
6. Tutorialbedingungen aus echten Spielzuständen prüfen und die vorbereiteten
   Profile mit den konkreten Lektionen verbinden.
7. Fremdfenster-Cursor; keine Anrechnung als Ersatz für Basisspieloberflächen.
8. Anzeige-/Leistungstests, alte Spielstände, Deaktivierung, drei Verständlichkeitstests
   und getrennte Endabnahmen beider Controller vor Release/Workshop-Paket.

Keine Browser-Demo kann die Verse-Oberfläche oder den Hardware-Nachweis ersetzen.
Der Prototyp bietet daher keinen Web-Preview-Dienst.

## Quellen aus dem Plan

- Apple: https://developer.apple.com/documentation/gamecontroller/gcextendedgamepad
- Apple: https://developer.apple.com/documentation/gamecontroller/gcdualsensegamepad
- Console-FAQ: https://rimworld.double11.com/faq

Die Quellenabfrage in dieser Umgebung scheiterte an der Web-Tool-Authentifizierung.
Console-Navigationsdiagramme aus tatsächlich gesichteten Aufnahmen stehen aus;
keine Abläufe werden als beobachtet ausgegeben.
