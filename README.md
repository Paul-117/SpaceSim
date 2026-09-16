# SpaceSim 1.1

Ein spielbarer 2D-Prototyp eines modularen Raumschiff-Simulators: Trägheitsflug,
statische Ziele, eine automatisch ladende Energielanze und zwei über eine
Sternenkarte verbundene Encounter. Encounter 1 startet mit 10 Zielen, Encounter 2
mit 15. Es gibt keinen Respawn. Grafik und HUD entstehen aus geometrischen
Formen; externe Assets sind nicht erforderlich.

## Schnellstart unter Windows

**`Start.cmd` doppelklicken.** Das Skript baut das Projekt und startet das Spiel.
Alternativ im Workspace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start.ps1
```

Zum Öffnen im Editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start.ps1 -Editor
```

Oder `SpaceSim.Godot/project.godot` in Godot .NET importieren und **F5** drücken.
Die Testszene startet unmittelbar. Die Ausführungsrichtlinie wird durch die
Skriptaufrufe nur für den jeweiligen PowerShell-Prozess gesetzt.

### Voraussetzungen

- Windows, .NET SDK **10.0.x** (hier geprüft mit 10.0.401).
- **Godot 4.7.2 .NET**, im vorhandenen Ordner
  `Godot_v4.7.2-stable_mono_win64/`. Die Standardversion ohne .NET reicht nicht.
- Ein mit Godots Compatibility-Renderer kompatibler Grafiktreiber.

`NuGet.Config` verwendet ausschließlich die mitgelieferten Godot-Pakete aus
`GodotSharp/Tools/nupkgs`. Es werden keine zusätzlichen NuGet-Pakete verwendet.
Bei einer anderen Godot-Installation die Paketquelle, die SDK-Version im
Godot-Projekt und die Pfade in den Skripten gemeinsam anpassen.

## Steuerung

| Taste | Wirkung |
| --- | --- |
| W | Haupttriebwerk: Schub in Richtung der Schiffsnase |
| S | Rückwärtsschub relativ zur Schiffsausrichtung, halbe Hauptschubkraft |
| A | Drehmoment nach links |
| D | Drehmoment nach rechts |
| Leertaste | Lanze einmal abfeuern, wenn bereit |
| Button „Warp Drive“ | Sternenkarte jederzeit öffnen |
| Klick auf einen anderen Encounter | Sprungpunkt auswählen |
| Button „Jump“ | Zum ausgewählten Encounter springen, sobald der Drive bereit ist |
| Esc / „Zurück zum Flug“ | Sternenkarte schließen |

**Loslassen bremst nicht.** Lineare und Winkelgeschwindigkeit bleiben erhalten.
Zum Bremsen passend gegensteuern. Rückwärtsschub bremst nur dann die gesamte
Bewegung, wenn die Schiffsnase zur Bewegungsrichtung ausgerichtet ist. Es gibt
weder Reibung noch ein Geschwindigkeitslimit und keine automatische Stabilisierung.

Die Lanze startet ungeladen, lädt in drei Simulationssekunden und schießt entlang
der gestrichelten Visierlinie. Ein zu früher Tastendruck wird verworfen; Halten
löst kein Dauerfeuer aus. Nach dem Schuss erneut drücken. Die erste Zieloberfläche
innerhalb von 1.600 m stoppt den Strahl. Ein Treffer zählt sofort und entfernt das
Ziel dauerhaft für diese Sitzung. Der sichtbare Strahl bleibt nur 0,16 Sekunden
bestehen. Auch beim Wegfliegen oder Zurückspringen werden keine Ziele ersetzt.

## Sternenkarte und Warp Drive

Der Warp Drive lädt beim Spielstart und nach jedem Sprung automatisch in
**10 Simulationssekunden** auf. „Warp Drive“ öffnet die Karte unabhängig von der
Ladung. Wähle dort einen anderen Encounter und bestätige mit **Jump**.
Ohne Auswahl, während des Ladens oder für den aktuellen Encounter ist Jump
gesperrt. Auswahl allein löst keinen Sprung aus; frühe Sprungbefehle werden
verworfen und nicht für später vorgemerkt.

