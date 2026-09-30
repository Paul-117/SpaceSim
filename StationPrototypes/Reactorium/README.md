# Reactorium Station Prototype

Eigenstaendiger HTML/CSS/JavaScript-Prototyp fuer eine Reaktorstation. Er hat
keine Abhaengigkeit zum Godot-Projekt oder zum vorhandenen StationServer.

`index.html` direkt in einem Browser oeffnen. Der lokale Demo-Reaktor startet
mit 100 Fuel Units. Das Betriebslevel steuert die Output Power; hoehere Levels
verbrauchen ueberproportional mehr Treibstoff. Sobald der Schieberegler fokussiert
ist, laesst er sich in 5-Prozent-Schritten mit den Pfeiltasten bedienen.

Die Anzeige beschraenkt sich auf Fuel, Fuel Usage, Output Power und Current Draw.
Eine spaetere Server-Anbindung kann diese Demo-Werte durch Snapshots ersetzen.
