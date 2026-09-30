# SpaceSim 1.9.1

Ein spielbarer 2D-Prototyp eines modularen Raumschiff-Simulators: Traegheitsflug,
statische Ziele, eine automatisch ladende Energielanze und vier ueber eine
Sternenkarte verbundene Encounter. Encounter 1 startet mit 10 Zielen, Encounter 2
mit einem Easy-Gegner, Encounter 3 mit einem Medium-Gegner und Encounter 4 mit
einem Hard-Gegner. Es gibt keinen Respawn. Grafik und HUD entstehen aus
geometrischen Formen; externe Assets sind nicht erforderlich.
## Energieverteilung und Schilde

Jedes Schiff besitzt einen vereinfachten Reaktor mit konstanten **100 Power Units**.
Der Core verteilt diese Leistung auf Antrieb, Waffen und Schilde. Der Standardwert
ist 35 / 35 / 30. Negative oder zu hohe Zuweisungen werden atomar abgewiesen.
Der Reaktor ist absichtlich nur ein Platzhalter; Wärme, Brennstoff, Reaktortypen,
Kopplungsstufen und Overdrive existieren noch nicht.

Die Wirkung ist linear: Antriebsleistung skaliert Haupt-, Rückwärts- und
Yaw-Schub, Waffenleistung bestimmt die Ladegeschwindigkeit der Lanze und
Schildleistung bestimmt die Regeneration. Bereits vorhandene Bewegung,
Lanzenladung und Schildstärke bleiben beim Umverteilen erhalten.

Das HUD bietet rechts **POWER DISTRIBUTION** mit -/+ Buttons in 5-Power-Unit-
Schritten. Erhöhungen ohne freie Leistung werden abgewiesen. Die Anzeige enthält
auch die Schildstärke und den aktuellen Status.

Jeder Schild besitzt 100 Punkte; die Lanze verursacht 100 Schaden. Ein voller
Schild absorbiert einen Treffer, jeder Restschaden zerstört das Schiff, weil
V1.3 noch keine Hüllenpunkte besitzt. Nach einem Schildtreffer gilt ein
dreisekündiger Recharge Delay. Danach regeneriert der Schild mit bis zu 20
Punkten pro Sekunde, abhängig von der Schildleistung.

## Hull, Systemschaden und Speed Limit

Jedes Schiff besitzt nun **3 Hull Integrity**. Vollständig absorbierte
Schildtreffer verändern die Hülle nicht. Jeder Restschaden entfernt genau einen
Hull-Punkt und beschädigt mit reproduzierbarem Zufall eines von drei Subsystemen:
Propulsion, Weapons oder Shields. Der Zustand sinkt pro Treffer um 50 Prozent;
bei 0 Prozent ist das System ausgefallen. Hull 0 zerstört Spieler oder Gegner.

Die effektive Leistung jedes Systems lautet **Power Factor x Condition**.
Antriebsschaden skaliert Schub und Drehmoment, Waffenschaden die Laderate und
Schildschaden die Regeneration. Bereits geladene Lanze und bestehende Schildstärke
bleiben unverändert.

Ein erfolgreicher Warp repariert alle drei Subsysteme und füllt den Schild auf,
repariert aber keine Hull Integrity. Das Speed Limit ist gameplaybedingt:
`500 m/s x PropulsionPowerFactor x PropulsionCondition`. Es begrenzt nur weitere
beschleunigende Hauptschubimpulse. Eine bereits höhere Geschwindigkeit bleibt
dank Traegheit erhalten und kann mit Rueckschub
abgebaut werden.

## Schnellstart unter Windows

**`Start.cmd` doppelklicken.** Das Skript baut das Projekt und startet das Spiel.
Alternativ im Workspace:

Der aktuelle Arbeitsstand ist **1.9.1**. Er umfasst die externe Waffenstation **Armarium**, das angeschlossene **Reactorium** und die überarbeitete Brückenansicht. Der Stand ist fuer `v1.9.1` bereit; ein Git-Tag wird nur auf ausdrueckliche Anweisung erstellt.

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
| Mausrad hoch / runter | Taktische Ansicht hinein- / herauszoomen |
| Leertaste | Lanze einmal abfeuern, wenn bereit |
| Button „Warp Drive“ | Sternenkarte jederzeit öffnen |
| Klick auf einen anderen Encounter | Sprungpunkt auswählen |
| Button „Jump“ | Zum ausgewählten Encounter springen, sobald der Drive bereit ist |
| Esc / „Zurück zum Flug“ | Sternenkarte schließen |