**Die Sternenkarte pausiert das Spiel nicht.** Flug, Rotation, Waffenladung und
Warp-Ladung laufen weiter. W/S, A/D und Leertaste bleiben bedienbar. Die Buttons
übernehmen keinen Tastaturfokus, damit Leertaste keinen Sprung auslösen kann.

Nach einem Sprung schließt sich die Karte. Das Schiff startet am lokalen
Einstiegspunkt `(0, 0, 0)`, mit Standardausrichtung und ohne lineare oder
Winkelgeschwindigkeit. Lanzenladung und Gesamttrefferzahl bleiben erhalten.
Beide Encounter behalten ihre verbliebenen Ziele und Treffer auch bei späteren
Besuchen. Nach dem letzten Treffer bleibt ein Encounter leer und weiter
besuchbar; ein Sprung erfordert nicht, vorher alle Ziele zu zerstören.
Dieser Fortschritt gilt für die laufende Sitzung; ein Savegame existiert nicht.

Das HUD zeigt den aktuellen Encounter, Geschwindigkeit, Winkelgeschwindigkeit,
Lanzenladung/READY, Gesamttreffer, lokalen Fortschritt, Warp-Ladung und Restzeit,
Position und Kurs. Der türkisfarbene Pfeil zeigt die Bewegungsrichtung;
orange Markierungen kennzeichnen Ziele. Bei Bedarf weist ein Randpfeil zum
nächsten Ziel. Bei Fokusverlust wird die Eingabe deaktiviert, die Simulation
läuft weiter.

## Bauen und testen

Alle Befehle aus dem Workspace-Stamm:

```powershell
dotnet build SpaceSim.sln
dotnet run --project SpaceSim.Core.Tests
```

Die Core-Tests sind ein kleiner Konsolen-Testläufer ohne Testframework-Paket.
Jeder Fall meldet PASS/FAIL; bei Fehlern ist der Exitcode 1. `dotnet test` ist
deshalb hier nicht der Testbefehl. Die Tests benötigen keinen Godot-Prozess,
keine Grafik und keinen laufenden Editor.

Build, Core-Tests und Godot-Integrationstest gemeinsam:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1 -GodotSmoke
```

Die Integrationstests importieren das Projekt headless und starten die echte
Szene. Ein kurzer Test simuliert 240 Ticks mit Schuss, Treffer ohne Respawn, Schub
und Drehmoment. Der Warp-Test bedient die echten Buttons mit Mausereignissen:
Karte während des Ladens öffnen, Ziel wählen, gesperrten Jump prüfen, bei laufendem
Flug laden, nach Encounter 2 und wieder zurück springen, Fortschritt prüfen.
Er dauert 1.205 Ticks, also etwa 20 Simulationssekunden.
Beide beenden sich automatisch mit Exitcode 0 und `SMOKE PASS` bzw.
`WARP SMOKE PASS`, wenn die Prüfungen bestehen. Das Skript prüft zusätzlich die
Godot-Logs auf Fehler. Logs liegen in
`.artifacts/`. Ohne `-GodotSmoke` werden nur Build und Core-Tests ausgeführt;
`-SkipBuild` verwendet den bestehenden Debug-Build.

Optionaler visueller Prüflauf mit automatischem Screenshot und anschließendem
Beenden (nach dem Build, mit grafischer Sitzung):

```powershell
New-Item -ItemType Directory -Force .artifacts
$capture = Join-Path $PWD '.artifacts/flight.png'
& ./Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --path SpaceSim.Godot -- "--capture=$capture"
```

`--capture` benötigt den grafischen Renderer und darf nicht mit `--headless`
verwendet werden. Zusammen mit `--smoke-test` wird der Trefferstrahl erfasst;
zusammen mit `--warp-smoke-test` die geöffnete Sternenkarte während des Ladens.

## Architektur

```text
KeyboardShipControl : IShipControl     StarMap: Ziel wählen + Jump
        | ShipCommand                     | NavigationCommand
        +------------------+--------------+
                           v
