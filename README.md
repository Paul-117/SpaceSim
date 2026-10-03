# SpaceSim 2.0.0

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
Der Reaktor ist absichtlich nur ein Platzhalter; WÃ¤rme, Brennstoff, Reaktortypen,
Kopplungsstufen und Overdrive existieren noch nicht.

Die Wirkung ist linear: Antriebsleistung skaliert Haupt-, RÃ¼ckwÃ¤rts- und
Yaw-Schub, Waffenleistung bestimmt die Ladegeschwindigkeit der Lanze und
Schildleistung bestimmt die Regeneration. Bereits vorhandene Bewegung,
Lanzenladung und SchildstÃ¤rke bleiben beim Umverteilen erhalten.

Das HUD bietet rechts **POWER DISTRIBUTION** mit -/+ Buttons in 5-Power-Unit-
Schritten. ErhÃ¶hungen ohne freie Leistung werden abgewiesen. Die Anzeige enthÃ¤lt
auch die SchildstÃ¤rke und den aktuellen Status.

Jeder Schild besitzt 100 Punkte; die Lanze verursacht 100 Schaden. Ein voller
Schild absorbiert einen Treffer, jeder Restschaden zerstÃ¶rt das Schiff, weil
V1.3 noch keine HÃ¼llenpunkte besitzt. Nach einem Schildtreffer gilt ein
dreisekÃ¼ndiger Recharge Delay. Danach regeneriert der Schild mit bis zu 20
Punkten pro Sekunde, abhÃ¤ngig von der Schildleistung.

## Hull, Systemschaden und Speed Limit

Jedes Schiff besitzt nun **3 Hull Integrity**. VollstÃ¤ndig absorbierte
Schildtreffer verÃ¤ndern die HÃ¼lle nicht. Jeder Restschaden entfernt genau einen
Hull-Punkt und beschÃ¤digt mit reproduzierbarem Zufall eines von drei Subsystemen:
Propulsion, Weapons oder Shields. Der Zustand sinkt pro Treffer um 50 Prozent;
bei 0 Prozent ist das System ausgefallen. Hull 0 zerstÃ¶rt Spieler oder Gegner.

Die effektive Leistung jedes Systems lautet **Power Factor x Condition**.
Antriebsschaden skaliert Schub und Drehmoment, Waffenschaden die Laderate und
Schildschaden die Regeneration. Bereits geladene Lanze und bestehende SchildstÃ¤rke
bleiben unveraendert.

Ein erfolgreicher Warp repariert alle drei Subsysteme und fÃ¼llt den Schild auf,
repariert aber keine Hull Integrity. Das Speed Limit ist gameplaybedingt:
`500 m/s x PropulsionPowerFactor x PropulsionCondition`. Es begrenzt nur weitere
beschleunigende Hauptschubimpulse. Eine bereits hÃ¶here Geschwindigkeit bleibt
dank Traegheit erhalten und kann mit Rueckschub
abgebaut werden.

## Schnellstart unter Windows

**`Start.cmd` doppelklicken.** Das Skript baut das Projekt und startet das Spiel.
Alternativ im Workspace:

Der aktuelle Arbeitsstand ist **2.0.0**. Er umfasst Armarium, Voltarium und das neue **Sensorium** als externe Browser-Station. Die Bruecke zeigt Gegner erst nach einer Sensorium-Identifikation oder aktiven Sonaremission. Der Stand ist fuer `v2.0` bereit; ein Git-Tag wird nur auf ausdrueckliche Anweisung erstellt.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start.ps1
```

Zum Ã–ffnen im Editor:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Start.ps1 -Editor
```

Oder `SpaceSim.Godot/project.godot` in Godot .NET importieren und **F5** drÃ¼cken.
Die Testszene startet unmittelbar. Die AusfÃ¼hrungsrichtlinie wird durch die
Skriptaufrufe nur fÃ¼r den jeweiligen PowerShell-Prozess gesetzt.

### Voraussetzungen

- Windows, .NET SDK **10.0.x** (hier geprÃ¼ft mit 10.0.401).
- **Godot 4.7.2 .NET**, im vorhandenen Ordner
  `Godot_v4.7.2-stable_mono_win64/`. Die Standardversion ohne .NET reicht nicht.
- Ein mit Godots Compatibility-Renderer kompatibler Grafiktreiber.

`NuGet.Config` verwendet ausschlieÃŸlich die mitgelieferten Godot-Pakete aus
`GodotSharp/Tools/nupkgs`. Es werden keine zusÃ¤tzlichen NuGet-Pakete verwendet.
Bei einer anderen Godot-Installation die Paketquelle, die SDK-Version im
Godot-Projekt und die Pfade in den Skripten gemeinsam anpassen.

## Schnellstart unter Linux

Vorausgesetzt werden das **.NET SDK 10** und **Godot 4.7.2 .NET fuer Linux**.
Die Godot-Standardversion ohne C#-Unterstuetzung reicht nicht. Das offizielle
Linux-.NET-Archiv von https://godotengine.org/download/linux/ kann direkt in den
Projektordner entpackt werden. Anschliessend:

```bash
./Start_Linux.sh
```

Der Starthelfer findet die entpackte Godot-Datei automatisch, baut das Projekt
mit den darin enthaltenen .NET-Paketen und startet das Spiel. Liegt Godot an
einem anderen Ort, kann die ausfuehrbare Datei explizit angegeben werden:

```bash
GODOT_BIN=/pfad/zu/Godot_v4.7.2-stable_mono_linux.x86_64 ./Start_Linux.sh
```

Der Editor laesst sich mit `./Start_Linux.sh --editor` oeffnen.

## Steuerung

