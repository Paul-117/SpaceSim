# SpaceSim — Schritt-für-Schritt-Prompts für Codex

## Vorbereitung

1. Lege diese **drei Markdown-Dateien** in das SpaceSim-Repository, vorzugsweise gemeinsam in einen Projekt-Dokumentationsordner:
   - `SpaceSim_ShipGenerator_ImplementationPlan.md`
   - `SpaceSim_ModuleCatalog_V1.md`
   - `Codex_StepByStep_Prompts.md`
2. Öffne Codex im Repository und weise auf die beiden ersten Dateien hin. Alle Prompts setzen voraus, dass Codex den Repository-Inhalt selbst untersuchen kann.
3. Arbeite **einen Prompt pro Codex-Auftrag** ab. Prüfe das Ergebnis; gehe erst danach weiter.
4. Falls Codex die Dokumente nicht lesen kann, kopiere sie zunächst in seinen Arbeitskontext.
5. Die Prompts sind bewusst so gebaut, dass Codex nicht unkontrolliert das komplette Projekt umschreibt.

**Standard-Abnahme für jeden Schritt:** Codex muss am Ende (a) Änderungen/Dateien nennen, (b) Befehle und reale Testergebnisse berichten, (c) offene Fragen/Annahmen dokumentieren, (d) bei Fehlern nicht behaupten, der Schritt sei abgeschlossen. Nicht ungefragt mit dem nächsten Schritt fortfahren.

---

## Prompt 0 — Repository-Audit: erst verstehen, nichts verändern

```text
Du arbeitest an meinem SpaceSim-Projekt. Lies zuerst vollständig:
- SpaceSim_ShipGenerator_ImplementationPlan.md
- SpaceSim_ModuleCatalog_V1.md

WICHTIG: In diesem Schritt keinerlei Code oder Konfigurationsdateien ändern.

Untersuche stattdessen den tatsächlichen Repository-Stand und erstelle einen technischen Ist-Bericht mit konkreten Dateipfaden, Klassen, Methoden und Datenflüssen zu:
1. SpaceSim.Core, Godot und Stations/WebSocket-Snapshots.
2. Ship-Erzeugung in Encountern, KI-Duellen und Gefechtslabor.
3. Waffen, Schilden, Hull/HP, Reaktoren, Treibstoff, Leistungszuweisung und Subsystemzustand.
4. Main-, Reverse- und Side-Boostern, Schub-/Trägheitsphysik, aktuellen PU-Budgets.
5. Sensorium, passiver Erkennung, aktivem Sonar, Kontaktidentifikation.
6. Kestrel-Zustandsmaschine und deren Kampfdistanzparametern.
7. Bestehenden Tests, Build-Kommandos, Logs und Konfigurationskonventionen.

Liste ausdrücklich Diskrepanzen zu den Konzeptdokumenten (besonders 30 HP vs. 3 Hull-Punkte, Sensor-PU und 3 Booster-PU). Schlage eine minimal-invasive Dateistruktur für den Generator und einen migrationssicheren Weg für Legacy-Schiffe vor. Unterscheide Fakten aus Code und Vorschläge. Beende nach dem Audit mit einer klaren Checkliste für Schritt 1.
```

**Prüfen:** Sind alle technischen Annahmen tatsächlich durch Dateipfade belegt? Keine Codeänderungen? Ist das Energie-/Hull-Problem erkannt?

---

## Prompt 1 — 51 Moduldefinitionen und Datenvalidierung

```text
Setze ausschließlich Meilenstein M1 aus dem Implementationsplan um.

Implementiere im autoritativen SpaceSim.Core einen einzigen kanonischen Modul-Katalog gemäß SpaceSim_ModuleCatalog_V1.md, passend zur vorhandenen Repository-Konvention. Verwende stabile persistente IDs, getrennte Anzeigenamen und typsichere Definitionen je Modulslot. Erfasse exakt alle 51 Module mit den vereinbarten Parametern und Einheiten. Keine Werte eigenmächtig ändern.

Schreibe Tests für:
- Exakt 51 Module und die 7 Slot-Anzahlen (10/10/10/10/5/3/3).
- Eindeutige IDs/Namen; genau ein Standardmodul pro Slot.
- Keine Modulnamen mit Aegis, Kestrel oder Vanguard.
- Vollständige Pflichtfelder und gültige Parametergrenzen.
- Referenzwerte einiger Standard- und Extremmodule.

Bestehende Ship-/Gameplaylogik, KI, Godot und Webstationen in diesem Schritt unverändert lassen. Führe die relevanten Builds/Tests aus und berichte anschließend die geänderten Dateien und Ergebnisse. Stoppe nach M1.
```