Simulation.Step()                  <-- fester Takt: 60 Hz
  ShipPhysics -> LanceSystem -> WarpDriveSystem
        |                          |
        v                          v
  WorldState                  SimulationEvent-Liste
        |                          |
        +-------- Godot -----------+
             Ansicht und HUD
```

**Der Core besitzt den Zustand.** Godot liest ihn und liefert neutrale Commands.
Die Weltmodelle sind von außen nur lesbar. Kein Node und kein Sprite entscheidet
über Physik, Waffenladung oder Treffer. Der Core ist eine normale C#-Bibliothek
mit `System.Numerics.Vector3` und `Quaternion`, ohne Godot-Abhängigkeit. Dadurch
kann dieselbe Simulation später grundsätzlich in einem Headless-Server laufen.

### Projektstruktur

```text
SpaceSim.sln
SpaceSim.Core/
  Simulation/    Simulation, Einstellungen, WorldState, Events
  Ships/         3D-Schiffszustand, Commands, Steuerungsschnittstelle, Physik
  Weapons/       Lanzenladung und Ray/Sphere-Trefferberechnung
  Targets/       Statische Ziele und einmalige zufällige Platzierung
  Navigation/    Encounter-Zustand, Sprungbefehl und Warp Drive
SpaceSim.Core.Tests/
  Program.cs     Automatisierte Physik-, Waffen- und Zieltests
SpaceSim.Godot/
  project.godot
  SpaceSim.Godot.sln  Projektmappe für den Godot-Editor
  Flight.cs      Verbindung zwischen Simulation, Eingabe und Darstellung
  Scenes/        Direkt startende Flight-Szene
  Input/         Tastaturadapter
  Rendering/     Schiff, Ziele, Strahl, Sternenhimmel, Darstellungskonstanten
  UI/            Cockpit-HUD und Sternenkarte
  Testing/       Automatischer Maus-/Warp-Integrationstest
