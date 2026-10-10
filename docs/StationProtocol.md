# Station Protocol v9

Der SpaceSim-Haupt-PC ist der alleinige Simulationsserver. Browser-Stationen senden
nur Bedienabsichten und erhalten kleine, stationsspezifische Snapshots.

## Transport

- HTTP: `http://<host>:47870/armarium/`, `http://<host>:47870/voltarium/`, `http://<host>:47870/sensorium/` oder `http://<host>:47870/debug/`
- WebSocket: `ws://<host>:47870/station`
- Protokollversion: `9`
- Textnachrichten: UTF-8 JSON mit camelCase-Feldern

## Client zu Server

### `hello`

Muss die erste WebSocket-Nachricht sein.

```json
{ "type": "hello", "station": "armarium", "protocolVersion": 9 }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `hello`. |
| `station` | string | `armarium`, `voltarium`, `sensorium` oder `debug`. |
| `protocolVersion` | integer | Muss `9` sein. |

Bei falscher Stationskennung oder Version sendet der Server `error` und beendet die
Stationsverbindung.

### `fire_lance`

```json
{ "type": "fire_lance" }
```

Im Armarium sendet die **Leertaste** genau einen solchen Befehl; gedrückt gehaltene
Tasten erzeugen durch die Repeat-Sperre kein Dauerfeuer.

Der Server puffert die Absicht als einmaligen Fire-Impuls. Er prueft die Lanzenladung
nicht im Netzwerkcode; die normale Simulation entscheidet im folgenden Tick, ob ein
Schuss moeglich ist. Eine erfolgreich angemeldete Armarium-Station darf den Command
immer senden.

### `turret`

```json
{ "type": "turret", "direction": "left", "active": true }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `turret`. |
| `direction` | string | `left` oder `right`. |
| `active` | boolean | `true` beim Druecken, `false` beim Loslassen. |

Dieser Befehl ist eine gehaltene Absicht und wird beim Verlust der Verbindung
geloescht. Er dreht ausschliesslich die Lanzenlafette, niemals das Schiff. Die
Lafette bewegt sich mit 10 Grad pro Sekunde und wird bei -5 beziehungsweise +5 Grad
relativ zur Schiffsnase begrenzt.

## Server zu Client

### `welcome`

```json
{ "type": "welcome", "station": "armarium", "protocolVersion": 8 }
```

Bestetigt eine kompatible Stationsverbindung.

### `armarium_state`

```json
{
  "type": "armarium_state",
  "targetAvailable": true,
  "targetBearingDegrees": -12.4,
  "targetDistanceMeters": 640.0,
  "lanceCharge": 0.72,
  "lanceReady": false,
  "lanceTurretAngleDegrees": -2.5,
  "targetHitSequence": 12,
  "lastTargetHitBearingDegrees": -12.4,
  "availablePower": 40.0,
  "maximumPower": 40.0,
  "lanceSystemCondition": 1.0,
  "simulationTick": 1234,
  "connectionSequence": 77
}
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `targetAvailable` | boolean | Es gibt einen aktiven Gegner im aktuellen Encounter. |
| `targetBearingDegrees` | number | Horizontaler Bearing zur Bugwaffe. Negativ ist links, positiv rechts. |
| `targetDistanceMeters` | number | Horizontale Entfernung zum aktiven Gegner in Metern fuer die taktische 1-km-Karte. |
| `lanceCharge` | number | Lanzenladung von 0 bis 1. |
| `lanceReady` | boolean | Lanze darf nach den Core-Regeln feuern. |
| `lanceTurretAngleDegrees` | number | Aktueller horizontaler Lafettenwinkel: negativ links, positiv rechts. |
| `targetHitSequence` | integer | Erhoeht sich nach jedem vom Core bestaetigten Spieler-Lanzentreffer. |
| `lastTargetHitBearingDegrees` | number | Bearing des zuletzt bestaetigten Treffers fuer den kurzen Zielhilfe-Effekt. |
| `availablePower` | number | Aktuelles, vom Voltarium zugewiesenes Armarium-Budget in PU. |
| `maximumPower` | number | Maximale Energieaufnahme der Armarium-Station in PU. |
| `lanceSystemCondition` | number | Zustand des Waffensystems von 0 bis 1. |
| `simulationTick` | integer | Tick des verwendeten Simulations-Snapshots. |
| `connectionSequence` | integer | Fortlaufende Server-Sendenummer. |

Der Server sendet maximal 25 Zustandsnachrichten pro Sekunde. Der vollständige
`WorldState`, Gegner-Hull, AI-Daten und Weltkoordinaten werden nicht uebertragen.
Die Entfernung ist auf den aktiven Gegner und die Darstellung der Armarium-Karte
beschraenkt.

### `error`

```json
{ "type": "error", "code": "protocol_mismatch", "message": "Expected a supported station using protocol 8." }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `code` | string | Maschinenlesbarer Fehlercode. |
| `message` | string | Lesbare Fehlerbeschreibung. |

