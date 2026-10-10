# SpaceSim — Implementationsplan: regelbasierter Schiffsgenerator (V1)

**Stand:** 09.10.2026
**Adressat:** Codex, gemeinsam mit dem Projektentwickler
**Ziel:** Aus einer Schiffsklasse wie `Corvette` automatisch **regelkonforme, vielfältige und taktisch plausible** Ausrüstungen aus den 51 vereinbarten Modulen erzeugen; dieselben Definitionen sollen in der autoritativen Kampfsimulation gelten.

**Verbindliche Begleitdatei:** `SpaceSim_ModuleCatalog_V1.md` mit allen 51 Modulen und exakten Parametern. Die schrittweise Ausführung steht in `Codex_StepByStep_Prompts.md`.

> **Vorgehen:** Zuerst Repository analysieren, Ist-Zustand nachweisen, dann **kleine testbare Schritte** umsetzen. Noch nicht den ganzen Plan in einem Durchgang programmieren. Bei Widersprüchen zu existierendem Code nicht stillschweigend raten; Befund und minimale Lösung dokumentieren.

---

## 0. Projektkontext und unveränderliche Architekturregeln

Die aktuelle SpaceSim-Architektur besteht aus:

- **`SpaceSim.Core`**: Autoritative C#-Simulation (Physik, Schiffe, Energie, Waffen, Schilde, Treffer, KI und Events). Alle Loadout-Regeln, Zufallsauswahl und Validierung gehören hierhin oder in eine Core-nahe datenorientierte Schicht.
- **`SpaceSim.Godot`**: Visualisierung, lokales HUD, Benutzerinteraktion, Menüs, Karte, Animationen und Sounds; keine verbindlichen eigenen Gameplay-Regeln.
- **`SpaceSim.Stations`**: Externe Browser-Stationen über LAN/WebSockets. Sie geben nur Bedienabsichten/Commands ab und erhalten **zweckgebundene Snapshots**, keinen kompletten WorldState.
- **Commands:** Spieler, Autopilot, KI und Netzwerkstationen verwenden die bestehenden `ShipCommands` wie `MainThrust`, `ReverseThrust`, `YawLeft`, `YawRight`, `FireLance`.
- **Newton-artige Physik:** Bewegung und Rotation nur über Schub-/Drehmomentintegration, nie über direkte Positions- oder Geschwindigkeitssetzung. Aktuelle Boostergrenzen dürfen Drift nicht künstlich wegkappen.
- **Subsystemzustand:** `Condition` (0 bis 1), Energiezuweisung, Schadenauswirkungen und Warp-Reparatur müssen weiterhin funktionieren.
- **Gefechtslabor und Duellmodus:** Existierende Logs, Kestrel-KI und Testmodi bleiben erhalten. Das isolierte 1v1-Duell hat derzeit bewusst vereinfachte Energie-/Sensorregeln; nicht unbemerkt verändern.

Bekannter Altzustand: Standardreaktor 125 PU, Bugwaffe 40 PU, Schilde 30 PU, Sensor 20 PU (künftiges Modul), alle drei Standard-Booster je 30 PU. Der vorhandene Hull-/HP-Code muss untersucht werden: In bisherigen Angaben erscheinen sowohl 30 HP als auch 3 Hull-Punkte. **Nicht ohne Codeprüfung vereinheitlichen.** Die Ship-Generator-Arbeit darf keine stillschweigende Änderung des Damage-Systems einführen.

### Nicht-Ziele der ersten Iteration

Keine Wirtschaft, kein Loot, keine Crew, keine neue Waffenart, keine umfangreiche neue UI, keine pauschale Neuimplementierung der KI, kein Umschreiben des Netzwerks, keine Veränderung sämtlicher bestehenden Encounter und keine automatische Anpassung der 51 Modulwerte.

---

## 1. Zielbild und Verantwortlichkeiten

Für den Aufruf sinngemäß `GenerateShip(classId: Corvette, seed: 12345)` soll der Core folgendes erzeugen:

1. Klasse `Corvette` mit ihren verbindlichen Grenzen.
2. Eine gewählte **Combat Doctrine** (z. B. `Patrol`, `Assault`, `Standoff`).
3. Genau **ein Modul je Slot**: Bow Weapon, Shield Generator, Reactor, Sensor Array, Main Booster, Reverse Booster, Side Booster.
4. Eine **technisch gültige Konfiguration**, einschließlich **neuer Energiepflichtregel**.
5. Eine **taktische Kohärenzbewertung**, damit nicht nur beliebige gültige Kombinationen entstehen.
6. Abgeleitete KI-Parameter (Wunschantfernung, Angriffsentfernung, Manövrierstil), ohne andere KI-Modelle zu zerstören.
7. Eine reproduzierbare Herkunft (`Seed`, Klasse, Doktrin, Modul-IDs, Regelversion, optional Score), darstellbar und testbar.