| Taste | Wirkung |
| --- | --- |
| W | Haupttriebwerk: Schub in Richtung der Schiffsnase |
| S | RÃ¼ckwÃ¤rtsschub relativ zur Schiffsausrichtung, halbe Hauptschubkraft |
| A | Drehmoment nach links |
| D | Drehmoment nach rechts |
| Mausrad hoch / runter | Taktische Ansicht hinein- / herauszoomen |
| F | Brueckenlanze einmal abfeuern, wenn bereit |
| Leertaste | Freie taktische Karte ein-/ausschalten |
| W / A / S / D bei freier Karte | Kamera verschieben; das Schiff erhÃ¤lt dabei keinen Schubbefehl. Gegner-Randpfeile sind ausgeblendet, die gestrichelte Kurslinie bleibt sichtbar. |
| Linksklick auf sichtbaren Gegner | Kontakt im rechten `CONTACTS`-Fenster auswÃ¤hlen |
| P | Autopilot fÃ¼r den ausgewÃ¤hlten Kontakt ein-/ausschalten; Waffen bleiben manuell |
| Button â€žWarp Driveâ€œ | Sternenkarte jederzeit Ã¶ffnen |
| Klick auf einen anderen Encounter | Sprungpunkt auswÃ¤hlen |
| Button â€žJumpâ€œ | Zum ausgewÃ¤hlten Encounter springen, sobald der Drive bereit ist |
| Esc / â€žZurÃ¼ck zum Flugâ€œ | Sternenkarte schlieÃŸen |

**Loslassen bremst nicht.** Lineare und Winkelgeschwindigkeit bleiben erhalten.
Zum Bremsen passend gegensteuern. RÃ¼ckwÃ¤rtsschub bremst nur dann die gesamte
Bewegung, wenn die Schiffsnase zur Bewegungsrichtung ausgerichtet ist. Es gibt
weder Reibung noch automatische Stabilisierung. Der aktive Antrieb begrenzt weitere Beschleunigung am aktuellen Speed Limit; bestehende Traegheit bleibt erhalten.

Die Lanze startet ungeladen, lÃ¤dt in drei Simulationssekunden und schieÃŸt entlang
der gestrichelten Visierlinie. Ein zu frÃ¼her Tastendruck wird verworfen; Halten
lÃ¶st kein Dauerfeuer aus. Nach dem Schuss erneut drÃ¼cken. Die erste ZieloberflÃ¤che
innerhalb von 1.000 m trifft die Lanze. Dahinter verursacht sie keinen Treffer.
Der sichtbare Strahl reicht als Effekt bis 3.000 m und fadet ab 1.000 m weich aus.
Ein Treffer zÃ¤hlt sofort und entfernt das Ziel dauerhaft fÃ¼r diese Sitzung. Der
sichtbare Strahl bleibt nur 0,16 Sekunden bestehen. Auch beim Wegfliegen oder
ZurÃ¼ckspringen werden keine Ziele ersetzt.

## Armarium, Voltarium und Sensorium Station Server (V2.0)

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
im Browser sendet genau einen `fire_lance`-Befehl. Die lokale Bruecke feuert mit
**F**; die Leertaste des Armariums bleibt weiterhin dessen eigener Fire-Command.

Die kompakte Waffenzeile zeigt rechts LANCE, Ladebalken und Lafettenwinkel. Links
steht das Armarium-Energiefenster mit dem vom Core zugewiesenen Budget in PU und
dem Waffenstatus ONLINE, LIMITED oder OFFLINE. Der Status folgt Energie-Budget und
Weapons-Subsystemzustand.

Der Haupt-PC bleibt autoritativ: Der Browser erhaelt nur `targetAvailable`,
`targetBearingDegrees`, `targetDistanceMeters`, `lanceCharge`, `lanceReady`,
`lanceTurretAngleDegrees`, `availablePower`, `maximumPower`, `lanceSystemCondition`
und `simulationTick`.
Er entscheidet nicht ueber Waffenfeuer oder Treffer. Der Fire Command wird im
Haupt-PC thread-sicher gepuffert und erst im normalen `ShipCommand` des naechsten
Simulationsticks verarbeitet. Das Armarium nutzt derzeit die exakte Gegnerposition;
eine spaetere Sensorium-Quelle kann den Bearing liefern, ohne den Webclient zu aendern.

Das WebSocket-Protokoll hat Version **9**. Der Client identifiziert sich beim
Verbindungsaufbau als `armarium`, zeigt `ARMARIUM ONLINE/OFFLINE` und verbindet sich
nach einem Abbruch automatisch erneut. Details stehen in
[StationProtocol.md](docs/StationProtocol.md).

### Voltarium

Das Voltarium ist unter `http://127.0.0.1:47870/voltarium/` erreichbar. Sein
Operating-Level-Schieberegler setzt den autoritativen **Sollwert** von 0 bis
100 Prozent. Die physische Reaktorleistung folgt diesem Sollwert trÃ¤ge und benÃ¶tigt
fÃ¼r den Weg von 0 auf 100 Prozent exakt **60 Simulationssekunden**. Bei voller
Leistung liefert der Reaktor **125 PU**. Die Voltarium-Ansicht verwendet wieder
den wabbernden Reaktorkern aus dem Stationsprototyp; seine GrÃ¶ÃŸe und Bewegung
folgen dem vom Core Ã¼bertragenen tatsÃ¤chlichen Output.

Das Voltarium verteilt diese Leistung mit drei Slidern: **Bruecke**, **Schilde**
und **Armarium**. Die Werte sind Prozent des aktuellen Reaktor-Outputs und zeigen
direkt daneben die daraus autoritativ abgeleiteten PU. Die Anfangsverteilung
40/28/32 Prozent nutzt bei 125 PU die Stationsmaxima von 50/35/40 PU. Ein Slider
kann weder Ã¼ber 100 Prozent Gesamtzuweisung noch Ã¼ber das PU-Limit seiner Station
steigen. FÃ¼r eine hÃ¶here Zuweisung muss zuerst eine andere Station reduziert
werden. Nicht zugewiesene Prozent bleiben ungenutzt.

