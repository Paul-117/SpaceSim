# Aegis – Duellauswertung

Ausgewertet wurden die fünf gespeicherten Aegis-1VS1-Läufe vom 06.10.2026.

## Was besser läuft

- Aegis vermeidet in der Regel die frühere 180°-Wende mit Main Thruster in unmittelbarer Kampfentfernung.
- Der tangentiale Einflug erlaubt echte Fly-bys statt eines reinen Frontalanflugs.
- Die Rate-basierte Zielregelung kann eine bewegte Feuerlösung stabil halten: Im erfolgreichen Lauf lag der Aim Error beim Feuern bei etwa 0,2°; fünf von fünf Gegner-Schüssen trafen.
- Über alle fünf Läufe traf Aegis 11 von 15 Schüssen. Damit ist die Waffenführung in einer stabilen ATTACK-Lage belastbar.
- Aegis gewann einen Lauf gegen den Spieler und erzielte dort den tödlichen dritten Hüllentreffer nach 49,5 Sekunden.

## Noch nicht ideal

- ATTACK wird teilweise viel zu früh betreten. Ein Lauf enthielt rund 33 Sekunden mit geladener Lanze innerhalb von 1 km, aber keinen Gegner-Schuss. Der mittlere Aim Error in ATTACK lag dort bei 84°.
- APPROACH, ATTACK und REPOSITION können im 0,35-Sekunden-Abstand wechseln. Das erzeugt sichtbares State-Flattern und unterbricht die Flugplanung.
- Ein Lauf erreichte 112 m Abstand: über der Kollisionsgrenze von 100 m, aber unter der gewünschten Sicherheitsdistanz von 180 m und dem Kampfmindestabstand von 250 m.
- Die Annäherung kann zu passiv sein. In einem Lauf dauerte es 51,7 Sekunden bis zum ersten ATTACK-State; die Distanz wuchs davor zeitweise auf 2,4 km.
- Der Logger schreibt beim Endtick aktuell denselben Event-Snapshot zweimal. Das verfälscht die Eventanzahl im Viewer, nicht die Simulation.

## Nachfolger

Das Nachfolgemodell **Vanguard** behält Aegis unverändert als Vergleichsmodell und verbessert gezielt:

1. ATTACK erst bei höchstens 28° Zielabweichung.
2. einen 220-m-Sicherheitskreis und vorausschauende Closest-Approach-Prüfung.
3. mindestens 1,2 Sekunden REPOSITION, bevor ein neuer ATTACK-Wechsel zulässig ist.
4. eine stärker vorausberechnete Einfluggeschwindigkeit mit seitlichem Versatz.

Neue 1VS1-Läufe werden unter `SpaceSim.Godot/Logs/Vanguard/` gespeichert.