**Prinzip:** `ShipClass` bestimmt, was technisch zulässig ist. `CombatDoctrine` bestimmt, was taktisch bevorzugt wird. `LoadoutEvaluator` validiert und bewertet. `ShipLoadoutGenerator` führt die Auswahl aus. Das fertige `ShipLoadout` wird auf das vorhandene Schiffs-/Subsystemmodell angewendet.

### Empfohlene Kernobjekte (Namen beispielhaft)

- `ModuleDefinition` mit typisierten Untertypen oder klaren Slot-spezifischen Stats; unveränderliche Daten.
- `ModuleCatalog` mit 51 Einträgen, stabilem `ModuleId`, `DisplayName`, `SlotType`, `IsDefault`; keine Referenzen auf Godot oder Browser-JS.
- `ShipClassDefinition` mit Slotanzahl, numerischen Grenzen, zulässigen Doktrinen und Gewichten.
- `CombatDoctrineDefinition` mit Auswahlgewicht und **weichen Bewertungskriterien**.
- `ShipLoadout` als Zuordnung `SlotType → ModuleId` mit Generator-Metadaten.
- `PowerBudgetAssessment` mit exakten Teilbeträgen, `RequiredPU`, `AvailablePU`, `MarginPU`, `IsValid`.
- `LoadoutValidationResult` mit maschinenlesbaren Gründen (Code + Beschreibung), nicht nur bool.
- `LoadoutScore` mit nachvollziehbarer Aufschlüsselung pro Kriterium.
- `ShipLoadoutGenerator` mit injizierbarem Seed/Zufallsgenerator.
- `ShipTacticalProfile` als aus Waffe, Sensor, Mobilität und Doktrin abgeleitete KI-Zielwerte.

Vorhandene Klassen/Dateistruktur zuerst suchen und erweitern, **keine Duplikatarchitektur daneben bauen**.

---

## 2. Zentrales Regelwerk: Energie (NEU, verbindlich)

**Die neue Benutzervorgabe ersetzt die vorherige Energieidee ausdrücklich.** Bei der Validierung eines **generierten** Schiffs müssen alle drei Booster gleichzeitig zu 100 % ihres jeweiligen Nenn-PU-Werts berücksichtigt werden. Waffen, Schilde und Sensoren gehen jeweils zu 80 % ihres jeweiligen Nenn-PU-Werts ein.

```
RequiredPU = MainBoosterPU
           + ReverseBoosterPU
           + SideBoosterPU
           + 0.80 * WeaponPU
           + 0.80 * ShieldPU
           + 0.80 * SensorPU

ValidPowerBudget = RequiredPU <= ReactorMaxOutputPU
PowerMarginPU = ReactorMaxOutputPU - RequiredPU
```

**Keine Abkürzung durch `max(Main, Reverse, Side)` oder „nur gerade aktive Booster“.** Selbst wenn Main/Reverse physikalisch selten gleichzeitig feuern, zählt **bei der Installationsprüfung** die Summe aller drei Booster. Reaktor-Ramp-Up, Fuel Usage und `Condition` reduzieren **nicht** die Nennkapazität im Generator, sondern beeinflussen die reale Kampfleistung im bestehenden Runtime-System.

**Beispiel Standardmodule:** Booster `30+30+30=90 PU`; Waffe 40, Schild 30, Sensor 20 → `0,8×90=72 PU`; erforderlich **162 PU**, Standardreaktor **125 PU**, Defizit **37 PU**. Das bisherige Standardsetup ist damit **für die neue prozedurale Generierung ungültig**.

**Noch strenger:** Selbst mit den sparsamsten Katalogmodulen (Main 30 + Reverse 24 + Side 30 + 80 % × [Waffe 22 + Schild 24 + Sensor 8]) beträgt der Mindestbedarf **127,2 PU**. Unter der harten Regel kann ein Generator daher **kein** neues Schiff mit einem 125-PU-Reaktor erzeugen; 80-/90-/110-/115-PU-Modelle ebenfalls nicht. Das ist ein erwünschter, klar gemeldeter Validierungsbefund, **kein Anlass, Regeln heimlich aufzuweichen**.

### Umgang mit Legacy-Schiffen

- Bereits bestehende fest verdrahtete Test-/Spielerschiffe unverändert lauffähig lassen; sie sind **Legacy**, nicht „von Generator validiert“.
- Für Legacy optional Warnung/Diagnose `loadout fails generation rules: 162/125 PU` anzeigen oder loggen.
- Bereits gespeicherte/feste Schiffe **nicht automatisch** umbauen.
- Spätere Entscheidung des Entwicklers: Module/PU neu balancieren, Standardreaktor ändern, die neue Regel bewusst anders definieren oder eine Legacy-Ausnahme dauerhaft zulassen. **Diese V1 hält die harte Regel strikt.**

### Runtime-Energie und Stationsbudgets

