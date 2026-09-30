# Station Protocol v6

Der SpaceSim-Haupt-PC ist der alleinige Simulationsserver. Browser-Stationen senden
nur Bedienabsichten und erhalten kleine, stationsspezifische Snapshots.

## Transport

- HTTP: `http://<host>:47870/armarium/`
- WebSocket: `ws://<host>:47870/station`
- Protokollversion: `6`
- Textnachrichten: UTF-8 JSON mit camelCase-Feldern

## Client zu Server

### `hello`

Muss die erste WebSocket-Nachricht sein.

```json
{ "type": "hello", "station": "armarium", "protocolVersion": 6 }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `hello`. |
| `station` | string | Immer `armarium`. |
| `protocolVersion` | integer | Muss `6` sein. |

Bei falscher Stationskennung oder Version sendet der Server `error` und beendet die
Stationsverbindung.

### `fire_lance`

```json
{ "type": "fire_lance" }
```

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
{ "type": "welcome", "station": "armarium", "protocolVersion": 6 }
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
| `simulationTick` | integer | Tick des verwendeten Simulations-Snapshots. |
| `connectionSequence` | integer | Fortlaufende Server-Sendenummer. |

Der Server sendet maximal 25 Zustandsnachrichten pro Sekunde. Der vollständige
`WorldState`, Gegner-Hull, AI-Daten und Weltkoordinaten werden nicht uebertragen.
Die Entfernung ist auf den aktiven Gegner und die Darstellung der Armarium-Karte
beschraenkt.

### `error`

```json
{ "type": "error", "code": "protocol_mismatch", "message": "Expected ARMARIUM protocol 6." }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `code` | string | Maschinenlesbarer Fehlercode. |
| `message` | string | Lesbare Fehlerbeschreibung. |