## Voltarium (V1.9)

Das Voltarium verwendet denselben HTTP-/WebSocket-Server unter
`http://<host>:47870/voltarium/` und meldet sich mit `station: "voltarium"` an.

### Voltarium Client zu Server

```json
{ "type": "hello", "station": "voltarium", "protocolVersion": 8 }
```

```json
{ "type": "reactor_level", "levelPercent": 75 }
```

`levelPercent` wird auf 0 bis 100 begrenzt. Der Command ist nur eine Absicht; der
Simulations-Thread setzt die Reaktorleistung im naechsten Tick.

```json
{ "type": "power_allocation", "bridgePercent": 40, "shieldsPercent": 28, "armariumPercent": 32 }
```

Die drei Werte sind Prozent des aktuellen Reaktor-Outputs. Sie dürfen zusammen
höchstens 100 ergeben. Der Server puffert nur die Absicht; der Core prüft die
Werte und setzt die Zuweisung im nächsten Simulationstick.

### Voltarium Server zu Client

```json
{
  "type": "voltarium_state",
  "targetOperatingLevelPercent": 75,
  "operatingLevelPercent": 68.3,
  "outputPower": 85.38,
  "maximumOutputPower": 125,
  "currentDraw": 50,
  "fuel": 93.4,
  "fuelCapacity": 100,
  "fuelUsagePerMinute": 3.4,
  "bridgePercent": 40,
  "shieldsPercent": 28,
  "armariumPercent": 32,
  "bridgePower": 50,
  "shieldsPower": 35,
  "armariumPower": 40,
  "bridgeMaximumPower": 50,
  "shieldsMaximumPower": 35,
  "armariumMaximumPower": 40,
  "simulationTick": 1234
}
```

`targetOperatingLevelPercent` ist der vom Voltarium angeforderte Sollwert.
`operatingLevelPercent` ist die reale, vom Core gerampte Reaktorleistung. Der
Weg von 0 auf 100 Prozent dauert 60 Simulationssekunden. Bei 100 Prozent liefert
der Reaktor 125 PU.

Die `*Percent`-Felder beschreiben die zugewiesenen Anteile. Die `*Power`-Felder
sind die daraus resultierenden, pro Station begrenzten PU-Budgets. Die jeweiligen
`*MaximumPower`-Felder sind die festen Stationsgrenzen für die Slider-Anzeige.

`fuel` und `fuelCapacity` werden in Fuel Units übertragen. Der Core verbraucht
bei jeder aktiven Reaktorleistung Fuel und überträgt die aktuelle Rate mit
`fuelUsagePerMinute`. Bei Fuel 0 schaltet der Core den Reaktor aus; Output und
gelieferte Leistung sind dann 0. Der Webclient entscheidet dies nicht selbst.

## Enemy AI Debug

Die reine Entwicklungsstation liegt unter `http://<host>:47870/debug/` und meldet
sich mit `station: "debug"` an. Nach dem normalen `hello` empfängt sie keine
Commands und sendet selbst keine Bedienbefehle.

### `enemy_debug_state`

