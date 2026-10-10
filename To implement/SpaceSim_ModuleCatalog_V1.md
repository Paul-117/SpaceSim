# SpaceSim — verbindlicher Modul-Katalog V1

**Zweck:** Datenquelle für den geplanten regelbasierten Ship-Loadout-Generator. Dieser Katalog bildet die in der Konzeptphase festgelegten 51 Module ab. **Keine Werte während der Implementation eigenmächtig balancieren.**

**Benennungsregeln:** Jeder vollständige Modulname ist einzigartig. Die Namen `Aegis`, `Kestrel` und `Vanguard` dürfen **in keinem Modulnamen** vorkommen. Bestehende KI-Namen/Codeklassen (z. B. KI-Modell Kestrel) sind davon nicht betroffen und sollen nicht umbenannt werden.

**Einheiten:** PU = Power Units; U/min = Treibstoffeinheiten/Minute; kN = Schubkraft; m/s = lineare Geschwindigkeit; °/s = Winkelgeschwindigkeit. `PU` bei Boostern ist der Nennleistungsbedarf unter Last, nicht automatisch eine konstante Verbrauchslast im Leerlauf. Für die **Installationsprüfung** werden jedoch die PU-Anforderungen **aller drei Booster gleichzeitig vollständig** eingerechnet; siehe Implementationsplan.

## A. Bow Weapons — 10 Module

| Name | Schaden | Reichweite | Aufladezeit | DPS (Anzeige, gerundet) | Schwenkfenster | Schwenkrate | PU |
|---|---:|---:|---:|---:|---:|---:|---:|
| PEREGRINE L-1000 (Standard) | 20 | 1.000 m | 5,0 s | 4,00 | ±5° | 3,33°/s | 40 |
| RAPTOR | 11 | 750 m | 2,5 s | 4,40 | ±12° | 10°/s | 38 |
| DOOMHAMMER | 36 | 1.400 m | 11,0 s | 3,27 | ±5° | 2°/s | 60 |
| LONGSPEAR | 15 | 1.650 m | 6,5 s | 2,31 | ±3° | 2,5°/s | 46 |
| HELLSTORM | 6 | 500 m | 1,0 s | 6,00 | ±15° | 15°/s | 48 |
| RAVAGER | 28 | 600 m | 5,5 s | 5,09 | ±7° | 5°/s | 42 |
| SPECTRE | 13 | 1.050 m | 3,5 s | 3,71 | ±15° | 12°/s | 50 |
| OBLIVION | 48 | 1.800 m | 15,0 s | 3,20 | ±2° | 1,5°/s | 75 |
| VIPER | 18 | 850 m | 3,8 s | 4,74 | ±8° | 7°/s | 44 |
| WRAITH | 16 | 900 m | 5,0 s | 3,20 | ±6° | 4°/s | 22 |

**Regel:** DPS ist **abgeleitet** (`Schaden / Aufladezeit`) und darf nicht als unabhängiger, potenziell inkonsistenter Simulationsparameter gespeichert werden. Schwenkfenster ist symmetrisch um den Bug. Die gezeigten 3,33°/s der Standardwaffe sind gerundet aus 10°/3 s; bei Genauigkeitsansprüchen die ursprüngliche Semantik bewahren.

## B. Shield Generators — 10 Module

| Name | Schild-HP | Volle Aufladezeit | Reboot nach Schildbruch | PU |
|---|---:|---:|---:|---:|
| GUARDIAN S-20 (Standard) | 20 | 5,0 s | 10 s | 30 |
| PHANTOM VEIL | 12 | 1,5 s | 6 s | 35 |
| IRONCLAD | 40 | 9,0 s | 20 s | 45 |
| CITADEL | 50 | 15,0 s | 30 s | 60 |
| PULSEGUARD | 18 | 2,5 s | 12 s | 42 |
| SENTINEL ARRAY | 30 | 6,0 s | 15 s | 38 |
| ETHEREAL WARD | 15 | 3,5 s | 5 s | 24 |
| BULWARK | 35 | 8,0 s | 18 s | 32 |
| NOVA BARRIER | 25 | 4,0 s | 22 s | 50 |
| QUICKSILVER | 10 | 1,0 s | 8 s | 40 |

**Mechanik-Ziel:** Solange Schild-HP > 0, normal regenerieren; bei Schild-HP ≤ 0 zuerst Reboot, danach Wiederaufladung. Ob das aktuell im Repository exakt so implementiert ist und wie Hull-Schaden berechnet wird, **vor Änderungen prüfen**. Bestehende Damage-/Warp-/Subsystem-Mechanik nicht eigenmächtig überschreiben.

## C. Reactors — 10 Module

| Name | Max Output | Fuel Usage bei Volllast | Ramp-Up 0–100 % |
|---|---:|---:|---:|
| CORE-X125 (Standard) | 125 PU | 7,0 U/min | 60 s |
| SWIFTCORE R-90 | 90 PU | 6,5 U/min | 15 s |
| MONOLITH T-200 | 200 PU | 13,0 U/min | 110 s |
| ECOFLUX P-110 | 110 PU | 4,0 U/min | 45 s |
| INFERNO X-175 | 175 PU | 15,0 U/min | 25 s |
| HORIZON A-150 | 150 PU | 8,5 U/min | 65 s |
| ENDURANCE W-80 | 80 PU | 2,5 U/min | 90 s |
| OVERDRIVE OD-140 | 140 PU | 12,0 U/min | 10 s |
| LEVIATHAN L-250 | 250 PU | 19,0 U/min | 150 s |
| HELIX N-115 | 115 PU | 5,5 U/min | 30 s |