**Prüfen:** Einziger Katalog? Keine Kopien der Daten in Godot? Verbotene Wörter betreffen nur *Modulnamen*, nicht den KI-Code.

---

## Prompt 2 — Corvette-Klasse und Combat Doctrines

```text
Setze ausschließlich Meilenstein M2 aus dem Implementationsplan um.

Führe das Datenmodell für ShipClassDefinition und CombatDoctrineDefinition im Core ein, mit klar getrennten harten Klassengrenzen und weichen Doktrinpräferenzen.

Implementiere als erste generierbare Klasse Corvette: je ein Slot pro Kategorie, Reaktor 110–175 PU, Waffen-Einzelschaden <=36, Waffenreichweite <=1650 m, Schild-HP <=40, Main-Booster-Schub <=150 kN. Keine unbestätigten Grenzen für Frigate/Interceptor erfinden.

Implementiere Corvette-Doktrinen und Gewichtungen: Patrol 40 %, Assault 35 %, Standoff 25 %. Definiere die Präferenzkriterien über Eigenschaften (Waffenreichweite, Sensorabdeckung, Mobilität, Schilde usw.), nicht über hartcodierte Modulnamen.

Ergänze Unit-Tests für Klassenfilter, inklusive zulässiger Grenzwerte und korrekt abgelehnter Module. Noch keine Schiffsgenerierung, keine Änderungen an KI/Physik. Tests ausführen und stoppen.
```

**Prüfen:** `OBLIVION` wird in Corvette wegen Schaden/Reichweite abgelehnt, `LEVIATHAN` wegen Reaktorlimit, `CITADEL` wegen Schild-HP; erlaubte Grenzwerte funktionieren.

---

## Prompt 3 — Harte Loadout-Validierung und 100/80-Energieregel

```text
Setze ausschließlich Meilenstein M3 um und lies Abschnitt 2 des Implementationsplans vor der Änderung erneut.

Schreibe einen reinen, gut testbaren LoadoutValidator mit strukturierter Fehlerdiagnostik. Er prüft genau einen Eintrag je erforderlichem Slot, existierende IDs, richtige Slot-Typen, Corvette-Grenzen und die verbindliche Energieberechnung:

RequiredPU = MainPU + ReversePU + SidePU + 0.8 * (WeaponPU + ShieldPU + SensorPU)
Valid nur falls RequiredPU <= ReactorMaxOutputPU.

Alle drei Booster zählen gleichzeitig zu 100 %, unabhängig davon, ob sie im Flug zeitgleich benutzt werden. Die vorher diskutierte max()-Regel ist VERWORFEN. Keine Runtime-Energiezuteilung in diesem Schritt ändern.

Schreibe exakte Tests:
- Standardschiff: 162 PU Bedarf gegen CORE-X125 125 PU => ungültig, Defizit 37 PU.
- Sparsamste Module: 127,2 PU Bedarf, damit 125 PU ungültig.
- OVERDRIVE-140-Beispiel: 127,2 PU => gültig.
- HORIZON-150-Beispiel: 141,6 PU => gültig.
- INFERNO-175-Assault-Beispiel: 174,2 PU => gültig.
- Genau Gleichheit gilt, minimale Überschreitung ist ungültig.

Legacy-Test-/Spielerschiffe weiterhin starten lassen, ohne ihre Modulwerte zu ändern. Gib konkrete Fehlercodes und PU-Zerlegung aus. Tests ausführen und stoppen.
```

**Prüfen:** `<=` richtig; kein Rundungsfehler bei 174,2; das alte Standardsetup wird **nicht** als generiert gültig getarnt.

---

## Prompt 4 — Deterministischer Generator und Soft Scoring

```text
Setze ausschließlich Meilenstein M4 um.

Implementiere einen deterministischen ShipLoadoutGenerator im Core, der Corvette + optionalen Doctrine-Override + Seed annimmt, gültige Kandidaten erzeugt und zufällig mit Gewichten auswertet. Erst harte Klassen-/Slot-/Energievalidierung, danach Soft Score. Nicht immer nur den höchsten Score wählen: Vielfalt ist gewünscht.

Soft-Faktoren: DoctrineFit, WeaponMobilityFit, WeaponSensorFit, EnergyMargin, Survivability. Definiere die Kriterien nachvollziehbar und testbar ohne if-Sonderfälle für konkrete Modulnamen. Nutze begrenzte Suche, Pre-Filtering/Caching oder Backtracking. Bei Unlösbarkeit strukturiertes NoValidLoadout statt Endlosschleife/illegalem Fallback.

Die Reihenfolge des Katalogs und der Zufallsgenerator müssen die Reproduzierbarkeit sichern. Logge Klasse, Doktrin, Seed, Modul-IDs, Score, RequiredPU und PowerMarginPU.

Tests: Gleicher Seed/Regelstand => identische Module. Mindestens 1000 Seeds => alle Ergebnisse gültig oder ausdrücklich begründete Fehler, reproduzierbare Stichproben, mehrere verschiedene Loadouts/Doktrinen. Noch nicht in echte Schiffe einbauen. Tests ausführen und stoppen.
```