**Loslassen bremst nicht.** Lineare und Winkelgeschwindigkeit bleiben erhalten.
Zum Bremsen passend gegensteuern. Rückwärtsschub bremst nur dann die gesamte
Bewegung, wenn die Schiffsnase zur Bewegungsrichtung ausgerichtet ist. Es gibt
weder Reibung noch automatische Stabilisierung. Der aktive Antrieb begrenzt weitere Beschleunigung am aktuellen Speed Limit; bestehende Traegheit bleibt erhalten.

Die Lanze startet ungeladen, lädt in drei Simulationssekunden und schießt entlang
der gestrichelten Visierlinie. Ein zu früher Tastendruck wird verworfen; Halten
löst kein Dauerfeuer aus. Nach dem Schuss erneut drücken. Die erste Zieloberfläche
innerhalb von 1.600 m stoppt den Strahl. Ein Treffer zählt sofort und entfernt das
Ziel dauerhaft für diese Sitzung. Der sichtbare Strahl bleibt nur 0,16 Sekunden
bestehen. Auch beim Wegfliegen oder Zurückspringen werden keine Ziele ersetzt.

## Armarium und Reactorium Station Server (V1.9)

Beim Start von SpaceSim startet auf dem Haupt-PC der Station Server auf Port
**47870**. Er liefert den Armarium-Webclient aus und akzeptiert dessen WebSocket-
Verbindung. Lokal wird die Station im Browser unter
`http://127.0.0.1:47870/armarium/` geoeffnet. Im LAN wird dieselbe URL mit der
LAN-IP des Haupt-PCs verwendet, beispielsweise `http://192.168.x.x:47870/armarium/`.

Das Armarium ist eine reine HTML/CSS/Vanilla-JavaScript-Canvas-Anwendung. Es zeigt
den horizontalen Bearing des ersten aktiven Gegners relativ zur Bugwaffe, den
Lanzenladestand und den Verbindungsstatus. Das obere Fenster heisst **ZIELHILFE**.
Die Skala zeigt -7,5 bis +7,5 Grad; der rote Gegnerpunkt laeuft mit browserseitiger
Interpolation weich auf ihrer horizontalen Achse und bleibt ausserhalb dieses Bereichs
am Rand. Ein vom Core bestaetigter Lanzen-Treffer laesst den Punkt kurz aufleuchten.
Sobald ein Browser das Stationsprotokoll
erfolgreich angemeldet hat, zeigt das Flight-HUD gruen `ARMARIUM ONLINE`; der Browser
zeigt denselben Status. Die Bruecke besitzt ausschliesslich die Schiffssteuerung mit
**A/D**. Die Pfeiltasten im Armarium bewegen nur die Lanzenlafette mit **10 Grad pro
Sekunde** innerhalb von **-5 bis +5 Grad** relativ zur Schiffsnase. Der cyanfarbene
Marker im Armarium zeigen die aktuelle Waffenachse. Die gestrichelte Linie in der
Brueckenansicht zeigt dagegen immer die Bugausrichtung bis zum sichtbaren Bildschirmrand.
Unter
der bisherigen Zielskala zeigt die **TACTICAL LANCE MAP** einen 1-km-Sektor vor dem
Schiff. Sichtbar ist nur der Bereich von **-30 bis +30 Grad**; ausserhalb bleibt die
Karte leer. Der Sektor zeigt die Begrenzung der Lanzenlafette bei -5/+5 Grad, ihre
aktuelle cyanfarbene Ausrichtung und den Gegner nur innerhalb dieses Bereichs und
der Reichweite. Die Armarium-Oberflaeche verwendet immer genau die aktuelle
Fensterhoehe und benoetigt keine Scrollleiste. **Leertaste**
im Browser sendet genau einen `fire_lance`-Befehl. Die lokale Leertaste bleibt parallel
aktiv.

Der Haupt-PC bleibt autoritativ: Der Browser erhaelt nur `targetAvailable`,
`targetBearingDegrees`, `targetDistanceMeters`, `lanceCharge`, `lanceReady`,
`lanceTurretAngleDegrees` und `simulationTick`.
Er entscheidet nicht ueber Waffenfeuer oder Treffer. Der Fire Command wird im
Haupt-PC thread-sicher gepuffert und erst im normalen `ShipCommand` des naechsten
Simulationsticks verarbeitet. Das Armarium nutzt derzeit die exakte Gegnerposition;
eine spaetere Sensorium-Quelle kann den Bearing liefern, ohne den Webclient zu aendern.