```json
{
  "type": "enemy_debug_state",
  "enemyAvailable": true,
  "enemyId": 2,
  "difficulty": "MEDIUM",
  "playerDetected": false,
  "aiState": "ACQUIRE",
  "distanceToPlayer": 2380.5,
  "closingSpeed": -12.0,
  "relativeSpeed": 151.0,
  "enemySpeed": 100.0,
  "hull": 3,
  "maximumHull": 3,
  "shield": 0.0,
  "maximumShield": 100.0,
  "lanceCharge": 0.0,
  "lanceReady": false,
  "reactorTargetOperatingLevelPercent": 50.0,
  "reactorOperatingLevelPercent": 50.0,
  "reactorAvailablePower": 62.5,
  "reactorCurrentDraw": 50.0,
  "propulsionRequested": 50.0,
  "weaponsRequested": 0.0,
  "shieldsRequested": 0.0,
  "propulsionDraw": 50.0,
  "weaponsDraw": 0.0,
  "shieldsDraw": 0.0,
  "simulationTick": 1234
}
```

Zusätzlich enthält der Snapshot `propulsionCondition`, `weaponsCondition` und
`shieldsCondition` (jeweils 0 bis 1) sowie die zugehörigen gelieferten Draw-Werte.
`enemyAvailable: false` signalisiert, dass der aktuelle Encounter keinen aktiven
Gegner besitzt. Der optionale Block `loadout` beschreibt die konkret montierten
Module. Er enthält Klasse, Quelle (`PROCEDURAL` oder `STANDARD`), bei
prozeduralen Gegnern Seed, Subklasse, Score, Peak-/Mindestleistung sowie die
ausgewählten Reaktor-, Waffen-, Schild-, Sensor- und Booster-Module. Der Server serialisiert keinen `WorldState`; die Nachricht ist
gezielt auf die Fehlersuche der Gegner-KI beschränkt.

## Sensorium (V2.0)

Das Sensorium ist unter `http://<host>:47870/sensorium/` erreichbar und meldet sich
mit `station: "sensorium"` an. Es sendet nur Sensorabsichten; der Server leitet
diese als gepufferte Befehle an die autoritative Simulation weiter.

### Client zu Server: `identify_contact`

```json
{
  "type": "identify_contact",
  "enemyId": 2
}
```

Diese Nachricht entsteht nur nach einer gueltigen Spektrometer-Bestaetigung:
passende Signaturbibliothek und ein Kontakt innerhalb der Ausrichtungstoleranz.
Der Core validiert die Enemy-ID im aktuellen Encounter und gibt den Kontakt danach
fuer Karte, Contacts und Autopilot der Bruecke frei.

### Client zu Server: `active_sonar`

```json
{ "type": "active_sonar" }
```

Jede Umschaltung des aktiven Sonars ist eine Emission. Sie gibt alle lokalen
Gegnerkontakte an die Bruecke weiter und alarmiert die Gegner.

### Server zu Client: `sensorium_state`

```json
{
  "type": "sensorium_state",
  "contacts": [
    {
      "enemyId": 2,
      "name": "Argus-02",
      "signatureCode": "ARGUS",
      "shipClass": "FRIGATE",
      "bearingDegrees": -12.4,
      "distanceMeters": 640.0,
      "reactorOutputFraction": 0.70,
      "shieldFraction": 0.30,
      "propulsionOutputFraction": 0.50,
      "weaponsOutputFraction": 0.80,
      "hull": 3,
      "maximumHull": 3
    }
  ],
  "simulationTick": 1234
}
```

Der Snapshot enthaelt nur Sensorium-Kontaktwerte: relative Peilung, Entfernung,
Signaturklasse und vier Systemleistungen. Er enthaelt keine Weltkoordinaten,
KI-Daten oder einen vollstaendigen `WorldState`. Das Frontend zeigt aktivem Sonar
alle Kontakte. In der passiven Peilung muss die Spektrometerachse innerhalb von
±10 Grad auf einen Kontakt zeigen; die passende Bibliothek und `Enter` bestaetigen
die Identifikation lokal in der Station.