scripts/         Build-, Test- und Startskripte
Start.cmd        Start per Doppelklick
```

### Entscheidungen

- **SI-Einheiten:** Meter, Sekunden, Newton, kg, rad/s und kg·m².
  Werte wie Masse (12.000 kg), Schub (144.000 N), Rückschub (72.000 N), Drehmoment,
  Waffenreichweite und Spawnradien stehen zentral in
  `SpaceSim.Core/Simulation/SimulationSettings.cs`.
- **Build-Konfigurationen:** Die Projektmappen verwenden Godots `Debug`,
  `ExportDebug` und `ExportRelease`; der Core wird dabei passend als Debug oder
  Release gebaut. `scripts/Build.ps1 -Configuration ExportRelease` erstellt die
  optimierten Assemblies. Ein eigenständiger Spiel-Export ist noch nicht enthalten.
- **3D-Daten, planare Regel:** Position, Geschwindigkeit und Winkelgeschwindigkeit
  sind Vector3; Orientierung ist ein Quaternion. Version 1.1 hält Y = 0 und dreht
  nur um Y. Die Nase zeigt lokal nach -Z; positives Y-Drehmoment dreht nach links.
  Godot projiziert X/Z auf Bildschirm-X/Y. Für vollständiges 3D müssen Integrator,
  Trägheitsmodell und Commands erweitert werden, nicht das Zustandsformat ersetzt.
- **Fester Takt:** Ein `Step` entspricht exakt 1/60 Simulationssekunde.
  Godot ruft ihn aus `_PhysicsProcess` auf. Semi-implizites Euler integriert
  Geschwindigkeit und Position; Quaternionen werden normalisiert. Die Darstellung
  interpoliert zwischen Ticks und schreibt dabei nichts in die Simulation zurück.
- **Neutrale Eingabe:** Ein `IShipControl` liefert einen `ShipCommand`. Ein anderer
  Adapter kann später Hardware, Netzwerk oder Autopilot übersetzen. Tastenzustände
  werden ausschließlich im Godot-Adapter gelesen. `FireLance` ist ein Impuls.
- **Einfache Events:** `WeaponFired`, `TargetHit`, `TargetSpawned` und
  `EncounterChanged` sind unveränderliche C#-Records. `Simulation.Events` gilt für
  den gerade abgeschlossenen Tick und muss vor dem nächsten Step verarbeitet
  werden. Direkt nach dem Konstruktor enthält es die initialen Spawnereignisse
  beider Encounter, jeweils mit Encounter-ID.
  Dies ist eine lokale Ausgabe ohne Event-Bus oder Netzwerkgarantien.
- **Zwei persistente Encounter:** Beim Erstellen der Simulation werden einmalig
  10 bzw. 15 statische Ziele im Ring zwischen 180 und 750 m erzeugt. Beide
  Encounter besitzen eigene Ziellisten in lokalen Koordinaten. Treffer entfernen
  Ziele nur aus dem aktiven Encounter. Es gibt weder Respawn noch Recycling.
  Ein fester, konfigurierbarer Seed macht Testläufe reproduzierbar.
- **Warp im Core:** Der unabhängige `WarpDriveState` lädt mit dem festen Takt.
  `NavigationCommand` enthält einen einmaligen Sprungwunsch. Der Core prüft
  Ladung und Ziel selbst, wechselt den aktiven Encounter und setzt den
  Schiffszustand am Einstieg zurück. Ein `EncounterChanged`-Ereignis lässt die
  Ansicht alte Effekte verwerfen und verhindert Interpolation zwischen Encountern.
- **Karte als Overlay:** Auswahl und Sichtbarkeit sind reine UI-Zustände.
  Es gibt keinen Szenenwechsel und kein Pausieren des SceneTree beim Öffnen.
- **Treffer unabhängig von Grafik:** Der Core schneidet einen vorwärtsgerichteten,
  reichweitenbegrenzten Ray mit den Zielkugeln. Godot verwendet nur das resultierende
  Ereignis für Strahl und Trefferring. Es gibt keine Godot-Physik-Kollisionen.
- **Begrenzte Hintergrundkosten:** Der Sternenhimmel besteht aus gehashten,
  prozeduralen Kacheln mit zwei Parallaxebenen; die Kamera folgt dem Schiff.
  Es gibt keinen sichtbaren Kartenrand und keinen unendlich wachsenden Objektpool.
- **Spätere Stationen:** Das lokale Debug-HUD darf den vollständigen Zustand
  lesen. Zukünftige externe Stationen sollten ausschließlich gezielte, vom Core
  abgeleitete Systemausgaben erhalten. Der WorldState ist keine Netzwerk-API;
  Sensorfilter und Transportprotokolle sind noch nicht implementiert.

## Testumfang

Die 26 Core-Tests prüfen unter anderem:

- Geschwindigkeit ohne Schub, Beschleunigung in gedrehter Schiffsausrichtung,
  Weiterflug nach Loslassen, Rückschub und fehlendes Geschwindigkeitslimit.
- Rotationsträgheit, Abbremsen und Umkehren durch Gegendrehmoment,
  Ebenenregel und Quaternion-Normalisierung über 36.000 Ticks.
- Exakte Ladezeit, verworfene frühe Feuerbefehle, Entladung, automatisches
  Nachladen und einmalige Ereignisausgabe.
- Sofortigen Treffer, Fehlschuss, nächstes Ziel, Reichweite, Ziele hinter dem
  Schiff und gedrehte Waffenrichtung.
- Trefferzähler, fehlenden Respawn, sichere und reproduzierbare Platzierung,
  statische Zielpositionen auch bei großer Entfernung und 10/15 Anfangsziele.
- Zehn Sekunden Warp-Ladezeit, verworfene frühe und ungültige Sprungbefehle,
  Entladung nach dem Sprung, Ankunft im Stillstand, erhaltene Lanzenladung
  und Fortschritt nach Hin- und Rücksprung.
- Weiterlaufenden Flug und unabhängige Waffenladung während des Warp-Ladens.
- Zurückweisen ungültiger physikalischer Parameter.

## Aktuelle Einschränkungen

- Nur X/Z-Bewegung und Yaw; keine Kollisionen, Gegner, Schäden, Sensoranalyse,
  Energieversorgung, Hardwarekommunikation, Netzwerk, Audio oder Speicherung.
- Single-Precision-Koordinaten ohne Origin-Rebasing. Sehr große Entfernungen und
  extrem hohe Geschwindigkeiten verschlechtern numerische und grafische Präzision.
- Eine konstante Masse und ein Yaw-Trägheitsmoment; kein vollständiger 3D-Tensor.
- Unter starker Rechnerlast kann die Simulationszeit langsamer als die reale
  Zeit laufen. Das feste Integrationsintervall bleibt dabei erhalten.
- Das minimale HUD ist für Desktop-Fenster ausgelegt. Eine vollständige
  Bedienoberfläche, Pausefunktion und ein Export-Installer fehlen noch.
- Der Godot-Binärordner, Build-Ausgaben und Caches sind in `.gitignore`
  ausgeschlossen. Für einen neuen Checkout wird Godot .NET separat benötigt.

## Kurze Roadmap

1. Fluggefühl und physikalische Parameter im Testgefecht abstimmen.
2. Encounter-Inhalte erweitern und ein vollständiges 3D-Bewegungsmodell ergänzen.
3. Stationsspezifische Systemausgaben entwerfen, anschließend einen ersten
   Hardware- oder Netzwerkadapter anbinden.
4. Sensoren, Energie und Schäden einzeln als testbare Core-Systeme entwickeln.

## Git und Versionsstände

Der erste spielbare Stand ist als Version **1.0.0** auf dem Branch `main`
mit dem annotierten Git-Tag **`v1.0`** gesichert. Das Repository ist lokal;
ein Remote-Repository ist noch nicht eingerichtet.

Version **1.1.0** ist mit dem annotierten Tag **`v1.1`** gesichert.
Die Änderungen gegenüber v1.0 stehen in [SpaceSim_v1.1_Changelog.txt](SpaceSim_v1.1_Changelog.txt).
Der bestehende Tag `v1.0` bleibt unverändert und enthält weiterhin die
ursprüngliche Version ohne Warp Drive. `SpaceSim_v1.0_Uebersicht.txt`
dokumentiert diesen historischen Stand.

```powershell
git status             # Änderungen seit dem letzten Commit anzeigen
git log --oneline      # Gespeicherte Entwicklungsstände anzeigen
git show v1.0 --stat   # Den gespeicherten Stand von Version 1.0 anzeigen
git show v1.1 --stat   # Den gespeicherten Stand von Version 1.1 anzeigen
```

Für weitere Arbeit empfiehlt sich ein eigener Branch, zum Beispiel
`git switch -c feature/flugsteuerung`. Neue Änderungen werden anschließend
gezielt mit `git add <Datei>` und `git commit -m "Beschreibung"` gespeichert.
Der Tag `v1.0` bleibt dabei auf dem ursprünglichen Versionsstand.

Godot-Binärdateien, Build-Ausgaben und lokale Caches sind ausgeschlossen.
Ein lokales Git-Repository ersetzt keine Sicherung auf einem anderen Datenträger.

Der ursprüngliche Projektauftrag bleibt unverändert in `Plan` erhalten.
