# Kestrel – KI-Modell und Duellauswertung

Kestrel ist das aktuelle 1VS1-Gegnermodell. Es entstand nach **Aegis** und **Vanguard** und verwendet ausschließlich normale `ShipCommand`-Befehle. Position, Geschwindigkeit und Rotation werden niemals direkt durch die KI verändert; die gemeinsame Schiffssimulation verarbeitet alle Triebwerks-, Rotations- und Waffenbefehle.

Neue 1VS1-Läufe werden als JSONL-Textdateien im selben Ordner gespeichert.

## Ziel

Kestrel soll offensiv kämpfen, aber keine unnötigen Kollisionsmanöver oder langen Fluchtkurven erzeugen:

1. kontrolliert in Lanzenreichweite kommen;
2. die Nase auf den Spieler halten;
3. mit geladener Lanze bei sauberer Lösung feuern;
4. nur bei einem unmittelbaren Kollisionskurs kurz seitlich ausweichen;
5. danach aktiv wieder in den Angriff zurückkehren.

## Zustände

### ACQUIRE

Initialzustand. Der Gegner übernimmt den bekannten WorldState des Spielers und wechselt unmittelbar zu APPROACH.

### APPROACH

Kestrel richtet die Nase mit einem Rate-Controller auf die aktuelle Sichtlinie zum Spieler aus. Haupt- und Rückwärtstriebwerk regeln die Annäherung:

- außerhalb der bevorzugten Kampfdistanz wird beschleunigt;
- bei zu hoher Annäherungsgeschwindigkeit wird mit Reverse Thrusters gebremst;
- innerhalb von 2 km wird kein Main-Thruster-Wendemanöver zum Bremsen verwendet.

ATTACK wird erst betreten, wenn der Gegner innerhalb von 250–900 m liegt und die Zielabweichung höchstens 24° beträgt.

### ATTACK

Kestrel hält die Nase auf dem Spieler und berücksichtigt dabei die Winkelgeschwindigkeit der Sichtlinie. Die Lanze feuert nur bei:

- voller Ladung;
- Entfernung innerhalb der 1-km-Lanzenreichweite;
- Ziel vor der Bugwaffe;
- Zielabweichung innerhalb der zentralen Feuer-Toleranz von 3°.

Während ATTACK reguliert Kestrel die Distanz weiter mit Main- und Reverse-Thrusters. Bei einer üblichen Kampfgeometrie bleibt die Ausrichtung sehr nah an der Sichtlinie.

### REPOSITION

REPOSITION ist kein Fluchtzustand. Er beginnt nur bei einem direkten vorhergesagten Kollisionskurs und erzeugt eine kurze seitliche Ablenkung. Nach mindestens 0,75 s wechselt Kestrel zwingend zurück zu APPROACH, sobald der Kollisionskurs aufgelöst ist. Dadurch richtet sich die KI wieder auf den Spieler aus, statt dauerhaft seitlich weiterzufliegen.

## Distanz- und Kollisionsregeln

| Regel | Wert | Bedeutung |
|---|---:|---|
| Reale Schiffskollision | unter 50 m | Beide Schiffe werden zerstört. |
| KI-Minimum | 100 m | Kestrel akzeptiert keinen geplanten direkten Vorbeiflug darunter. |
| Manöverabstand | 250 m | Gewünschter Abstand für Flug- und Sicherheitsmanöver. |
| Kollisionsausweich-Trigger | 350 m | Erst hier startet ein seitliches Manöver, wenn der vorhergesagte nächste Abstand weiterhin unter 100 m liegt. |
| Kampfdistanz | 250–900 m | Bereich für ATTACK. |
| Bevorzugte Kampfdistanz | 600 m | Referenz für die Geschwindigkeitsregelung. |
| Lanzenreichweite | 1.000 m | Gameplay-Reichweite der Lanze. |

Diese Regeln gelten auch im Basegame: Die gemeinsame Simulation löst Kollisionen unter 50 m aus; reguläre Gegner und der Spieler-Autopilot verwenden dieselbe 100-/250-/350-m-Kollisionslogik.

## Auswertung der ersten fünf Kestrel-Duelle

Alle fünf aufgezeichneten Duelle endeten mit `player_destroyed`.

- Kestrel gewann **5 von 5** Duellen.
- Es gab keine Schiffskollision.
- Die kleinsten Abstände lagen zwischen **721 m und 843 m**.
- Kestrel trat zuverlässig bei ungefähr 900 m in ATTACK ein.
- Die mittlere Zielabweichung in ATTACK lag in allen Läufen bei ungefähr **0,2°**.
- Bereinigt um den aktuell doppelt geloggten Endtick traf Kestrel **21 von 21** Gegner-Schüssen.
- Ein längerer Lauf zeigte eine REPOSITION von 0,8 s, danach die geplante Rückkehr zu APPROACH und anschließend erneut ATTACK.

## Bekannte Einschränkung

In einem langen Lauf stieg die Distanz zeitweise auf rund **3,16 km**, bevor Kestrel wieder in den Kampf zurückfand. Das Modell gewinnt den Lauf trotzdem, aber die langfristige Einflugplanung kann später noch effizienter werden.

## Abgrenzung zu älteren Modellen

- **Basic AI** bleibt das reguläre Encounter-Modell.
- **Aegis** ist das erste präzise Duellmodell, kann aber gefährlich nahe kommen und State-Flattern zeigen.
- **Vanguard** verbessert Sicherheitsmanöver, blieb jedoch gelegentlich dauerhaft in REPOSITION.
- **Kestrel** verbindet präzise Feuerführung mit kurzer Kollisionsabweichung und einer garantierten Rückkehr zum Angriff.