**Hinweis:** Installationsvalidierung gegen `Max Output` bei Nennzustand; tatsächliche Runtime-Leistung bleibt von Reaktorhochlauf, Lastzuweisung, Treibstoff und Subsystemzustand abhängig.

## D. Sensor Arrays — 10 Module

| Name | Min. passive Ortungsreichweite | Max. passive Ortungsreichweite | PU |
|---|---:|---:|---:|
| ARGUS S-200 (Standard) | 1.000 m | 2.500 m | 20 |
| GHOST EYE | 500 m | 1.400 m | 8 |
| RAVEN S-4 | 750 m | 1.800 m | 12 |
| ECHOSHROUD V-7 | 650 m | 2.300 m | 18 |
| ORACLE X-9 | 1.200 m | 3.000 m | 45 |
| HAWKEYE M-3 | 1.100 m | 2.700 m | 32 |
| VOIDSEEKER | 900 m | 3.000 m | 38 |
| WATCHTOWER | 1.500 m | 2.200 m | 28 |
| NIGHTFALL | 500 m | 2.000 m | 15 |
| OMNISCIENT | 1.400 m | 3.000 m | 55 |

**Vorgeschlagene passive Reaktorsignatur-Formel aus der bisherigen Konzeptphase:**

`Ortungsradius(sensor, targetActualReactorPU) = clamp(sensor.MinRangeM + 10 m/PU × max(0, targetActualReactorPU), sensor.MinRangeM, sensor.MaxRangeM)`.

Damit beim ARGUS als Referenz: 50 PU Signatur → 1.500 m; 100 PU → 2.000 m. Systemweite Sensorgrenzen: mindestens 500 m und höchstens 3.000 m. **Wichtig:** Diese Formel ist ein Konzeptvorschlag, nicht als bereits implementiert voraussetzen; in der tatsächlichen Implementierung Spieler- und KI-Ortung sowie aktives Sonar getrennt behandeln.

## E. Main Boosters — 5 Module

| Name | Leistung | Ramp-Up | Max. Vorwärtsgeschwindigkeit | PU |
|---|---:|---:|---:|---:|
| ATLAS M-100 (Standard) | 100 kN | 10 s | 100 m/s | 30 |
| SKYFANG M-85 | 85 kN | 3 s | 120 m/s | 40 |
| DREADNOUGHT M-180 | 180 kN | 18 s | 85 m/s | 48 |
| STARLING M-70 | 70 kN | 5 s | 145 m/s | 35 |
| FIREBRAND M-150 | 150 kN | 7 s | 130 m/s | 60 |

## F. Reverse Boosters — 3 Module

| Name | Leistung | Max. Rückwärtsgeschwindigkeit | PU |
|---|---:|---:|---:|
| ANCHOR R-30 (Standard) | 30 kN | 50 m/s | 30 |
| GRAVEBREAK R-60 | 60 kN | 40 m/s | 45 |
| BACKDRAFT R-20 | 20 kN | 80 m/s | 24 |

## G. Side Boosters — 3 Module

| Name | Leistung | Max. Rotation | PU |
|---|---:|---:|---:|
| VECTOR S-1 (Standard) | 1,0 kN | 10°/s | 30 |
| TALON S-2 | 2,0 kN | 18°/s | 50 |
| COLOSSUS S-4 | 4,0 kN | 7°/s | 42 |

**Physik:** Side-Booster-Wirkung entsteht aus Schubkraft, Angriffspunkt/Hebelarm, Masse/Trägheit; max. Rotation ist die definierte Geschwindigkeitsgrenze, keine direkte Setzung der Winkelgeschwindigkeit. Bestehende Newton-artige Bewegung, Geschwindigkeitsregelung und Drift erhalten.

## Katalog-Validierung (Pflichttests)

- Genau **51 Einträge**: 10 Waffen, 10 Schilde, 10 Reaktoren, 10 Sensoren, 5 Main, 3 Reverse, 3 Side.
- Jeder Eintrag hat eine persistente **ID unabhängig vom Anzeigenamen**, einen eindeutigen Namen, korrekt typisierte Felder und exakt einen Slot-Typ.
- Verbotene Namen nie in Modulnamen; Namensprüfung case-insensitive und als Wort/Stamm passend zur vereinbarten Namensregel.
- Positive bzw. gültige Einheiten/Werte; Waffen-Schwenkfenster ≤ ±15°, Shields: HP ≤ 50, Aufladezeit ≥ 1 s, Reboot ≤ 30 s, Sensoren: 500–3.000 m und Min ≤ Max.
- Genau **ein Standardmodul je Slot-Typ**.
- Keine Namen/Stats stillschweigend verändern; Vorschläge für spätere Balanceanpassungen gesondert dokumentieren.