Vor Umsetzung untersuchen, ob die drei Booster intern separat oder über ein gemeinsames `Propulsion`-Budget versorgt werden. Die **Designregel** zur Installation darf nicht mit der **dynamischen Laufzeit-Leistungszuweisung** verwechselt werden. Die realen Werte müssen nachvollziehbar sein:

- Booster können ihre Nennleistung nur bei ausreichender Energie-/Condition-Versorgung erzeugen.
- Bei weniger als 80 % Energie für Waffe/Schild/Sensor während des Hochlaufs oder nach Schaden bleibt das im Kampf möglich. Generator-Gültigkeit ist keine Garantie der aktuellen Energieverfügbarkeit.
- Keine doppelten PU-Abzüge durch parallele Booster- und Stationsabrechnung.
- Falls es bereits feste Grundlasten anderer Systeme (z. B. Brücke) gibt, deren Accounting dokumentieren. **Nicht stillschweigend aus der hier definierten Formel herausschneiden oder hineinrechnen.**
- Rechenwerte intern ausreichend genau halten; bei `174,8 ≤ 175` darf Rundung kein falsches Ablehnen auslösen. Anzeige darf runden, Entscheidungslogik nicht.

---

## 3. Modul-Katalog (51 Definitionen)

Den vollständigen Katalog aus `SpaceSim_ModuleCatalog_V1.md` übernehmen:

| Slot | Anzahl |
|---|---:|
| Bow Weapon | 10 |
| Shield Generator | 10 |
| Reactor | 10 |
| Sensor Array | 10 |
| Main Booster | 5 |
| Reverse Booster | 3 |
| Side Booster | 3 |
| **Gesamt** | **51** |

**Speicherung:** Einen einzigen autoritativen Datenursprung nutzen. Bevorzugt typsichere Core-Definitionen oder validierte, versionierte JSON-Dateien, **abhängig von bestehender Projektkonvention**. Godot/Stationen sollen daraus Daten bekommen statt einen zweiten eigenen Zahlenkatalog zu pflegen.

**Stabile IDs und Anzeigenamen strikt trennen** (Umbenennung darf gespeicherte Loadouts nicht brechen). Daten laden und beim Start validieren: Anzahl, Eindeutigkeit, Slottypen, Grenzen, Defaults, positive Werte, keine ausgeschlossenen Namen in **Modulnamen**. Verbotene Namen sind `Aegis`, `Kestrel`, `Vanguard`; Namen vorhandener KI-Modelle wie `Kestrel` dürfen weiter existieren.

**DPS und Regenerationsrate** aus den Kanonwerten berechnen, nicht redundant als Gameplayparameter pflegen. Einheiten explizit halten (m, m/s, kN, Sekunden, PU, Grad/s), damit im Core keine impliziten Umrechnungsfehler entstehen.

### Anwendung auf bestehende Systeme

- **Waffe:** Schaden, Reichweite, Ladezeit, ± Schwenkfenster, Schwenkrate, PU. Bestehender Raycast, Zielwinkel, Waffenschäden und `FireLance` erhalten.
- **Schilde:** Max HP, Ladezeit, Reboot, PU. Bei HP > 0 regeneriert das System; nach ≤ 0 erfolgt Reboot, dann Aufladung; genaue bisherige Semantik überprüfen. Hull-/Subsystemregeln erhalten.
- **Reaktor:** Max Output, U/min Volllast, Ramp-Up. Reaktorträgheit, Treibstoff und Voltarium-Zuweisung erhalten.
- **Sensoren:** Min-/Max-Passivreichweite, PU. Formel und Identifikationsregeln siehe Abschnitt 8.
- **Booster:** Kräfte, Ramp-Up nur Main, Geschwindigkeits-/Rotationslimits und PU. Kommandobasierte Newton-Physik beibehalten.

---

## 4. Schiffsklassen — V1 nur Corvette als generierbarer Pfad

Erste implementierte `ShipClassDefinition` ist **Corvette** mit folgenden **vorläufigen, bereits konzeptionell verwendeten Grenzwerten**:

| Regel | Corvette V1 |
|---|---|
| Bow Weapon / Shield / Reactor / Sensor / Main / Reverse / Side | jeweils genau 1 Slot |
| Zulässiger Reaktor-Max-Output | 110–175 PU |
| Waffen-Einzelschaden | ≤ 36 |
| Waffenreichweite | ≤ 1.650 m |
| Schild-HP | ≤ 40 |
| Main-Booster-Schub | ≤ 150 kN |
| Kampfdoktrinen | Patrol / Assault / Standoff |

Diese Grenzen sind Konfigurationsdaten, nicht verstreute `if (class == Corvette)`-Ausnahmen. Klassen `Interceptor`, `Frigate` oder `Transporter` später in gleicher Struktur ergänzen, **ohne jetzt unbestätigte numerische Grenzen zu erfinden**. Die Nenn-Reaktorgrenzen schließen u. a. den LEVIATHAN mit 250 PU aus. Die Energiepflichtregel ist davon unabhängig und kann zusätzlich viele formal kompatible Konfigurationen verwerfen.