Der Reaktor besitzt **100 U Fuel**. Solange der Reaktor Leistung erzeugt, verbraucht
er Fuel abhÃ¤ngig vom tatsÃ¤chlichen Betriebslevel: bei minimaler aktiver Leistung
0,2 U/min, bei 100 Prozent 7,0 U/min mit quadratischem Verlauf dazwischen. Bei
0 U wird der Reaktor im Core abgeschaltet; Output und gelieferte Stationsleistung
werden 0. Eine Betankung existiert derzeit noch nicht. Die drei Stationen
fordern ihre Leistung innerhalb ihres Voltarium-Budgets selbst an: Bruecke maximal
50 PU, Armarium maximal 40 PU und Schilde maximal 35 PU. Der Core liefert keiner
Station mehr Energie als ihr zugewiesenes Budget.

Die Bruecke besitzt keine Power-Distribution-Buttons mehr. Ihre verfÃ¼gbare
Stationsleistung wird ausschlieÃŸlich im Voltarium zugewiesen. W rampet beim
Gedrueckthalten als Gas bis zur maximalen Main-Thruster-Anforderung von 20 PU.
Fuel, Ramp, Zuweisung und Abschaltung werden ausschlieÃŸlich vom Simulations-Core
berechnet; das Voltarium zeigt nur dessen Snapshot und sendet Zuweisungsabsichten.

Innerhalb der Antriebsstation sind **30 PU** dauerhaft fÃ¼r RÃ¼ckwÃ¤rts- und
Seitentriebwerke reserviert: S, A und D besitzen jeweils 10 PU PrioritÃ¤t, auch
wenn sie gerade nicht betÃ¤tigt werden. Von maximal 50 PU bleiben W damit hÃ¶chstens
20 PU. Bei 35 PU Stationsleistung erhÃ¤lt W folglich 5 PU. Sinkt die verfÃ¼gbare
Stationsleistung unter 30 PU, erhalten S, A und D jeweils denselben linearen
Leistungsfaktor; W erhÃ¤lt dann keine Leistung.

### Sensorium

Das Sensorium ist unter `http://127.0.0.1:47870/sensorium/` erreichbar; im LAN
wird `127.0.0.1` durch die IP des Haupt-PCs ersetzt. Es verwendet das vorhandene
HTML/CSS/Canvas-Frontend und verbindet sich mit dem Station Server per WebSocket
Protokoll **9**. Die Station sendet ausschliesslich Sensorabsichten; die
autoritative Simulation entscheidet ueber die daraus entstehenden Brueckenkontakte.

Das aktive Sonar (`O`) zeigt alle Gegner des aktuellen Encounters sofort als rote
Kontakte mit exakter relativer Peilung und Entfernung. Die passive Ansicht steuert
mit `A`/`D` eine Spektrometerachse. Sie zeigt die echte Systemspektrum-Signatur des
am besten ausgerichteten Gegners; ausserhalb von **+-10 Grad** ist das Signal stark
gedaempft. Mit den Pfeiltasten wird die Bibliothek CETUS/Corvette,
ARGUS/Frigate und ATLAS/Cruiser gewaehlt. `Enter` bestaetigt eine passende Signatur,
`L` wechselt danach zwischen den bestaetigten Kontakten. `P` schaltet die
Spektrometer-Nachfuehrung auf den aktuell rechts dargestellten bestaetigten Kontakt.
Sie bleibt auch bei aktivem Sonar aktiv; manuelles A/D schaltet sie wieder aus. Die Kontaktdaten enthalten
Name, Klasse, vier Systemleistungen, exakte Sonarentfernung und Hull.

Beim Eintritt in ein Gefecht besitzt die Bruecke zunaechst keine Gegnerkontakte:
keine Kartendarstellung, Randanzeige, Contacts-Information oder Autopilot-Ziel.
Eine gueltige Spektrometer-Bestaetigung (`Enter` bei passender Bibliothek und
Ausrichtung) uebergibt den Kontakt an die Bruecke; die Bestaetigung bleibt fuer den
laufenden Encounter erhalten. Eine aktive Sonaremission gibt der Bruecke sofort alle
lokalen Positionen und Kontaktinformationen frei und alarmiert zugleich alle Gegner.

Bei der Eintrittsplanung im Hyperraum markiert die Karte eine **LAST KNOWN POSITION**.
Sie wird beim Beginn der Planung aus der echten Gegnerposition mit einer Unsicherheit
von maximal 300 m erzeugt. Die angezeigte Distanzlinie endet an diesem bekannten
Punkt, nicht an der aktuellen echten Gegnerposition.

Beide Sensoransichten sind fest auf Welt-Norden ausgerichtet und drehen sich nicht
mit dem Bug des Spielerschiffs. Jedes Umschalten der aktiven Sonaransicht sendet
eine aktive Emission aus: Alle Gegner im aktuellen Encounter werden dadurch sofort
alarmiert und gehen in denselben Kampfzustand wie bei einer Nahdistanz-Erkennung.

### Enemy AI Debug

Die schreibgeschuetzte Entwicklungsstation ist unter
`http://127.0.0.1:47870/debug/` erreichbar; im LAN wird `127.0.0.1` durch die
IP des Haupt-PCs ersetzt. Sie enthaelt keine Bedienbefehle und kann den
Spielzustand nicht veraendern. Fuer den aktiven Gegner zeigt sie seine
Erkennung (**PATROL / UNAWARE** oder **COMBAT / DETECTED**), FSM-Zustand,
Distanz, Annaeherungs- und Relativgeschwindigkeit, Hull, Schild,
Lanzenladung, Subsystemzustand sowie Reaktor- und Energieprofil. Existiert im
aktuellen Encounter kein Gegner, zeigt die Seite das explizit an. Die Daten sind
ein gezielter Debug-Snapshot und kein uebertragener `WorldState`.

### Bruecke 1.9.1

Das bisherige Fenster **ENERGY** oben rechts ist entfernt. Die bisherigen Treffer-
und Lanzenboxen sind ebenfalls nicht mehr Teil der Bruecke, damit diese Informationen
spÃ¤ter als eigene Stationen laufen kÃ¶nnen.

