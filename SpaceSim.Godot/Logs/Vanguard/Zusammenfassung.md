# Vanguard – Duellauswertung

Ausgewertet wurden fünf 1VS1-Läufe vom 06.10.2026.

## Ergebnis

Alle fünf Duelle endeten mit einem Sieg des Spielers. Vanguard ist damit gegenüber Aegis in diesen Testläufen sicherer, aber noch nicht gefährlicher genug.

## Was funktioniert

- Kein Lauf löste eine Schiffskollision aus.
- Die geringsten Abstände betrugen 458 m, 689 m, 697 m, 508 m und 587 m. Der Sicherheitsabstand wird zuverlässig eingehalten.
- Das frühere State-Flattern wurde stark reduziert. REPOSITION hält nun mehrere Sekunden statt nur wenige Ticks.
- Wenn Vanguard ATTACK erreicht, ist die Feuergeometrie gut: In zwei Angriffsläufen lag der mittlere Aim Error bei 0,5° beziehungsweise 1,3°.
- Diese Angriffsläufe erzeugten Treffer: einmal drei von vier und einmal drei von fünf Gegner-Schüssen.

## Hauptproblem

In drei von fünf Duellen erreichte Vanguard nie ATTACK und feuerte keinen Schuss. Teilweise war die Lanze lange geladen und der Spieler lag innerhalb von 1 km, während die KI in REPOSITION verblieb.

Die Ursache: Vanguard richtet während REPOSITION auf ihren seitlichen Ausweichvektor. ATTACK verlangt jedoch bereits eine gute Zielausrichtung. Damit kann sich die KI in eine Schleife bringen: sicher ausweichen, aber nie wieder genug auf den Spieler zeigen, um ATTACK zu betreten.

## Nachfolger

Das Nachfolgemodell **Kestrel** übernimmt die Zielpräzision von Vanguard, aber:

1. nutzt REPOSITION nur für ein kurzes, konkretes Kollisionsmanöver;
2. kehrt danach zwingend zu APPROACH zurück und richtet wieder auf den Spieler aus;
3. hält 250 m als gewünschte Manöverdistanz;
4. weicht bei einem direkten Kollisionskurs erst innerhalb von 350 m aus;
5. akzeptiert bis 100 m als KI-Minimum, während die reale Kollision im Core erst unter 50 m auslöst.

Neue 1VS1-Logs werden unter `SpaceSim.Godot/Logs/Kestrel/` abgelegt.
