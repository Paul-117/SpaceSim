# Station Protocol v1

Der SpaceSim-Haupt-PC ist der alleinige Simulationsserver. Browser-Stationen senden
nur Bedienabsichten und erhalten kleine, stationsspezifische Snapshots.

## Transport

- HTTP: `http://<host>:47870/armarium/`
- WebSocket: `ws://<host>:47870/station`
- Protokollversion: `1`
- Textnachrichten: UTF-8 JSON mit camelCase-Feldern

## Client zu Server

### `hello`

Muss die erste WebSocket-Nachricht sein.

```json
{ "type": "hello", "station": "armarium", "protocolVersion": 1 }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `hello`. |
| `station` | string | Immer `armarium`. |
| `protocolVersion` | integer | Muss `1` sein. |

Bei falscher Stationskennung oder Version sendet der Server `error` und beendet die
Stationsverbindung.

### `fire_lance`

```json
{ "type": "fire_lance" }
```

Der Server puffert die Absicht als einmaligen Fire-Impuls. Er prueft die Lanzenladung
nicht im Netzwerkcode; die normale Simulation entscheidet im folgenden Tick, ob ein
Schuss moeglich ist.

## Server zu Client

### `welcome`

```json
{ "type": "welcome", "station": "armarium", "protocolVersion": 1 }
```

Bestetigt eine kompatible Stationsverbindung.

### `armarium_state`

```json
{
  "type": "armarium_state",
  "targetAvailable": true,
  "targetBearingDegrees": -12.4,
  "lanceCharge": 0.72,
  "lanceReady": false,
  "simulationTick": 1234,
  "connectionSequence": 77
}
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `targetAvailable` | boolean | Es gibt einen aktiven Gegner im aktuellen Encounter. |
| `targetBearingDegrees` | number | Horizontaler Bearing zur Bugwaffe. Negativ ist links, positiv rechts. |
| `lanceCharge` | number | Lanzenladung von 0 bis 1. |
| `lanceReady` | boolean | Lanze darf nach den Core-Regeln feuern. |
| `simulationTick` | integer | Tick des verwendeten Simulations-Snapshots. |
| `connectionSequence` | integer | Fortlaufende Server-Sendenummer. |

Der Server sendet maximal 25 Zustandsnachrichten pro Sekunde. Der vollständige
`WorldState`, Entfernungen, Gegner-Hull, AI-Daten und Weltkoordinaten werden nicht
uebertragen.

### `error`

```json
{ "type": "error", "code": "protocol_mismatch", "message": "Expected ARMARIUM protocol 1." }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `code` | string | Maschinenlesbarer Fehlercode. |
| `message` | string | Lesbare Fehlerbeschreibung. |