Unten links zeigt die Triebwerksbox **MAIN THRUSTERS**, **STARBOARD THRUSTERS**,
**PORT THRUSTERS** und **REVERSE THRUSTERS**. Ganz oben steht die fÃ¼r die Station
verfÃ¼gbare Energie in PU. Die Hilfstriebwerke stehen vor den Main Thrusters; der
blaue Main-Thrust-Balken liegt am unteren Rand und zeigt die tatsÃ¤chlich wirksame
VorwÃ¤rtsleistung von W. Jeder Eintrag zeigt seinen Energiezustand: **ONLINE** in
GrÃ¼n bei voller Leistung, **LIMITED** in Gelb bei Drosselung und **OFFLINE** in Rot
ohne Leistung. Main Thrusters sind erst bei 50 verfÃ¼gbaren PU vollstÃ¤ndig online;
die drei Hilfstriebwerke benÃ¶tigen zusammen 30 PU. Nur **W** baut seinen Schub Ã¼ber **5 Sekunden**
bis 100 Prozent auf und nach dem Loslassen Ã¼ber **3 Sekunden** wieder ab.
RÃ¼ckwÃ¤rtsschub mit S und die Seitentriebwerke mit A/D reagieren sofort. Positions-,
Zoom-, IntegritÃ¤ts-, Kurs- und Simulationszeitdaten stehen unten rechts.

Der **Warp Drive** befindet sich oben mittig. Seine SchaltflÃ¤che bleibt anklickbar
und ist selbst der Ladebalken: Die FÃ¼llung zeigt den aktuellen Warp-Ladestand. Der
frÃ¼here Text mit Prozentwert beziehungsweise `READY` wird nicht mehr zusÃ¤tzlich
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

Die Zustandsmaschine verwendet `ACQUIRE`, `APPROACH`, `ATTACK` und `REPOSITION`.
Sie kennt keinen Risk-Level und keinen EVADE-State. Gegner schaetzen die eigene
Hull-, Schild- und Systemlage nicht taktisch ein; sie verwenden REPOSITION nur,
um Entfernung, Relativgeschwindigkeit und drohende Kollisionen zu korrigieren.

### Gegnerpatrouille und Anflug 1.9.6

Jeder Gegner beginnt zwei bis drei Kilometer vom lokalen Einstiegspunkt entfernt
auf einem reproduzierbar zufaelligen Kurs mit 100 m/s Reisegeschwindigkeit. Seine
Nase zeigt dabei in die Reiserichtung. Er hat
den Spieler zunaechst nicht entdeckt und laesst die vorhandene Kampf-FSM in
`ACQUIRE`. Sein Reaktor startet bei 50 Prozent. Das Patrouillenprofil fordert
50 PU nur fuer Propulsion, ohne Waffen- oder Schildleistung. Sein Schild beginnt
leer und regeneriert in der Patrouille nicht.

Die Erkennungsdistanz richtet sich nach der tatsaechlichen Reaktorleistung des
Spielers: bei 100 Prozent betraegt sie 2.000 m, bei 50 Prozent 1.000 m; die
Werte dazwischen werden linear berechnet. Sobald der Gegner den Spieler erkennt,
setzt er den Reaktor-Sollwert auf 100 Prozent und ramped dann mit der normalen
Reaktorrate von 50 auf 100 Prozent. Dabei fordert er die maximale Leistung aller
drei Stationen an: 50 PU Propulsion, 40 PU Weapons und 35 PU Shields.
Anschliessend geht er unmittelbar ueber `ACQUIRE` nach `APPROACH`. Die beim
Hochfahren verfuegbare Leistung wird wie beim Spieler proportional begrenzt.

Jeder Gegner besitzt einen festen Kontaktnamen und eine Kontaktklasse. Der Name
steht auf der taktischen Karte ueber dem Schiff. Ein Linksklick auf ein sichtbares
Gegnerschiff waehlt ihn fuer das rechte **CONTACTS**-Fenster aus. Dieses zeigt
Name, Klasse, Distanz, Relativgeschwindigkeit, aktuelle Reaktorleistung,
Shield- und Weapons-Status sowie Hull Integrity. Die Auswahl ist reine
Bruecken-Darstellung und veraendert weder Gegner-KI noch Simulationszustand.

Mit **P** kann die Bruecke den Autopiloten fuer den ausgewaehlten Kontakt
einschalten. Er verwendet dieselben Flugregeln wie die Gegner-KI: Vorhalteflug,
kontrollierte AnnÃ¤herung, Bremsen, Reposition und kollisionssichere Fly-bys.
Er erzeugt ausschliesslich normale Flug-`ShipCommand`-Befehle und feuert nie.
Lanzenfeuer und Lafettensteuerung bleiben beim Spieler beziehungsweise Armarium.
Die Box oberhalb von `CONTACTS` zeigt `AUTOPILOT: ACTIVE` und den festgelegten
Zielnamen. Wird das Ziel zerstoert oder der Encounter gewechselt, deaktiviert
sich der Autopilot automatisch.

`APPROACH` sagt den Spielerort voraus und plant eine Ankunft nahe der bevorzugten
Kampfentfernung von 600 m. Die erlaubte AnnÃ¤herungsgeschwindigkeit ergibt sich
aus der aktuell mÃ¶glichen Reverse-Bremsleistung und ist auf 55 m/s begrenzt.
Bei einer vorhergesagten AnnÃ¤herung von mehr als 35 m/s mit weniger als 180 m
nÃ¤chstem Abstand plant die KI einen tangentialen Fly-by: Sie lenkt mit normalen
Yaw- und Haupttriebwerksbefehlen seitlich um, statt umzudrehen und direkt vor dem
Spieler gegenzubremsen. Ein sicherer Fly-by darf im Bereich von 250 bis 900 m
bereits angreifen und im Vorbeiflug feuern.

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