Klassenspezifische Hull-Werte, Masse, Trägheitsmoment, Länge, Kollisionsradius etc. bleiben V1 vorerst so, wie sie im bestehenden Schiffstyp definiert sind. Eine neue Modulvariante darf **nicht** automatisch Masse oder Geometrie ändern, wenn keine belastbaren Werte vorliegen.

---

## 5. Combat Doctrines — Gewichtung statt starrer Presets

**Corvette-Doctrine-Auswahl, vorläufige Gewichte:**

- `Patrol`: **40 %**, normale Waffenreichweite, ausgewogene Mobilität und Sensorabdeckung.
- `Assault`: **35 %**, tendenziell kurze Reichweite, gutes Beschleunigen/Abbremsen, guter Waffenwinkel oder Drehfähigkeit.
- `Standoff`: **25 %**, längere Waffenreichweite, ausreichende Sensorabdeckung, Distanzkontrolle.

Diese Werte sind **Zielgewichte unter realistisch generierbaren Doktrinen**. Wenn eine Doktrin bei gegebener Klasse und Energiepflichtregel überhaupt keine gültigen Konfigurationen besitzt, dokumentierte Warnung und Fallback-Strategie; nie Endlosschleife. Bei vorhandenen gültigen Varianten Wahrscheinlichkeiten mit Seed reproduzierbar sampeln.

**Soft Scoring, nicht alles hart verbieten:**

- `WeaponSensorFit`: Sensorentdeckung vor oder mindestens rechtzeitig zur gewünschten Schussdistanz. Sensorreichweite gegen Zielreaktor-Signatur auswerten, nicht blind immer `SensorMax`.
- `WeaponMobilityFit`: kurze Waffenreichweite profitiert von guter Annäherung/Bremse; enges Schwenkfenster profitiert von guter Rotation.
- `DoctrineFit`: Reichweite/DPS/Schilde/Mobilität passen zur Doktrin.
- `EnergyMargin`: kleine Reserve ist gültig, größere Reserve mildert spätere Lastwechsel.
- `Survivability`: Schildkapazität, Reboot und Regeneration plausibel zur Doktrin.
- `Diversity`: valide Varianten sollen nicht permanent auf einem einzigen Meta-Loadout enden.

**V1-Scoring-Vorschlag:** normierte Kriterien 0–1; z. B. Doktrin 35 %, Waffen↔Mobilität 25 %, Sensor↔Waffe 20 %, Energie-Reserve 10 %, Defensive 10 %. Endscore 0–100. Die Gewichtung bleibt konfigurierbar. Score entscheidet **Auswahlwahrscheinlichkeit**, nicht allein deterministisch das Maximum; schwächere, aber gültige NPCs bleiben möglich. Fehlende Evidenz/unklare Messwerte dokumentieren statt pseudo-exakte Balance zu behaupten.

**Keine numerisch willkürlichen Sonderregeln mit Modulnamen** wie `if weapon == HELLSTORM then require TALON`. Stattdessen Eigenschaften vergleichen. Modulnamen und IDs nur bei expliziten Story-/Boss-Presets verwenden.

---

## 6. Generierungsalgorithmus

**Empfohlener Ablauf:**

1. Beim Start Katalog, Klassen und Doktrinen laden und validieren.
2. `ShipClassId`, `Seed` und optional `DoctrineOverride` entgegennehmen.
3. Doctrine nach Gewicht auswählen, sofern mindestens ein gültiger Build existiert.
4. Zuerst Waffe nach Doctrine-Passung wählen (definiert bevorzugte Distanz).
5. Restliche Slots aus **kompatiblen Kandidaten** zusammensetzen, besonders Reaktor früh genug berücksichtigen.
6. Bei jeder Kandidatenkombination harte Klassen-, Slot- und Energieprüfung ausführen; Invalid-Grund protokollieren.
7. Aus gültigen Builds einen gewichteten Kandidatenpool anhand des Soft Scores bilden.
8. Über einen deterministischen Seed einen Kandidaten wählen.
9. Finales Loadout erneut vollständig validieren und in das vorhandene Ship-/Subsystemmodell überführen.
10. Modulliste, Klasse, Doktrin, Seed, Regelversion, Budget und Score in Entwicklerdiagnostik speichern.

**Performance-/Fehlerstrategie:** V1 darf begrenztes Backtracking/Pre-Filtering nutzen. Nicht naiv unbegrenzt zufällig ziehen, bis es zufällig klappt. Eine komplette Rohliste hätte 450.000 Kombinationen (10×10×10×10×5×3×3), nach Klassenfiltern weniger; ein klassenspezifischer, gecachter gültiger Pool ist daher bei diesem kleinen Katalog möglich. Später für größere Kataloge auf constraints-first/backtracking umstellen. In jedem Fall maximale Versuche, definierte Laufzeitgrenze, deterministisches Verhalten und eindeutige Fehlermeldung `NoValidLoadout` statt eines illegalen stillen Fallbacks.