Das WebSocket-Protokoll hat Version **7**. Der Client identifiziert sich beim
Verbindungsaufbau als `armarium`, zeigt `ARMARIUM ONLINE/OFFLINE` und verbindet sich
nach einem Abbruch automatisch erneut. Details stehen in
[StationProtocol.md](docs/StationProtocol.md).

### Reactorium

Das Reactorium ist unter `http://127.0.0.1:47870/reactorium/` erreichbar. Sein
Operating-Level-Schieberegler setzt den autoritativen **Sollwert** von 0 bis
100 Prozent. Die physische Reaktorleistung folgt diesem Sollwert träge und benötigt
für den Weg von 0 auf 100 Prozent exakt **60 Simulationssekunden**. Bei voller
Leistung liefert der Reaktor **125 PU**. Die Reactorium-Ansicht verwendet wieder
den wabbernden Reaktorkern aus dem Stationsprototyp; seine Größe und Bewegung
folgen dem vom Core übertragenen tatsächlichen Output.

Das Reactorium verteilt diese Leistung mit drei Slidern: **Brücke**, **Schilde**
und **Armarium**. Die Werte sind Prozent des aktuellen Reaktor-Outputs und zeigen
direkt daneben die daraus autoritativ abgeleiteten PU. Die Anfangsverteilung
40/28/32 Prozent nutzt bei 125 PU die Stationsmaxima von 50/35/40 PU. Ein Slider
kann weder über 100 Prozent Gesamtzuweisung noch über das PU-Limit seiner Station
steigen. Für eine höhere Zuweisung muss zuerst eine andere Station reduziert
werden. Nicht zugewiesene Prozent bleiben ungenutzt.

Der Reaktor besitzt **100 U Fuel**. Solange der Reaktor Leistung erzeugt, verbraucht
er Fuel abhängig vom tatsächlichen Betriebslevel: bei minimaler aktiver Leistung
0,2 U/min, bei 100 Prozent 7,0 U/min mit quadratischem Verlauf dazwischen. Bei
0 U wird der Reaktor im Core abgeschaltet; Output und gelieferte Stationsleistung
werden 0. Eine Betankung existiert derzeit noch nicht. Die drei Stationen
fordern ihre Leistung innerhalb ihres Reactorium-Budgets selbst an: Brücke maximal
50 PU, Armarium maximal 40 PU und Schilde maximal 35 PU. Der Core liefert keiner
Station mehr Energie als ihr zugewiesenes Budget.

Die Bruecke besitzt keine Power-Distribution-Buttons mehr. Ihre verfügbare
Stationsleistung wird ausschließlich im Reactorium zugewiesen. W rampet beim
Gedrueckthalten als Gas bis zur maximalen Main-Thruster-Anforderung von 20 PU.
Fuel, Ramp, Zuweisung und Abschaltung werden ausschließlich vom Simulations-Core
berechnet; das Reactorium zeigt nur dessen Snapshot und sendet Zuweisungsabsichten.

Innerhalb der Antriebsstation sind **30 PU** dauerhaft für Rückwärts- und
Seitentriebwerke reserviert: S, A und D besitzen jeweils 10 PU Priorität, auch
wenn sie gerade nicht betätigt werden. Von maximal 50 PU bleiben W damit höchstens
20 PU. Bei 35 PU Stationsleistung erhält W folglich 5 PU. Sinkt die verfügbare
Stationsleistung unter 30 PU, erhalten S, A und D jeweils denselben linearen
Leistungsfaktor; W erhält dann keine Leistung.

### Brücke 1.9.1

Das bisherige Fenster **ENERGY** oben rechts ist entfernt. Die bisherigen Treffer-
und Lanzenboxen sind ebenfalls nicht mehr Teil der Brücke, damit diese Informationen
später als eigene Stationen laufen können.