Der Warp Drive lÃ¤dt beim Spielstart und nach jedem Sprung automatisch in
**10 Simulationssekunden** auf. â€žWarp Driveâ€œ Ã¶ffnet die Karte unabhÃ¤ngig von der
Ladung. WÃ¤hle dort einen anderen Encounter und bestÃ¤tige mit **Jump**.
Ohne Auswahl, wÃ¤hrend des Ladens oder fÃ¼r den aktuellen Encounter ist Jump
gesperrt. Auswahl allein lÃ¶st keinen Sprung aus; frÃ¼he Sprungbefehle werden
verworfen und nicht fÃ¼r spÃ¤ter vorgemerkt.

**Die Sternenkarte pausiert das Spiel nicht.** Flug, Rotation, Waffenladung und
Warp-Ladung laufen weiter. W/S, A/D und F bleiben bedienbar. Die Buttons
Ã¼bernehmen keinen Tastaturfokus, damit Tastatureingaben keinen Sprung auslÃ¶sen.

Nach einem Sprung schlieÃŸt sich die Karte. Das Schiff startet am lokalen
Einstiegspunkt `(0, 0, 0)`, mit Standardausrichtung und ohne lineare oder
Winkelgeschwindigkeit. Lanzenladung und Gesamttrefferzahl bleiben erhalten.
Alle Encounter behalten ihre verbliebenen Ziele, Gegner und Treffer auch bei
spÃ¤teren Besuchen. Nach dem letzten Treffer bleibt ein Encounter leer und weiter
besuchbar; ein Sprung erfordert nicht, vorher alle Ziele zu zerstÃ¶ren.
Dieser Fortschritt gilt fÃ¼r die laufende Sitzung; ein Savegame existiert nicht.

Das HUD zeigt den aktuellen Encounter, Geschwindigkeit, Winkelgeschwindigkeit,
Lanzenladung/READY, Gesamttreffer, lokalen Fortschritt, Warp-Ladung und Restzeit,
Position und Kurs. Der tÃ¼rkisfarbene Pfeil zeigt die Bewegungsrichtung;
orange Markierungen kennzeichnen Ziele. Bei Bedarf weist ein Randpfeil zum
nÃ¤chsten Ziel. Bei Fokusverlust wird die Eingabe deaktiviert, die Simulation
lÃ¤uft weiter.

## Bauen und testen

Alle Befehle aus dem Workspace-Stamm:

```powershell
dotnet build SpaceSim.sln
dotnet run --project SpaceSim.Core.Tests
```

Die Core-Tests sind ein kleiner Konsolen-TestlÃ¤ufer ohne Testframework-Paket.
Jeder Fall meldet PASS/FAIL; bei Fehlern ist der Exitcode 1. `dotnet test` ist
deshalb hier nicht der Testbefehl. Die Tests benÃ¶tigen keinen Godot-Prozess,
keine Grafik und keinen laufenden Editor.

Build, Core-Tests und Godot-Integrationstest gemeinsam:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test.ps1 -GodotSmoke
```

Die Integrationstests importieren das Projekt headless und starten die echte
Szene. Ein kurzer Test simuliert 240 Ticks mit Schuss, Treffer ohne Respawn, Schub
und Drehmoment. Der Warp-Test bedient die echten Buttons mit Mausereignissen:
Karte wÃ¤hrend des Ladens Ã¶ffnen, Ziel wÃ¤hlen, gesperrten Jump prÃ¼fen, bei laufendem
Flug laden, nach Encounter 2 und wieder zurÃ¼ck springen, Fortschritt prÃ¼fen.
Er dauert 1.205 Ticks, also etwa 20 Simulationssekunden.
Beide beenden sich automatisch mit Exitcode 0 und `SMOKE PASS` bzw.
`WARP SMOKE PASS`, wenn die PrÃ¼fungen bestehen. Ein dritter Test springt zu
Encounter 2, lÃ¤sst die Gegner-KI mit ihrer Lanze gewinnen und prÃ¼ft Game Over,
Stillstand und Neustart. Das Skript prÃ¼ft zusÃ¤tzlich die
Godot-Logs auf Fehler. Logs liegen in
`.artifacts/`. Ohne `-GodotSmoke` werden nur Build und Core-Tests ausgefÃ¼hrt;
`-SkipBuild` verwendet den bestehenden Debug-Build.

Optionaler visueller PrÃ¼flauf mit automatischem Screenshot und anschlieÃŸendem
Beenden (nach dem Build, mit grafischer Sitzung):

```powershell
New-Item -ItemType Directory -Force .artifacts
$capture = Join-Path $PWD '.artifacts/flight.png'
& ./Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --path SpaceSim.Godot -- "--capture=$capture"
```

`--capture` benÃ¶tigt den grafischen Renderer und darf nicht mit `--headless`
verwendet werden. Zusammen mit `--smoke-test` wird der Trefferstrahl erfasst;
zusammen mit `--warp-smoke-test` die geÃ¶ffnete Sternenkarte wÃ¤hrend des Ladens.

## Architektur

```text
KeyboardShipControl : IShipControl     StarMap: Ziel wÃ¤hlen + Jump
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
Die Weltmodelle sind von auÃŸen nur lesbar. Kein Node und kein Sprite entscheidet
Ã¼ber Physik, Waffenladung oder Treffer. Der Core ist eine normale C#-Bibliothek
mit `System.Numerics.Vector3` und `Quaternion`, ohne Godot-AbhÃ¤ngigkeit. Dadurch
kann dieselbe Simulation spÃ¤ter grundsÃ¤tzlich in einem Headless-Server laufen.

### Projektstruktur