**Determinismus:** stabile Reihenfolge bei Katalogiterationen, expliziter PRNG/Seed und keine versteckten systemweiten Zufallsabfragen. Identischer Seed + identische Katalog-/Regelversion => identisches Ergebnis. Für reproduzierbare Gefechtstests Seed und Modul-IDs in bestehende Logs aufnehmen.

### Drei konkrete **gültige** Energie-Testfälle

Die folgenden Rechnungen sind **keine verpflichtenden fest verdrahteten Presets**, sondern Testvektoren für Validierung und Generator.

**A. Niedrigere Reaktorleistung / gültig:** `OVERDRIVE OD-140` (140 PU), `WRAITH` (22), `ETHEREAL WARD` (24), `GHOST EYE` (8), `ATLAS M-100` (30), `BACKDRAFT R-20` (24), `VECTOR S-1` (30). Bedarf `30+24+30 + 0,8×(22+24+8) = 127,2 PU` → **gültig**, Reserve **12,8 PU**.

**B. Mittlere Reaktorleistung / gültig:** `HORIZON A-150` (150), `WRAITH` (22), `GUARDIAN S-20` (30), `ARGUS S-200` (20), `ATLAS M-100` (30), `BACKDRAFT R-20` (24), `VECTOR S-1` (30). Bedarf `84 + 0,8×72 = 141,6 PU` → **gültig**, Reserve **8,4 PU**.

**C. Assault-Corvette / gültig:** `INFERNO X-175` (175), `RAVAGER` (42), `ETHEREAL WARD` (24), `GHOST EYE` (8), `SKYFANG M-85` (40), `GRAVEBREAK R-60` (45), `VECTOR S-1` (30). Bedarf `115 + 0,8×74 = 174,2 PU` → **gültig**, Reserve **0,8 PU**.

**D. Legacy Standard / ungültig für Generation:** `CORE-X125`, `PEREGRINE`, `GUARDIAN`, `ARGUS`, `ATLAS`, `ANCHOR`, `VECTOR` → Bedarf `162 PU > 125 PU` → **ungültig**.

**E. Grenzwerttests:** `RequiredPU == MaxOutput` muss bestehen, `RequiredPU > MaxOutput` muss scheitern; Bruchteile dürfen nicht vor dem Vergleich gerundet werden.

---

## 7. Simulationsintegration: Module müssen echte Werte beeinflussen

Ein reiner Generator, der nette Namen auswählt, ist nicht fertig. Module müssen **physisch wirksam** sein.

**Beim Schiffbau/Spawn:** Ein Loadout wird einer bestehenden Ship-Instanz als Modulausstattung zugeordnet. Das Schiffsmodell besitzt referenzierbare Modul-IDs und dynamische Zustände (`Condition`, aktuelle Ladung, Reaktoroutput, Treibstoff, Booster-Ramp, Turmwinkel, Schild-Reboot). Definitionen bleiben immutable; Runtime-Zustände sind pro Schiff separat. Beim Spawn zulässige Initialzustände aus bestehenden Regeln übernehmen, nicht willkürlich stets auf 100 % setzen.

**Waffen:** Raycast, Schussbedingung, Load/Ready, Winkel-Nachführung, Schaden und Reichweite lesen Parameter des installierten Waffenmoduls. KI und Spieler verwenden dieselben Regeln. Aufladezustand und Leistungsreduktion bleiben erhalten.

**Schilde:** Kapazität, Regenerationsdauer, Reboot aus aktuellem Schildmodul. Ausschalten bei 0 HP und Reset korrekt modellieren. Während Reboot kein sofortiges Regenerieren. Bestehenden Hull-/Sekundärschaden nicht neu erfinden.

**Reaktoren:** Max Output, Verbrauch und Ramp-Up aus installiertem Reaktormodul. Hochlauf und begrenzte Leistungszuweisung wie zuvor, mit bestehenden Voltarium-/Condition-/Fuel-Mechanismen.

**Antrieb:** Main/Reverse/Side aus Moduldefinition. Main-Ramp-Up, Schubkräfte, maximale Geschwindigkeit/Winkelgeschwindigkeit unverfälscht über das Physiksystem. Besonders Masse, Hebelarm und Trägheitsmoment berücksichtigen. Geschwindigkeiten oberhalb sinkender Limits nicht künstlich abschneiden.

**Subsystemschaden und Warp:** Schäden an Weapons/Armarium, Shields, Propulsion, Voltarium usw. wirken weiterhin über vorhandene Condition- und Reparaturregeln. Defekte Module ändern nicht automatisch ihre installierten Stammdaten oder Generator-Gültigkeit. Warp-Reparatur weiterhin nach Bestandsverhalten.

