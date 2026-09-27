# Station Protocol v2

Der SpaceSim-Haupt-PC ist der alleinige Simulationsserver. Browser-Stationen senden
nur Bedienabsichten und erhalten kleine, stationsspezifische Snapshots.

## Transport

- HTTP: `http://<host>:47870/armarium/`
- WebSocket: `ws://<host>:47870/station`
- Protokollversion: `2`
- Textnachrichten: UTF-8 JSON mit camelCase-Feldern

## Client zu Server

### `hello`

Muss die erste WebSocket-Nachricht sein.

```json
{ "type": "hello", "station": "armarium", "protocolVersion": 2 }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `hello`. |
| `station` | string | Immer `armarium`. |
| `protocolVersion` | integer | Muss `2` sein. |

Bei falscher Stationskennung oder Version sendet der Server `error` und beendet die
Stationsverbindung.

### `fire_lance`

```json
{ "type": "fire_lance" }
```

Der Server puffert die Absicht als einmaligen Fire-Impuls. Er prueft die Lanzenladung
nicht im Netzwerkcode; die normale Simulation entscheidet im folgenden Tick, ob ein
Schuss moeglich ist. Der Command wird nur angenommen, wenn die Bruecke die
Armarium-Steuerung aktiviert hat.

### `yaw`

```json
{ "type": "yaw", "direction": "left", "active": true }
```

| Feld | Typ | Bedeutung |
| --- | --- | --- |
| `type` | string | Immer `yaw`. |
| `direction` | string | `left` oder `right`. |
| `active` | boolean | `true` beim Druecken, `false` beim Loslassen. |

Dieser Befehl wird nur bei aktivierter Armarium-Steuerung angenommen. Er ist eine
gehaltene Absicht und wird beim Verlust der Verbindung geloescht. Die Bruecke setzt
die tatsaechliche Yaw-Intensitaet auf 50 Prozent der normalen Seitentriebwerke.

## Server zu Client

### `welcome`

```json
{ "type": "welcome", "station": "armarium", "protocolVersion": 2 }
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
  "armariumControlsActive": true,
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
| `armariumControlsActive` | boolean | Die Bruecke hat dem Armarium Yaw- und Fire-Control uebergeben. |
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