```text
SpaceSim.sln
SpaceSim.Core/
  Simulation/    Simulation, Einstellungen, WorldState, Events
  Ships/         3D-Schiffszustand, Commands, Steuerungsschnittstelle, Physik
  Weapons/       Lanzenladung und Ray/Sphere-Trefferberechnung
  Targets/       Statische Ziele und einmalige zufÃ¤llige Platzierung
  Navigation/    Encounter-Zustand, Sprungbefehl und Warp Drive
  Combat/        Gegnerzustand und globaler Spielzustand
  AI/            Kontext, zentrale Parameter und deterministische FSM
SpaceSim.Core.Tests/
  Program.cs     Automatisierte Physik-, Waffen- und Zieltests
SpaceSim.Godot/
  project.godot
  SpaceSim.Godot.sln  Projektmappe fÃ¼r den Godot-Editor
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

- **SI-Einheiten:** Meter, Sekunden, Newton, kg, rad/s und kgÂ·mÂ².
  Werte wie Masse (12.000 kg), Schub (144.000 N), RÃ¼ckschub (72.000 N), Drehmoment,
  Waffenreichweite und Spawnradien stehen zentral in
  `SpaceSim.Core/Simulation/SimulationSettings.cs`.
- **Build-Konfigurationen:** Die Projektmappen verwenden Godots `Debug`,
  `ExportDebug` und `ExportRelease`; der Core wird dabei passend als Debug oder
  Release gebaut. `scripts/Build.ps1 -Configuration ExportRelease` erstellt die
  optimierten Assemblies. Ein eigenstÃ¤ndiger Spiel-Export ist noch nicht enthalten.
- **3D-Daten, planare Regel:** Position, Geschwindigkeit und Winkelgeschwindigkeit
  sind Vector3; Orientierung ist ein Quaternion. Version 1.3 hÃ¤lt Y = 0 und dreht
  nur um Y. Die Nase zeigt lokal nach -Z; positives Y-Drehmoment dreht nach links.
  Godot projiziert X/Z auf Bildschirm-X/Y. FÃ¼r vollstÃ¤ndiges 3D mÃ¼ssen Integrator,
  TrÃ¤gheitsmodell und Commands erweitert werden, nicht das Zustandsformat ersetzt.
- **Fester Takt:** Ein `Step` entspricht exakt 1/60 Simulationssekunde.
  Godot ruft ihn aus `_PhysicsProcess` auf. Semi-implizites Euler integriert
  Geschwindigkeit und Position; Quaternionen werden normalisiert. Die Darstellung
  interpoliert zwischen Ticks und schreibt dabei nichts in die Simulation zurÃ¼ck.
- **Neutrale Eingabe:** Ein `IShipControl` liefert einen `ShipCommand`. Ein anderer
  Adapter kann spÃ¤ter Hardware, Netzwerk oder Autopilot Ã¼bersetzen. TastenzustÃ¤nde
  werden ausschlieÃŸlich im Godot-Adapter gelesen. `FireLance` ist ein Impuls.
- **Einfache Events:** `WeaponFired`, `TargetHit`, `TargetSpawned`,
  `EncounterChanged`, `EnemyDestroyed` und `PlayerDestroyed` sind unverÃ¤nderliche
  C#-Records. `Simulation.Events` gilt fÃ¼r
  den gerade abgeschlossenen Tick und muss vor dem nÃ¤chsten Step verarbeitet
  werden. Direkt nach dem Konstruktor enthÃ¤lt es die initialen Spawnereignisse
  des Ziel-Encounters, jeweils mit Encounter-ID.
  Dies ist eine lokale Ausgabe ohne Event-Bus oder Netzwerkgarantien.
- **Vier persistente Encounter:** Encounter 1 erzeugt einmalig zehn statische
  Ziele im Ring zwischen 180 und 750 m. Encounter 2 besitzt einen Easy-Gegner,
  Encounter 3 einen Medium-Gegner und Encounter 4 einen Hard-Gegner. Alle
  Encounter haben eigene lokale Zustaende. Treffer entfernen Ziele oder Gegner nur
  aus dem aktiven Encounter. Es gibt weder Respawn noch Recycling.
  Ein fester, konfigurierbarer Seed macht Testlaeufe reproduzierbar.
- **Warp im Core:** Der unabhÃ¤ngige `WarpDriveState` lÃ¤dt mit dem festen Takt.
  `NavigationCommand` enthÃ¤lt einen einmaligen Sprungwunsch. Der Core prÃ¼ft
  Ladung und Ziel selbst, wechselt den aktiven Encounter und setzt den
  Schiffszustand am Einstieg zurÃ¼ck. Ein `EncounterChanged`-Ereignis lÃ¤sst die
  Ansicht alte Effekte verwerfen und verhindert Interpolation zwischen Encountern.
- **Karte als Overlay:** Auswahl und Sichtbarkeit sind reine UI-ZustÃ¤nde.
  Es gibt keinen Szenenwechsel und kein Pausieren des SceneTree beim Ã–ffnen.
- **Eine Physik fÃ¼r alle Schiffe:** Spieler und Gegner enthalten denselben
  `ShipState`; beide werden durch `ShipPhysics.Step` integriert. Der
  `EnemyAiController` besitzt FSM-Zeit, Ausweichrichtung, abgeleiteten Kontext
  und den letzten `ShipCommand`. Er besitzt keine Schnittstelle zum direkten
  Setzen physikalischer ZustÃ¤nde.
- **Eine Waffe fÃ¼r beide Seiten:** Beide besitzen einen `LanceState`. Dieselben
  Funktionen laden und entladen die Lanze und schneiden ihren sofortigen Ray mit
  Schiffen. `WeaponFired` kennzeichnet SchÃ¼tzen und Trefferart.
- **Deterministische FSM:** SÃ¤mtliche Schwellen, Hysteresen und PD-Werte stehen in
  `EnemyAiSettings`. Zufall wird nur beim Eintritt in EVADE verwendet und ist
  Ã¼ber den Simulationsseed reproduzierbar. V1.3 verwendet den exakten
  Spielerzustand; eine Sensorabstraktion ist noch nicht vorhanden.
- **Treffer unabhÃ¤ngig von Grafik:** Der Core schneidet einen vorwÃ¤rtsgerichteten,
  reichweitenbegrenzten Ray mit den Zielkugeln. Godot verwendet nur das resultierende
  Ereignis fÃ¼r Strahl und Trefferring. Es gibt keine Godot-Physik-Kollisionen.
- **Begrenzte Hintergrundkosten:** Der Sternenhimmel besteht aus gehashten,
  prozeduralen Kacheln mit zwei Parallaxebenen; die Kamera folgt dem Schiff.
  Es gibt keinen sichtbaren Kartenrand und keinen unendlich wachsenden Objektpool.
- **SpÃ¤tere Stationen:** Das lokale Debug-HUD darf den vollstÃ¤ndigen Zustand
  lesen. ZukÃ¼nftige externe Stationen sollten ausschlieÃŸlich gezielte, vom Core
  abgeleitete Systemausgaben erhalten. Der WorldState ist keine Netzwerk-API;
  Sensorfilter und Transportprotokolle sind noch nicht implementiert.

## Testumfang

Die 56 Core-Tests prÃ¼fen unter anderem:

- Geschwindigkeit ohne Schub, Beschleunigung in gedrehter Schiffsausrichtung,
  Weiterflug nach Loslassen, RÃ¼ckschub und fehlendes Geschwindigkeitslimit.
- RotationstrÃ¤gheit, Abbremsen und Umkehren durch Gegendrehmoment,
  Ebenenregel und Quaternion-Normalisierung Ã¼ber 36.000 Ticks.
- Exakte Ladezeit, verworfene frÃ¼he Feuerbefehle, Entladung, automatisches
  Nachladen und einmalige Ereignisausgabe.
- Sofortigen Treffer, Fehlschuss, nÃ¤chstes Ziel, Reichweite, Ziele hinter dem
  Schiff und gedrehte Waffenrichtung.
- TrefferzÃ¤hler, fehlenden Respawn, sichere und reproduzierbare Platzierung,
  statische Zielpositionen auch bei groÃŸer Entfernung sowie zehn Ziele nur in
  Encounter 1.
- Zehn Sekunden Warp-Ladezeit, verworfene frÃ¼he und ungÃ¼ltige Sprungbefehle,
  Entladung nach dem Sprung, Ankunft im Stillstand, erhaltene Lanzenladung
  und Fortschritt nach Hin- und RÃ¼cksprung.
- Weiterlaufenden Flug und unabhÃ¤ngige Waffenladung wÃ¤hrend des Warp-Ladens.
- Gegnerplatzierung: Encounter 2 mit Easy-, Encounter 3 mit Medium- und
  Encounter 4 mit Hard-Gegner, ACQUIREâ†’APPROACH, vollstÃ¤ndigen
  `EnemyAiContext`, PD-Gegendrehmoment und Bremsen bei hoher AnnÃ¤herung.
- Bedrohungsbedingungen aus Ladung, Reichweite und Spielerzielwinkel sowie eine
  reproduzierbare, wÃ¤hrend EVADE stabile Ausweichrichtung und Mindestdauer.
- Gemeinsame physikalische Grenzen fÃ¼r KI-Bewegung, normale Lanzenladung,
  Spieler- und GegnerzerstÃ¶rung, deaktivierte DESTROYED-KI und vollstÃ¤ndiges
  Einfrieren aller Systeme nach Game Over.
- GÃ¼ltige und abgewiesene Power-Zuweisungen, linearen Schub und Drehmoment bei
  halber oder fehlender Antriebsleistung sowie Waffenladezeit und erhaltene Ladung.
- Schildabsorption, Restschaden, Recharge Delay, Regeneration, fehlende
  Schildenergie und reale EVADE-Power-Profile der Gegner.
- ZurÃ¼ckweisen ungÃ¼ltiger physikalischer Parameter.

## Aktuelle EinschrÃ¤nkungen

- Nur X/Z-Bewegung und Yaw; keine Kollisionen, SchÃ¤den, Sensoranalyse,
  Energieversorgung, Hardwarekommunikation, Netzwerk, Audio oder Speicherung.
- Ein Easy-Gegner in Encounter 2, ein Medium-Gegner in Encounter 3 und ein Hard-Gegner in Encounter 4; keine
  Gruppenkoordination, Formationen, Kollisions- oder Hindernisvermeidung, Schilde,
  Trefferpunkte oder TeilsystemschÃ¤den.
- Die KI kennt den exakten Spielerzustand. Sensorfehler, Stealth, ECM,
  Kontaktverlust, Lernverfahren und Schwierigkeitsgrade fehlen bewusst.
- Single-Precision-Koordinaten ohne Origin-Rebasing. Sehr groÃŸe Entfernungen und
  extrem hohe Geschwindigkeiten verschlechtern numerische und grafische PrÃ¤zision.
- Eine konstante Masse und ein Yaw-TrÃ¤gheitsmoment; kein vollstÃ¤ndiger 3D-Tensor.
- Unter starker Rechnerlast kann die Simulationszeit langsamer als die reale
  Zeit laufen. Das feste Integrationsintervall bleibt dabei erhalten.
- Das minimale HUD ist fÃ¼r Desktop-Fenster ausgelegt. Eine vollstÃ¤ndige
  BedienoberflÃ¤che, Pausefunktion und ein Export-Installer fehlen noch.
- Der Godot-BinÃ¤rordner, Build-Ausgaben und Caches sind in `.gitignore`
  ausgeschlossen. FÃ¼r einen neuen Checkout wird Godot .NET separat benÃ¶tigt.

## Kurze Roadmap

1. FluggefÃ¼hl und physikalische Parameter im Testgefecht abstimmen.
2. Encounter-Inhalte und Gegnerverhalten erweitern und ein vollstÃ¤ndiges
   3D-Bewegungsmodell ergÃ¤nzen.
3. Stationsspezifische Systemausgaben entwerfen, anschlieÃŸend einen ersten
   Hardware- oder Netzwerkadapter anbinden.
4. Sensoren, Energie und SchÃ¤den einzeln als testbare Core-Systeme entwickeln.

## Git und VersionsstÃ¤nde

Der erste spielbare Stand ist als Version **1.0.0** auf dem Branch `main`
mit dem annotierten Git-Tag **`v1.0`** gesichert. Das Repository ist lokal;
ein Remote-Repository ist noch nicht eingerichtet.

Version **1.1.0** ist mit dem annotierten Tag **`v1.1`** gesichert.
Die Ã„nderungen gegenÃ¼ber v1.0 stehen in [SpaceSim_v1.1_Changelog.txt](SpaceSim_v1.1_Changelog.txt).
Der bestehende Tag `v1.0` bleibt unveraendert und enthÃ¤lt weiterhin die
ursprÃ¼ngliche Version ohne Warp Drive. `SpaceSim_v1.0_Uebersicht.txt`
dokumentiert diesen historischen Stand.

Version **1.2.0** erweitert das Spiel um Gegner-FSM, Game Over und drei
Sprungpunkte: zehn Ziele in Encounter 1, einen Gegner in Encounter 2 und zwei
Gegner in Encounter 3. Der Stand ist mit dem annotierten Git-Tag **`v1.2`**
gesichert; die Details stehen in [SpaceSim_v1.2_Changelog.txt](SpaceSim_v1.2_Changelog.txt).

Version **1.3.0** ist als Commit gespeichert und erweitert das Spiel um Energieverteilung und Schilde. V1.4 bis V1.6.1 erweitern diesen Stand um Hull, Subsystemschaden, Kollisions- und Explosionsfolgen, Zoom, UI-Verbesserungen, Gegner-Schwierigkeiten und Soundeffekte. V1.8 ersetzt die Armarium-Seitentriebwerksteuerung durch eine begrenzte Lanzenlafette. V1.8.1 ergaenzt deren 1-km-Sektorkarte, V1.8.2 verdichtet die Zielskala und passt die Ansicht ohne Scrollen an. V1.8.3 erweitert die Sektorkarte und zeichnet die Bugausrichtung bis zum Bildschirmrand. V1.8.4 glattet die Zielhilfe und visualisiert bestaetigte Treffer. Der Stand ist fuer `v1.8.4` bereit. Ein Git-Tag wird nur auf ausdrueckliche Anweisung erstellt.

```powershell
git status             # Ã„nderungen seit dem letzten Commit anzeigen
git log --oneline      # Gespeicherte EntwicklungsstÃ¤nde anzeigen
git show v1.0 --stat   # Den gespeicherten Stand von Version 1.0 anzeigen
git show v1.1 --stat   # Den gespeicherten Stand von Version 1.1 anzeigen
git show v1.2 --stat   # Den gespeicherten Stand von Version 1.2 anzeigen
```

FÃ¼r weitere Arbeit empfiehlt sich ein eigener Branch, zum Beispiel
`git switch -c feature/flugsteuerung`. Neue Ã„nderungen werden anschlieÃŸend
gezielt mit `git add <Datei>` und `git commit -m "Beschreibung"` gespeichert.
Der Tag `v1.0` bleibt dabei auf dem ursprÃ¼nglichen Versionsstand.

Godot-BinÃ¤rdateien, Build-Ausgaben und lokale Caches sind ausgeschlossen.
Ein lokales Git-Repository ersetzt keine Sicherung auf einem anderen DatentrÃ¤ger.

Der ursprÃ¼ngliche Projektauftrag bleibt unveraendert in `Plan` erhalten.


### Target tracking and protected braking 1.9.6

Enemy AI and the bridge autopilot now track the moving line of sight with both
angular position error and angular-rate error. This removes the persistent
several-degree lag that otherwise appears when both ships fly laterally around
one another. The bridge autopilot holds a 2 degree alignment tolerance and
still never fires its own lance.

Within 2,000 m, neither controller may turn the ship around to brake with the
main engine. If the planned velocity requires deceleration, it retains the nose
toward the contact and uses reverse thrust. A predicted collision instead uses
a tangential fly-by: yaw toward a chosen side first and apply main thrust only
after that lateral heading is reached. This prevents exposing the stern while
entering the enemy lance range.

The browser debug station at `http://127.0.0.1:47870/debug/` shows PASS/BLOCK
for every enemy firing gate: detected combat, ATTACK state, lance readiness,
1 km range, target ahead, aim error/tolerance, ray hit and fire command. It
also shows the equivalent player-lance hit diagnostics.