**Energie-Hinweis:** Runtime muss die zugewiesene Leistung auf die ausgerüsteten Modul-Nennwerte beziehen; bei 80 % Waffenleistung gelten dieselben Regeln wie bisher für unterversorgte Waffen, soweit vorhanden. Neue Verbraucher Sensor/Side/Reverse sauber dem existierenden Energiebudget zuordnen und vorhandene Stationen nicht doppelt belasten.

---

## 8. Sensorik und Reaktorsignatur

**Passive Detektion** durch ein Sensor-Array soll sich auf die **aktuelle Reaktorleistung des Zielschiffs** beziehen, nicht blind auf dessen verbautes Reaktor-Maximum. Das erzeugt die beabsichtigte Signaturtaktik: wenig Output = schwerer auf große Distanz zu finden.

V1-Konzeptformel:

```
PassiveRangeM = Clamp(
    SensorMinRangeM + 10 * Max(0, TargetActualReactorOutputPU),
    SensorMinRangeM,
    SensorMaxRangeM)
```

Globaler erlaubter Sensorbereich: **500 bis 3.000 m**; alle Katalog-Sensormodule liegen darin. Referenz ARGUS: bei 50 PU Zielausstoß 1.500 m, bei 100 PU 2.000 m. Angegebene Werte sind Reichweiten **des beobachtenden Sensors**, skaliert durch **das beobachtete Schiff**.

Abgrenzen:

- **Detektion vs. Identifikation:** Ein georteter Kontakt ist nicht automatisch vollständig identifiziert. Vorhandene Sensorium-Bearing-/Sonar-/Kontakte-Regeln erhalten, keine unfreiwilligen Metadatenlecks in die Brücke.
- **Aktives Sonar:** Bestehende Erfassung bis 10 km und die Warnung an Gegner bewahren; nicht durch passiven Radius ersetzen.
- **KI-Gegner:** In normalen Encountern darf die KI nicht „magisch“ jedes Ziel kennen, wenn ein Modul-/Erkennungsmodell eingeführt wird. Unklaren Ist-Zustand vor Änderung prüfen. **Isolierte 1v1-Duelle** dürfen ihre explizit festgelegte Allwissenheits-Ausnahme behalten.
- **Leistung/Condition:** Ausfall und reduzierte Energie des Sensor-Moduls müssen konsistent mit den bestehenden Regeln behandelt werden; über eine Reichweitendrosselung oder einen klaren deaktivierten Zustand bewusst entscheiden und testen, statt Nebenwirkungen zu erfinden.
- **Sichtbarkeit bei Schwellenwerten:** Verhalten bei exakt Grenzdistanz, Zielwechsel, Zielverlust, Reboot und Leistungsabfall testen. Bei häufigem Flackern ggf. ein dokumentiertes, kleines Hysterese-/Refresh-Verfahren einführen, ohne Kontaktinformationen zu fälschen.

---

## 9. KI-Integration: Kestrel per Loadout parametrisieren

Die bestehende Kestrel-KI hat u. a. `ACQUIRE → APPROACH → ATTACK → REPOSITION → DESTROYED` und bevorzugt derzeit 600 m, bei Angriff 250–900 m. Diese FSM in V1 **nicht neu schreiben**.

**Neu:** pro generiertem Schiff ein `ShipTacticalProfile`, das aus Doctrine, Waffe und Schiffbeweglichkeit berechnet wird. Die KI liest diese Parameter statt immer dieselben 600 m zu benutzen.

**Erste Heuristik, später anhand Gefechtslabor kalibrieren:**

- `Assault`: bevorzugt grob 55–75 % der realen Waffenreichweite, aber Kollision und relativen Anflug berücksichtigen.
- `Patrol`: grob 65–85 % der Waffenreichweite.
- `Standoff`: grob 70–90 % der Waffenreichweite, sofern Ortung und Kampfgeometrie es zulassen.
- Angriff nur innerhalb tatsächlicher Waffenreichweite; Trefferbedingungen einschließlich Turmwinkel bleiben im Core.
- Distanzminima und Ausweichverhalten respektieren Kollision bei weniger als 50 m und den bisherigen kritischen Bereich unter ca. 350 m für KI-Kollisionskorrekturen. Keine blind erzwungene Annäherung auf 100 m wegen kurzer Waffenreichweite.
- Bei Kontaktverlust: vorhandenes `ACQUIRE` und Last-Known-Position-Verhalten nutzen; keinen freien Informationszugriff hinzufügen.

Wenn bestimmte Profile in der jetzigen FSM nicht realisierbar sind, zunächst als dokumentierte Einschränkung und Testfall aufnehmen, nicht die ganze KI in einem Schritt umbauen.

---

## 10. Godot, Stations und Benutzeroberflächen