Unten links zeigt die Triebwerksbox **MAIN THRUSTERS**, **STARBOARD THRUSTERS**,
**PORT THRUSTERS** und **REVERSE THRUSTERS**. Ganz oben steht die für die Station
verfügbare Energie in PU. Die Hilfstriebwerke stehen vor den Main Thrusters; der
blaue Main-Thrust-Balken liegt am unteren Rand und zeigt die tatsächlich wirksame
Vorwärtsleistung von W. Jeder Eintrag zeigt seinen Energiezustand: **ONLINE** in
Grün bei voller Leistung, **LIMITED** in Gelb bei Drosselung und **OFFLINE** in Rot
ohne Leistung. Main Thrusters sind erst bei 50 verfügbaren PU vollständig online;
die drei Hilfstriebwerke benötigen zusammen 30 PU. Nur **W** baut seinen Schub über **5 Sekunden**
bis 100 Prozent auf und nach dem Loslassen über **3 Sekunden** wieder ab.
Rückwärtsschub mit S und die Seitentriebwerke mit A/D reagieren sofort. Positions-,
Zoom-, Integritäts-, Kurs- und Simulationszeitdaten stehen unten rechts.

Der **Warp Drive** befindet sich oben mittig. Seine Schaltfläche bleibt anklickbar
und ist selbst der Ladebalken: Die Füllung zeigt den aktuellen Warp-Ladestand. Der
frühere Text mit Prozentwert beziehungsweise `READY` wird nicht mehr zusätzlich
angezeigt.

### Armarium vom Laptop starten

Auf dem Haupt-PC muss SpaceSim bereits laufen. Nach `git pull` auf dem Laptop kann
**`Armarium-Start.cmd`** doppelt geklickt werden. Das Skript fragt die LAN-IP des
Haupt-PCs ab und oeffnet dann die Armarium-Seite im Standardbrowser. Alternativ kann
die IP direkt als Parameter uebergeben werden:

```powershell
.\Armarium-Start.cmd 192.168.178.20
```

Der Starthelfer startet keine zweite Simulation; er verbindet den Laptop nur als
Armarium-Station mit dem laufenden Haupt-PC.
## Gegner und Game Over

Encounter 1 bleibt ein Uebungsbereich mit zehn Zielen. Encounter 2 enthaelt einen
Easy-Gegner, Encounter 3 einen Medium-Gegner und Encounter 4 einen Hard-Gegner.
Alle Gegner verwenden dieselbe Masse, Traegheit, Schub-, Energie-, Schild-, Hull-
und Subsystemregeln wie der Spieler. Die KI erzeugt ausschliesslich normale
`ShipCommand`-Befehle; sie setzt Position, Geschwindigkeit und Rotation nie direkt.
Bei einem Abstand unter **100 m** kollidieren Spieler- und Gegnerschiff; beide
werden sofort zerstoert. Der Spieler sieht anschliessend Game Over.
Ein zerstoerter Gegner erzeugt zudem eine Explosion: bis 350 m wird der
Spielerschild entleert, bis 250 m faellt ein Subsystem aus, bis 200 m zwei
Subsysteme und bis 150 m wird das Spielerschiff zerstoert.

Die Zustandsmaschine verwendet `ACQUIRE`, `APPROACH`, `ATTACK`, `EVADE` und
`REPOSITION`. Der Health-aware Risk Level bestimmt die Vorsicht innerhalb dieser
States: gesunde Gegner greifen aggressiv an, beschaedigte Gegner weichen bei
konkreter Lanzenbedrohung kurz aus und kehren danach wieder in den Kampf zurueck.

Ein voller Schild absorbiert einen Lanzentreffer. Restschaden reduziert die Hull,
beschaedigt ein Subsystem und zerstoert das Schiff erst bei Hull 0. Beim Spieler
loest das `GameOver` aus; Zeit, Physik, KI, Waffen und Navigation halten an.
## Sound Effects (V1.6)

Godot spielt die Soundeffekte ausschliesslich als Darstellung von Core-Events
oder dem lokalen Spielerzustand. Der Core bleibt damit frei von Audioabhaengigkeiten.
`Booster` loopt waehrend Haupt- oder Rueckschub aktiv ist. `Lance_ready` spielt
einmal beim Erreichen von 100 Prozent, `Lance_shot` beim Spielerschuss.
`shield_charge` spielt einmal drei Sekunden vor dem voraussichtlichen Volladen
des Spielerschilds, damit sein Ende den vollen Schild signalisiert. Schildtreffer, Schilddepletion,
Hull-Schaden und erfolgreiche Warp-Spruenge loesen jeweils `shield_hit`,
`shield_depleted`, `Ship_Damage` und `Warp_jump` aus. `shield_low` liegt bereits
im Projekt, bleibt aber bis zu einer mehrstufigen Schildmechanik ohne Ausloeser.