### Hyperraum-Navigation

Das Spiel beginnt im **Hyperraum** auf der Sternenkarte. Der erste gewaehlte
Encounter, einschliesslich Encounter 1, wird erst nach dem Setzen und
Bestaetigen eines Eintrittspunkts betreten. Ein bereiter **Warp Drive** startet
spaeter ebenfalls keinen direkten Encounterwechsel mehr: Ein Klick auf Warp
Drive entfernt das Spielerschiff aus dem aktuellen Realraum und oeffnet die
Sternenkarte. Erst dort wird mit **Jump** ein anderer Encounter als Ziel
bestimmt. Danach erscheint die freie taktische Karte ohne Bruecken-HUD,
Schiffsanzeige, Contacts oder Autopilot. Sie startet auf dem aktiven Gegner des
Ziel-Encounters zentriert; W/A/S/D verschiebt die Karte weiterhin und die
Leertaste schaltet in diesem Modus nicht um.

Ein Linksklick setzt einen roten Eintrittspunkt. Eine bernsteinfarbene Linie
zum Gegner zeigt dabei die geplante Eintrittsdistanz. Die bisherige Warp-Drive-
Box heisst dann **Jump** und ist erst nach einem gesetzten Punkt aktiv. Ihr
Klick setzt das Schiff an die gewaehlte Position, mit null Geschwindigkeit und
Standardrotation, repariert wie bisher die Subsysteme, fuellt den Schild auf
und blendet die normale Bruecke wieder ein. Erst dieses Reentry-Ereignis loest
den Warp-Sound aus. Der bekannte cyanfarbene Eintrittsring laeuft dabei fuer
drei Sekunden; die Distanzlinie verschwindet sofort.