**V1 nur notwendige Sichtbarkeit:** Ein Debug-/Entwicklerpanel oder Konsolen-/Logausgabe reicht. Anzeigen:

- `ShipClass`, `Doctrine`, `Seed`, Spawn-ID.
- Sieben Modulnamen (und bei Bedarf IDs).
- Reaktor-Max PU, `RequiredPU`, Reserve in PU, `Valid/Invalid` mit Gründen.
- Waffe: Schaden, Reichweite, Ladezeit, Schwenkfenster.
- Schild: HP, Regeneration, Reboot.
- Antrieb: Main/Reverse/Side-Leistung und Limits.
- Sensor: passiver Radius anhand *einer aktuellen* Zielreaktorsignatur.
- KI: Soll-Kampfdistanz und aktueller State.

Bestehende Bridge-/Armarium-/Reactorium-/Sensorium-Anzeigen nicht brechen. Webstationen bekommen benötigte neue **Snapshotfelder** nur, soweit dort tatsächlich angezeigt werden soll. Alle Befehle laufen weiter zentral über den Haupt-PC; keine Browser-Seite darf einen gültigen Schuss, Modulwechsel oder Ladezustand selbst bestimmen.

Wenn möglich eine Entwicklerfunktion für `Generate Corvette` mit optionalem Seed, **aber erst nachdem Core-Tests funktionieren**. Für V1 kein komplexer Spieler-Ausrüstungseditor nötig.

---

## 11. Gefechtslabor, Testbarkeit und Balance-Telemetrie

Der vorhandene KI-gegen-KI-Gefechtslabor-Modus und Logs sind besonders wertvoll:

- Optionale Parameter: `ShipClass`, `Doctrine`, `Seed` für jede Seite, oder weiterhin alte Direktparameter für Regressionstests.
- Pro Kampf Spawn-Seed, Modul-IDs, Doktrin, Energie-Budget und Taktikprofil in `session.json`/Log aufnehmen, ohne Altfelder zu entfernen.
- Bisherige 1–50-Partien-Funktion und 60-Hz-Logs erhalten.
- Regressionstests der bisherigen Kestrel-/Duell-Experimente nicht überschreiben.

**Messwerte für spätere Balance:** Winrate je Doctrine/Loadout-Paar, Distanz beim ersten Treffer, durchschnittliche Kampfdistanz, tatsächliche DPS/Trefferquote, Energieunterdeckung während des Gefechts, Schildbrüche, Reboot-Anzahl, Kollisionsereignisse, Zeit bis Sieg oder Timeout, Zufalls-Seed.

**Keine sofortige automatische Nachbalance:** Erst Ergebnisse sammeln; Katalogdaten nur in bewusst eigener Änderung anpassen.

---

## 12. Testplan / Definition of Done

### A. Daten und Kompatibilität

1. Katalog hat exakt **51** Module mit genau den V1-Daten; Namen und persistente IDs eindeutig.
2. Kein **Modulname** enthält `Aegis`, `Kestrel` oder `Vanguard`; die KI `Kestrel` bleibt unberührt.
3. Klassenfilter für Corvette funktionieren an den Grenzwerten einschließlich Gleichheit.
4. Standard-/Legacy-Loadouts bleiben weiterhin start- und spielbar; neue Validierung beanstandet sie erwartungsgemäß.
5. Bestehende automatisierte Tests und Builds laufen unverändert weiter.

### B. Energie und Determinismus

6. Exakter Energiesummen-Test `30+30+30+0,8*(40+30+20)=162` → gegen 125 ungültig.
7. Minimalenergie-Test `84+0,8*(22+24+8)=127,2` → gegen 125 ungültig und gegen 140 gültig.
8. Beispiel-Loadouts mit 140, 150 und 175 PU aus Abschnitt 6 bestehen.
9. Gleichheit `RequiredPU == ReactorMaxOutput` gültig, `>` ungültig; keine Voraus-Rundung.
10. Für eine feste Regel-/Katalogversion liefert derselbe Seed dasselbe Loadout.
11. Bei nicht erfüllbaren Filtern kommt `NoValidLoadout` mit Gründen; keine Endlosschleife oder illegale Notlösung.

### C. Gameplay

12. Ausgewählte Waffen-/Schild-/Reaktor-/Boosterwerte beeinflussen **nachweisbar** reale Simulationsergebnisse.
13. Boosterkräfte wirken über Thruster/Physik, nicht direkt über Velocity/Rotation.
14. Konditionsschäden und Energieunterversorgung beeinflussen die Runtime weiter korrekt.
15. Schildbruch startet Reboot statt sofortiger Regeneration.
16. Passive Ortung: ARGUS bei Zieloutput 50 PU → 1.500 m, bei 100 PU → 2.000 m; Min/Max sauber clampen.
17. Aktives Sonar, Kontaktidentifikation, Warp, Armarium und 1v1-Sonderregeln regressionsgetestet.
18. KI benutzt Waffendistanz/Doctrine, ohne die bestehenden States oder Kommandos zu umgehen.