**Prüfen:** Unter harter Energiepflicht kann CORE-X125 nicht als Reaktor eines generierten Schiffs erscheinen; Nicht-Verwendbarkeit wird nicht versteckt.

---

## Prompt 5 — Reaktor- und Energieanbindung

```text
Beginne M5 in einem kleinen Schritt: Binde zuerst ausschließlich das ausgerüstete Reaktormodul und die Nenn-PU der Module an die bestehenden Core-Energiesysteme an.

Untersuche vorhandene Propulsion-/Weapons-/Shields-/Sensor-Leistungsbudgets vor Änderung. Erhalte Reaktor-Ramp, Fuel Usage und Condition sowie Voltarium-Zuweisung. Stelle sicher, dass das Generator-Budget (volle drei Booster + je 80 % andere) eine Installationsregel ist und nicht fälschlich als konstante Runtime-Leistung oder doppelte Abbuchung implementiert wird.

Führe möglichst eine schmale Mapping-/Adapter-Schicht für Modulwerte ein. Bestehende Legacy-Schiffe behalten ihr bisheriges Verhalten, bis sie explizit ein Loadout tragen. Schreibe Unit-/Integrationstests für unterschiedliche Reactor-Max-Outputs, Ramp-Up und Unterversorgung.

Keine Waffen-/Schadens-/KI-Änderungen in diesem Schritt. Tests ausführen und stoppen.
```

**Prüfen:** Reaktor-`MaxOutput` wird korrekt, aber `ActualOutput` ist bei Ramp-Up nicht fälschlich sofort maximal. Kein doppelt gezählter Propulsion-Verbrauch.

---

## Prompt 6 — Waffenmodul ins echte Kampfmodell einhängen

```text
Setze den Waffen-Teil von M5 um. Waffenwerte für Damage, Range, ChargeTime, TraverseHalfAngle, TraverseRate und RequiredPU sollen für Schiffe mit Loadout aus dem installierten Bow-Weapon-Modul kommen.

Bestehenden Raycast, Schussprüfung, Turmausrichtung, Waffenkondition, Ladeverhalten und FireLance-Command beibehalten. KI und Spieler müssen denselben Core-Code nutzen. Ohne Loadout gilt das bisherige Standardverhalten.

Schreibe Tests für mindestens PEREGRINE, HELLSTORM und OBLIVION: geänderte Reichweite, Schaden, Ladezeit und Winkelgrenzen tatsächlich nachweisbar. Keine Änderung des bestehenden Hull-Schadensmodells. Tests ausführen und stoppen.
```

**Prüfen:** Ein anderer Waffenname allein reicht nicht; Simulationswerte ändern sich messbar.

---

## Prompt 7 — Schildmodul integrieren

```text
Setze den Shield-Teil von M5 um. Verknüpfe pro Schiff Schild-HP, volle Aufladezeit, Reboot und RequiredPU mit dem installierten Shield-Generator-Modul, ohne vorhandene Hull-/Subsystem-Schadenslogik zu ersetzen.

Prüfe zuerst die echte vorhandene Semantik für Schildbruch. Zielregel: Schild >0 regeneriert; Schild <=0 geht zuerst in Reboot und regeneriert erst danach. Halte Schildzustand und Condition pro Schiff, Moduldefinition immutable. Warp-Reparaturen nach Bestandslogik erhalten.

Teste Guardian, Quicksilver und Citadel inklusive Treffer über Schildgrenze, genau null HP, Reboot und niedriger Energie. Alten Duellmodus nicht beschädigen. Tests ausführen und stoppen.
```

**Prüfen:** Kein Reboot-Bypass, keine unbeabsichtigte neue 30-HP/3-Hull-Definition.

---

## Prompt 8 — Main/Reverse/Side Booster an Physik anbinden