## Tactical Zoom und Health-Aware Combat AI (V1.5)

Die Flight-Kamera bleibt immer auf dem Spielerschiff zentriert. Das Mausrad aendert nur ihren Zoom: hoch vergroessert die taktische Ansicht, runter verkleinert sie. Der Zoom hat keine kuenstliche Ober- oder Untergrenze und wird je Eingabe multiplikativ angepasst. Das HUD zeigt neben der Positionsanzeige die aktuelle Skala. HUD, Sternenkarte und Randindikatoren liegen weiterhin in eigenen Canvas-Ebenen und behalten deshalb ihre Bildschirmgroesse.

Jeder Gegner besitzt vollstaendig dasselbe Hull-, Shield- und Subsystemmodell
wie der Spieler. Die bestehende FSM bleibt erhalten und erhaelt eine getrennte,
deterministische Risikostufe: `AGGRESSIVE`, `NORMAL`, `DEFENSIVE` oder
`CRITICAL`. Sie bewertet Hull, aktuellen Schild und die drei System-Conditions.
Voller Hull und Schild bedeuten AGGRESSIVE; ein Schild unter 70 Prozent NORMAL,
unter 30 Prozent DEFENSIVE. Hull-Schaden oder schwere Systemschaden erhoehen die
Vorsicht weiter; bei einem Hull-Punkt oder einer System-Condition bis 25 Prozent
ist der Gegner CRITICAL.

Der gesunde Gegner bevorzugt etwa **450 m** Abstand; die risikobezogenen Ziele
liegen bei 450 / 475 / 525 / 550 m. ATTACK wird im Bereich **250--700 m**
bevorzugt und verlaesst ihn erst oberhalb von **850 m**. Ein voller Gegner weicht
einer lediglich bereiten Lanze nicht aus. Die Bedrohungsschwellen fuer
AGGRESSIVE / NORMAL / DEFENSIVE / CRITICAL sind jeweils 100 / 90 / 80 / 75
Prozent Waffenladung und 2 / 4 / 6 / 8 Grad Zielfehler. EVADE dauert 0,45 bis
0,90 Sekunden und aktiviert danach einen risikobezogenen Cooldown von 3,5 / 3 /
2,5 / 2 Sekunden. Danach kehrt die FSM bevorzugt zu ATTACK zurueck; es gibt
keinen FLEE-State. Die Debug-Zeile im HUD zeigt State, Risk, Hull, Shield und
Subsystem-Conditions des ersten aktiven Gegners.

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
Alle Encounter behalten ihre verbliebenen Ziele, Gegner und Treffer auch bei
späteren Besuchen. Nach dem letzten Treffer bleibt ein Encounter leer und weiter
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
`WARP SMOKE PASS`, wenn die Prüfungen bestehen. Ein dritter Test springt zu
Encounter 2, lässt die Gegner-KI mit ihrer Lanze gewinnen und prüft Game Over,
Stillstand und Neustart. Das Skript prüft zusätzlich die
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
  Lance laden -> AI/FSM erzeugt ShipCommand
  -> gemeinsame ShipPhysics -> LanceSystem -> WarpDriveSystem
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
  Combat/        Gegnerzustand und globaler Spielzustand
  AI/            Kontext, zentrale Parameter und deterministische FSM
SpaceSim.Core.Tests/
  Program.cs     Automatisierte Physik-, Waffen- und Zieltests
SpaceSim.Godot/
  project.godot
  SpaceSim.Godot.sln  Projektmappe für den Godot-Editor
  Flight.cs      Verbindung zwischen Simulation, Eingabe und Darstellung
  Scenes/        Direkt startende Flight-Szene
  Input/         Tastaturadapter
  Rendering/     Schiff, Ziele, Strahl, Sternenhimmel, Darstellungskonstanten
  UI/            Cockpit-HUD, Sternenkarte und Game-Over-Overlay
  Testing/       Automatisierte Godot-Integrationstests
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
  sind Vector3; Orientierung ist ein Quaternion. Version 1.3 hält Y = 0 und dreht
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
- **Einfache Events:** `WeaponFired`, `TargetHit`, `TargetSpawned`,
  `EncounterChanged`, `EnemyDestroyed` und `PlayerDestroyed` sind unveränderliche
  C#-Records. `Simulation.Events` gilt für
  den gerade abgeschlossenen Tick und muss vor dem nächsten Step verarbeitet
  werden. Direkt nach dem Konstruktor enthält es die initialen Spawnereignisse
  des Ziel-Encounters, jeweils mit Encounter-ID.
  Dies ist eine lokale Ausgabe ohne Event-Bus oder Netzwerkgarantien.
