# Gefechts-Simulation

Start über `Tools/Gefechts-Simulation_Windows.cmd`.

Der Lauf startet standardmäßig fünf deterministische 1VS1-Gefechte: Nomad und der Gegner nutzen jeweils dieselbe Kestrel-KI, dieselben Schiffsparameter sowie volle Energie. Die Simulation wartet nicht in Echtzeit; sie rechnet alle 60-Hz-Simulationstakte so schnell wie die CPU kann.

Die Ergebnisse liegen je Lauf unter `SpaceSim.Godot/Logs/Gefechts Simulationen/<Zeitstempel>/`:

- `gefecht_01.txt` bis `gefecht_05.txt`: ein Snapshot je Simulationstakt inklusive Trajektorien, Commands, Lanzenschüssen und Treffern.
- `Zusammenfassung.txt`: Seeds, Sieger, Dauer, minimale Distanz und Gesamtauswertung.

Die Einzeldateien können direkt im vorhandenen `Tools/Duell-Log-Viewer_Windows.cmd` geladen werden.
