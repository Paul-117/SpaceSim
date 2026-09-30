# Sensorium Station Prototype

Eigenstaendiger HTML/CSS/JavaScript-Prototyp fuer eine Sensorstation. Die
2x2-Anordnung verwendet oben ein grosses Spektrometerfenster und zwei kleinere
Fenster darunter. Die Seite hat eine feste Bildschirmhoehe und scrollt nicht.

Das Spektrometer zeichnet eine statische, gebinnte Verteilung von 380 bis 780 nm.
Die Grundstruktur entspricht der Einhuellenden eines gescannten Schifftyps. Vier
schmale Emissionspeaks liegen an den fuer diesen Typ erwarteten Wellenlaengen:
Reaktor, Schilde, Antrieb und Waffen. Ihre Hoehe wird separat durch den aktuellen
Systemzustand bestimmt. Die umschaltbare Bibliothek zeigt nur Vergleichs-
Einhuellende, damit die passende Signatur visuell identifiziert werden kann.
Fuer die aktuell ausgewaehlte Vorlage markiert das Diagramm die erwarteten
Reaktor-, Schild-, Antriebs- und Waffenbereiche mit gestrichelten Zonen.
Mit Enter wird die aktuelle Auswahl bestaetigt. Bei einer passenden Vorlage
erscheinen im rechten unteren Fenster Name, Klasse und die vier Peak-Leistungen;
Estimated Distance und Hull Integrity sind dort bewusst noch Platzhalter.
`GENERATE` erstellt einen neuen Kontakt aus einer der vorhandenen Vorlagen und
variiert Name, Systemleistungen und Messrauschen. Jede generierte Signatur passt
weiterhin zu genau einer Schiffseinhuellenden.

Das linke untere Fenster ist die Peilung fuer das Spektrometer. A und D drehen
die Scanlinie um das eigene Schiff. Je genauer die Linie auf den Kontakt zeigt,
desto staerker wird die im Spektrometer gemessene Signatur.

P wechselt das linke untere Fenster zwischen passiver Peilung und aktivem Sonar.
Beide Sensoren beziehen sich auf dieselben Gegner. Das Sonar zeigt sie als rote
Punkte auf einem rotierenden Scan; nach korrekter Identifikation wird die
geschaetzte Entfernung des gewaehlten Kontakts im rechten unteren Fenster angezeigt.

Jede Generierung enthaelt zwei Gegner. Nach ihrer Identifikation wechselt L durch
die bestaetigten Kontakte. Das Sonar markiert identifizierte Gegner mit einem
gelben Kreis; der Kontakt, dessen Werte rechts sichtbar sind, erhaelt zusaetzlich
einen mintfarbenen Rahmen.