- **Vier persistente Encounter:** Encounter 1 erzeugt einmalig zehn statische
  Ziele im Ring zwischen 180 und 750 m. Encounter 2 besitzt einen Easy-Gegner,
  Encounter 3 einen Medium-Gegner und Encounter 4 einen Hard-Gegner. Alle
  Encounter haben eigene lokale Zustaende. Treffer entfernen Ziele oder Gegner nur
  aus dem aktiven Encounter. Es gibt weder Respawn noch Recycling.
  Ein fester, konfigurierbarer Seed macht Testlaeufe reproduzierbar.
- **Warp im Core:** Der unabhängige `WarpDriveState` lädt mit dem festen Takt.
  `NavigationCommand` enthält einen einmaligen Sprungwunsch. Der Core prüft
  Ladung und Ziel selbst, wechselt den aktiven Encounter und setzt den
  Schiffszustand am Einstieg zurück. Ein `EncounterChanged`-Ereignis lässt die
  Ansicht alte Effekte verwerfen und verhindert Interpolation zwischen Encountern.
- **Karte als Overlay:** Auswahl und Sichtbarkeit sind reine UI-Zustände.
  Es gibt keinen Szenenwechsel und kein Pausieren des SceneTree beim Öffnen.
- **Eine Physik für alle Schiffe:** Spieler und Gegner enthalten denselben
  `ShipState`; beide werden durch `ShipPhysics.Step` integriert. Der
  `EnemyAiController` besitzt FSM-Zeit, Ausweichrichtung, abgeleiteten Kontext
  und den letzten `ShipCommand`. Er besitzt keine Schnittstelle zum direkten
  Setzen physikalischer Zustände.
- **Eine Waffe für beide Seiten:** Beide besitzen einen `LanceState`. Dieselben
  Funktionen laden und entladen die Lanze und schneiden ihren sofortigen Ray mit
  Schiffen. `WeaponFired` kennzeichnet Schützen und Trefferart.
- **Deterministische FSM:** Sämtliche Schwellen, Hysteresen und PD-Werte stehen in
  `EnemyAiSettings`. Zufall wird nur beim Eintritt in EVADE verwendet und ist
  über den Simulationsseed reproduzierbar. V1.3 verwendet den exakten
  Spielerzustand; eine Sensorabstraktion ist noch nicht vorhanden.
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

Die 56 Core-Tests prüfen unter anderem:

- Geschwindigkeit ohne Schub, Beschleunigung in gedrehter Schiffsausrichtung,
  Weiterflug nach Loslassen, Rückschub und fehlendes Geschwindigkeitslimit.
- Rotationsträgheit, Abbremsen und Umkehren durch Gegendrehmoment,
  Ebenenregel und Quaternion-Normalisierung über 36.000 Ticks.
- Exakte Ladezeit, verworfene frühe Feuerbefehle, Entladung, automatisches
  Nachladen und einmalige Ereignisausgabe.
- Sofortigen Treffer, Fehlschuss, nächstes Ziel, Reichweite, Ziele hinter dem
  Schiff und gedrehte Waffenrichtung.
- Trefferzähler, fehlenden Respawn, sichere und reproduzierbare Platzierung,
  statische Zielpositionen auch bei großer Entfernung sowie zehn Ziele nur in
  Encounter 1.
- Zehn Sekunden Warp-Ladezeit, verworfene frühe und ungültige Sprungbefehle,
  Entladung nach dem Sprung, Ankunft im Stillstand, erhaltene Lanzenladung
  und Fortschritt nach Hin- und Rücksprung.
- Weiterlaufenden Flug und unabhängige Waffenladung während des Warp-Ladens.
- Gegnerplatzierung: Encounter 2 mit Easy-, Encounter 3 mit Medium- und
  Encounter 4 mit Hard-Gegner, ACQUIRE→APPROACH, vollständigen
  `EnemyAiContext`, PD-Gegendrehmoment und Bremsen bei hoher Annäherung.
