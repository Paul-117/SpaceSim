# Lokale Sprachausgabe für Bridge-Notifications

SpaceSim spricht Bridge-Notifications lokal auf dem Haupt-PC mit **Piper**. Die
Sprachausgabe erhält nur den bereits erzeugten Notification-Text. Sie kann weder
Commands noch den Simulationszustand verändern und sendet keine Audiodaten über
das Netzwerk.

## Installation

Einmalig im Repository-Stamm ausführen:

```powershell
./Tools/Voice/Install-Piper_AmyMedium_Windows.ps1
```

Das Skript lädt den offiziellen Piper-Windows-Runner und das Modell
`en_US-amy-medium` nach `SpaceSim.Godot/Voice/PiperRuntime` beziehungsweise
`SpaceSim.Godot/Voice/PiperModels`. Diese großen lokalen Dateien bleiben aus
Git ausgeschlossen.

## Verhalten

- Jede neue Bridge-Notification wird zuerst dargestellt und danach lokal gesprochen.
- Gleichlautende Meldungen innerhalb von 0,75 Sekunden werden zusammengefasst.
- Höchstens sechs wartende Meldungen werden gespeichert; bei Überlauf entfällt die älteste.
- Szenen-/Encounterwechsel stoppen Wiedergabe und verwerfen wartende alte Meldungen.
- Ist Piper nicht installiert, bleibt das Spiel funktionsfähig und protokolliert einmalig,
  wie die Installation gestartet wird.