```text
Setze den Propulsion-Teil von M5 um. Main-, Reverse- und Side-Booster beziehen Force, maximale Geschwindigkeit/Rotation, Main-Ramp-Up und PU aus den ausgerüsteten Moduldefinitionen.

WICHTIG: Keine direkte Setzung von Position, Geschwindigkeit, Rotation oder Winkelgeschwindigkeit. Bestehende Newton-Physik mit Masse, Hebelarmen, Trägheitsmoment und kontrollierter Beschleunigung beibehalten. Geschwindigkeit oberhalb eines reduzierten Limits nicht hart begrenzen; Schiff driftet und muss bremsen. Reverse/Side haben nach aktuellem Konzept keinen künstlich erfundenen Ramp-Up.

Schreibe Tests für ATLAS vs SKYFANG und VECTOR vs TALON vs COLOSSUS, mit konstanten Schiffsparametern, um tatsächliche Unterschiede zu messen. Insbesondere gemeinsam genutzte Propulsion-PU-Budgets korrekt behandeln. Tests ausführen und stoppen.
```

**Prüfen:** Die Kraft aus `COLOSSUS` darf physikalisch anders wirken als nur ein Winkel-Speed-Wert; keine Teleport-/SetVelocity-Abkürzung.

---

## Prompt 9 — Passive Sensoren und Reaktorsignatur

```text
Setze M6 um: Implementiere modulabhängige passive Ortung im Core, ohne aktives Sonar oder Sensorium-Identifikationsregeln zu überschreiben.

Als V1-Konzept gilt:
PassiveRangeM = clamp(SensorMinM + 10 * max(0, TargetActualReactorOutputPU), SensorMinM, SensorMaxM)

Die Reichweite gehört zum Sensor des BEOBACHTERS, die Signatur zum aktuellen Reaktoroutput des ZIELS. ARGUS: Ziel mit 50 PU => 1500 m, Ziel mit 100 PU => 2000 m. Globale Untergrenze 500 m, Obergrenze 3000 m.

Prüfe zunächst vorhandene Sensorium-/Contact-/Sonar- und 1v1-Ausnahmeregeln. Detektion ist nicht automatisch Identifikation. Aktives Sonar (10-km-Radius + Gegnerwarnung) bleibt getrennt. Leckage vollständiger Kontaktdaten vermeiden. Sensorzustand und Energie-/Condition-Skalierung an bestehende Prinzipien anpassen und alle Abweichungen dokumentieren.

Teste Sichtbarkeit bei 0/50/100/250 PU Zieloutput, exakte Reichweitengrenzen, verlorene Kontakte und getrenntes Sonar. Tests ausführen und stoppen.
```

**Prüfen:** Keine magische Kenntnis außerhalb des Duell-Sondermodus; kein Verlust der vorhandenen Sensorium-Spielschleife.

---

## Prompt 10 — Kestrel liest generiertes Taktikprofil

```text
Setze M7 um, ohne die bestehende Kestrel-FSM neu zu schreiben.

Leite für jedes generierte Schiff aus Waffe, Sensor, Bewegung und Combat Doctrine ein ShipTacticalProfile ab. Verwende dieses für PreferredRange, AttackRange und sinnvolle Annäherung/Repositionierung. Assault eher Nahkampf, Patrol mittlere Distanz, Standoff Fernkampf; exakte Zielwerte über vorhandene Sicherheits- und Kollisionslogik begrenzen.

Bewahre ACQUIRE -> APPROACH -> ATTACK -> REPOSITION -> DESTROYED, bestehende ShipCommands, Thruster-Physik und Anti-Kollisions-Regeln. Keine freie Zielkenntnis hinzufügen. Für alte nicht generierte Schiffe die bisherigen KI-Defaults verwenden.

Teste anhand kurzer reproduzierbarer Läufe mindestens HELLSTORM-, PEREGRINE- und LONGSPEAR-Konfigurationen. Erkläre, welche Abstände tatsächlich angestrebt werden und ob die KI sie erreichen kann. Tests ausführen und stoppen.
```

**Prüfen:** LONGSPEAR-KI verschenkt ihre Reichweite nicht dauerhaft durch feste 600-m-Zieldistanz.

---

## Prompt 11 — Corvette-Spawning und minimale Debug-Anzeige

```text
Setze M8 um. Integriere die Corvette-Generierung an genau einer geeigneten, vorher im Audit identifizierten Encounter-/Spawn-Stelle. Bestehende festen Encounter, Warp, Testmodi und Legacy-Spawns dürfen nicht unbemerkt umgestellt werden.

Beim Spawn Klasse, Doctrine, Seed und Modul-IDs übergeben und das vollständige Loadout ins Schiffsmodell einhängen. Ergänze eine kleine Entwickleransicht oder eine klar lesbare Debug-/Logausgabe: Name/ID der 7 Module, Doctrine, Seed, Waffe, Schild, ReactorMaxPU, RequiredPU, PowerMarginPU und abgeleitete KI-Wunschantfernung.

Die autoritativen Regeln bleiben im Core. Falls Daten in Godot oder Stations angezeigt werden: nur zweckgebundene Snapshots ergänzen, keine neue Spiellogik in Browser-JS. Tests und einen manuellen Beispielspawn dokumentieren. Stoppen.
```