- Bedrohungsbedingungen aus Ladung, Reichweite und Spielerzielwinkel sowie eine
  reproduzierbare, während EVADE stabile Ausweichrichtung und Mindestdauer.
- Gemeinsame physikalische Grenzen für KI-Bewegung, normale Lanzenladung,
  Spieler- und Gegnerzerstörung, deaktivierte DESTROYED-KI und vollständiges
  Einfrieren aller Systeme nach Game Over.
- Gültige und abgewiesene Power-Zuweisungen, linearen Schub und Drehmoment bei
  halber oder fehlender Antriebsleistung sowie Waffenladezeit und erhaltene Ladung.
- Schildabsorption, Restschaden, Recharge Delay, Regeneration, fehlende
  Schildenergie und reale EVADE-Power-Profile der Gegner.
- Zurückweisen ungültiger physikalischer Parameter.

## Aktuelle Einschränkungen

- Nur X/Z-Bewegung und Yaw; keine Kollisionen, Schäden, Sensoranalyse,
  Energieversorgung, Hardwarekommunikation, Netzwerk, Audio oder Speicherung.
- Ein Easy-Gegner in Encounter 2, ein Medium-Gegner in Encounter 3 und ein Hard-Gegner in Encounter 4; keine
  Gruppenkoordination, Formationen, Kollisions- oder Hindernisvermeidung, Schilde,
  Trefferpunkte oder Teilsystemschäden.
- Die KI kennt den exakten Spielerzustand. Sensorfehler, Stealth, ECM,
  Kontaktverlust, Lernverfahren und Schwierigkeitsgrade fehlen bewusst.
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
2. Encounter-Inhalte und Gegnerverhalten erweitern und ein vollständiges
   3D-Bewegungsmodell ergänzen.
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

Version **1.2.0** erweitert das Spiel um Gegner-FSM, Game Over und drei
Sprungpunkte: zehn Ziele in Encounter 1, einen Gegner in Encounter 2 und zwei
Gegner in Encounter 3. Der Stand ist mit dem annotierten Git-Tag **`v1.2`**
gesichert; die Details stehen in [SpaceSim_v1.2_Changelog.txt](SpaceSim_v1.2_Changelog.txt).

Version **1.3.0** ist als Commit gespeichert und erweitert das Spiel um Energieverteilung und Schilde. V1.4 bis V1.6.1 erweitern diesen Stand um Hull, Subsystemschaden, Kollisions- und Explosionsfolgen, Zoom, UI-Verbesserungen, Gegner-Schwierigkeiten und Soundeffekte. V1.8 ersetzt die Armarium-Seitentriebwerksteuerung durch eine begrenzte Lanzenlafette. V1.8.1 ergaenzt deren 1-km-Sektorkarte, V1.8.2 verdichtet die Zielskala und passt die Ansicht ohne Scrollen an. V1.8.3 erweitert die Sektorkarte und zeichnet die Bugausrichtung bis zum Bildschirmrand. V1.8.4 glattet die Zielhilfe und visualisiert bestaetigte Treffer. Der Stand ist fuer `v1.8.4` bereit. Ein Git-Tag wird nur auf ausdrueckliche Anweisung erstellt.

```powershell
git status             # Änderungen seit dem letzten Commit anzeigen
git log --oneline      # Gespeicherte Entwicklungsstände anzeigen
git show v1.0 --stat   # Den gespeicherten Stand von Version 1.0 anzeigen
git show v1.1 --stat   # Den gespeicherten Stand von Version 1.1 anzeigen
git show v1.2 --stat   # Den gespeicherten Stand von Version 1.2 anzeigen
```

Für weitere Arbeit empfiehlt sich ein eigener Branch, zum Beispiel
`git switch -c feature/flugsteuerung`. Neue Änderungen werden anschließend
gezielt mit `git add <Datei>` und `git commit -m "Beschreibung"` gespeichert.
Der Tag `v1.0` bleibt dabei auf dem ursprünglichen Versionsstand.

Godot-Binärdateien, Build-Ausgaben und lokale Caches sind ausgeschlossen.
Ein lokales Git-Repository ersetzt keine Sicherung auf einem anderen Datenträger.

Der ursprüngliche Projektauftrag bleibt unverändert in `Plan` erhalten.