### D. Generierungsqualität und Integration

19. Mindestens 1.000 feste Seeds pro generierbarer Klasse ergeben ausschließlich regelkonforme Schiffe oder explizite, erklärte Fehlschläge.
20. Mehrere unterschiedliche gültige Loadouts und alle generierbaren Doktrinen sind in den Stichproben vertreten. Gewichtsverteilung als Statistik berichten, nicht exakte Verteilung je 1.000 erzwingen.
21. Eine neue Corvette erscheint mit nachvollziehbarer Modul-, Seed- und PU-Auflistung im Debug-Output.
22. Keine unautorisierten Änderungen in Godot/Stationen; Nachrichten bleiben zweckgebunden.
23. Gefechtslabor/Log-Viewer können bestehende Logs weiter lesen; neue Felder sind rückwärtskompatibel.

**V1 ist fertig**, wenn eine Corvette aus dem Core deterministisch erzeugt, vollständig hart validiert, physikalisch korrekt ausgerüstet, von Kestrel mit passenden Distanzen geführt und im Gefechtslabor reproduzierbar getestet werden kann, **ohne** die alten Spielmodi zu beschädigen.

---

## 13. Empfohlene Reihenfolge mit abnahmefähigen Meilensteinen

| Meilenstein | Änderung | Abnahme |
|---|---|---|
| M0 | Nur Repository-/Architektur-Audit | Ist-Zustand, Datenflüsse, Testbefehle, Risiken dokumentiert |
| M1 | Modul-Katalog und Validator | 51 geprüfte Definitionsdatensätze; keine Gameplayänderung |
| M2 | `ShipClass` und `CombatDoctrine` | Corvette-Grenzen und Doktrin-Gewichte getestet |
| M3 | Harte Loadout-/Energie-Validierung | Exakte 100/80-Regel, Testvektoren, Fehlerdiagnostik |
| M4 | Deterministischer Loadout-Generator | Verschiedene, immer gültige Kombinationen und Seed-Tests |
| M5 | Modulwerte an Runtime anbinden | Waffen, Schild, Reaktor, Booster und Conditions wirksam |
| M6 | Passive Sensorik / Signaturen | Beide Seiten korrekt detektierbar, Sonar/1v1 erhalten |
| M7 | Taktikprofil / Kestrel-Parameter | Plausible Kampfdistanzen ohne FSM-Rewrite |
| M8 | Corvette-Spawn / UI-Debug | Zufallscorvette im regulären Encounter mit nachvollziehbarem Loadout |
| M9 | Gefechtslabor / Auswertung | Multi-Seed-Testläufe, Regressionen, Telemetrie |
| M10 | Stabilisierung / Dokumentation | Tests grün, alte Modi intakt, V1 vollständig dokumentiert |

**Nach jedem Meilenstein:** Codex liefert geänderte Dateien, Designentscheidungen, Testkommando + tatsächliches Ergebnis, bekannte Restprobleme und einen kurzen Prüfauftrag an den Entwickler. Kein unbestätigtes Massen-Refactoring.

---

## 14. Offene Punkte, die beim Repository-Audit sichtbar gemacht werden müssen

Diese Punkte sind **nicht** als neue Gameplay-Annahmen auszugeben:

1. Ist die aktuelle Hülle als **30 HP** oder **3 Hull-Punkte** implementiert (und wie hängen Schildschaden, Durchschlag und Subsystemschaden zusammen)?
2. Gibt es bereits je Booster eigene PU-Bedarfe oder ein gemeinsames `Propulsion`-Budget? Wie lässt sich die neue volle Installationsprüfung abbilden, ohne Laufzeitenergie doppelt zu zählen?
3. Sind Sensoren heute überhaupt eigener Energieverbraucher, oder ist das Modul neu anzubinden?
4. Existiert schon eine passive Detektionsberechnung oder nur Sensorium-Bearing und aktives Sonar?
5. Welche Encounter werden aus festen Klassen/Blueprints erzeugt, und wie wird die aktuelle Kestrel-AI instanziiert?
6. Welches Konfigurationsformat nutzen Core/Godot/Stations bereits (C# Records, JSON, Config-Dateien)?
7. Gibt es eine Test-Harness-Projektstruktur und welche Tests lassen sich in dieser Umgebung real ausführen?
8. Welcher Gameplayzustand wird beim Warp erhalten/repariert, und welche Bau-/Spawn-Initialzustände sind dafür korrekt?
9. Wie werden stabile Ship- und Modul-IDs für Log/Replay/Saves (zukünftig) verwaltet?

**Arbeitsregel für Codex:** Diese Lücken bei M0 präzise dokumentieren, aber nicht durch spekulative Umbauten „lösen“. Erst die kleinstmögliche, vom Projektbestand getragene Implementierung wählen.