**Prüfen:** Ein konkreter Seed erzeugt nach jedem Neustart dasselbe nachvollziehbare Schiff.

---

## Prompt 12 — Gefechtslabor, Logs und reproduzierbare Experimente

```text
Setze M9 um. Ergänze das vorhandene Gefechtslabor um eine optionale Möglichkeit, je Seite Klasse Corvette, Doctrine (oder Random) und Seed zu verwenden. Die bisherigen direkten Testparameter und die alten Logs bleiben kompatibel.

Füge zu jeder neuen Simulationssitzung Modul-IDs, ReactorMaxPU, RequiredPU, Doctrine, Seed und Taktikprofil in die Logs/Metadaten ein, ohne vorhandene Datenfelder zu löschen. Zeige/berichte die wichtigsten Kennzahlen: Winrate, Kampfzeit, Trefferrate/DPS, erster Trefferabstand, Schildbrüche, minimale Distanz, Kollisions-/Timeout-Ereignisse, ggf. PU-Unterdeckung.

Führe einen kleinen deterministischen Vergleich von Patrol/Assault/Standoff mit dokumentierten Seeds durch. Noch keine automatische Balanceänderung der Module. Bericht mit technischen Auffälligkeiten erstellen und stoppen.
```

**Prüfen:** `Tools/GefechtsSimulation` startet weiterhin; alte Log-Sitzungen lassen sich lesen.

---

## Prompt 13 — Abschließendes Regression Audit

```text
Setze M10 um: Führe ein vollständiges, strukturiertes Abschluss-Audit des Ship-Generator-V1 durch. Verändere zuerst nichts, sondern baue/teste das Projekt und überprüfe den Implementationsplan gegen den wirklichen Stand.

Prüfe: 51 korrekte Module, verbotene Modulnamen, Corvette-Grenzen, strenge Booster-100 % / Sonstiges-80 %-Energieregel, Legacy-Ausnahme, Seed-Reproduzierbarkeit, Validierungsfehler, echte Runtime-Wirksamkeit, passives Sensorverhalten, aktives Sonar, Kestrel-FSM, Warp, Godot/Stations-Snapshots, Gefechtslabor und alte Duelle.

Erstelle eine Abnahmetabelle mit PASS/FAIL/NOT TESTED je Kriterium und Dateipfad bzw. Testbeleg. Behebe nur klar lokalisierte Fehler; für größere Refactorings zunächst begründeten Vorschlag vorlegen. Dokumentiere offene Balancepunkte getrennt von technischen Defekten. Stoppe nach dem Audit.
```

**Prüfen:** „Nicht getestet“ wird als solcher Status geführt und nicht als Erfolg verkauft.

---

## Optional: Zusatzprompt nach Abschluss — Balance-Runde, nicht Teil von V1

```text
Nutze die bestehenden 51 Modulwerte unverändert und analysiere jetzt ausschließlich die Balance anhand reproduzierbarer Gefechtslabor-Experimente. Suche nach dominanten oder praktisch niemals verwendeten Modulen unter der harten Energiepflichtregel. Erzeuge Tabellen zu Nutzungsraten, Winrates und Energieengpässen je Corvette-Doktrin. Schlage konkrete Balanceanpassungen nur als dokumentierte Vorschläge vor. Ändere keine Katalogwerte und keine KI-Parameter ohne separate Freigabe.
```

## Kurzes Review-Schema für die Zusammenarbeit mit Codex

Nach jedem Schritt kontrollieren:

1. **Scope:** Hat Codex wirklich nur den beauftragten Bereich bearbeitet?
2. **Architektur:** Neue Regeln nur im Core, Godot/Stationen nur als Anzeige/Commands?
3. **Regression:** Wurden alte Modi und die vorhandene Simulationsphysik geschützt?
4. **Tests:** Welche wurden wirklich ausgeführt, welche nur erwähnt?
5. **Semantik:** Wurde die strenge 100/80-Energieregel irgendwo versehentlich zu einer weicheren Regel umgedeutet?
6. **Nachvollziehbarkeit:** Kannst du aus einem Seed und einem Log den generierten Gegner rekonstruieren?

**Wenn ein Schritt scheitert:** Codex zunächst konkret den Fehler und die minimal nötige Korrektur bearbeiten lassen; erst danach den nächsten Prompt starten.
