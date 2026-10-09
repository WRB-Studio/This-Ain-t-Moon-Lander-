# Raumschiffe mit Flugassistenz

Sieben neue Schwarz-Weiß-Outline-Schiffe mit Flugassistenz und sieben klassische Lander mit ihrer bisherigen Steuerung in einer getrennten Testszene. Die neue Flotte hat unterschiedliche Rollen-Silhouetten und enthält wieder den schnittigen Interceptor. Nase oben, Haupttriebwerk unten; Grafiken, Größen und Vergleich sind unter [RoleDistinctFleet](RoleDistinctFleet/README.md) dokumentiert. Die früheren Entwürfe bleiben erhalten. Für die neuen Unity-Prefabs blendet ein eigenes Material den schwarzen Bildhintergrund aus; ein schwarzes Rumpf-Mesh deckt die Hintergrundsterne ab.

`ClassicLander01` bis `ClassicLander07` sind unabhängige Testkopien der ursprünglichen Lander. Original-Sprites, Polygonformen und die Originalskalierung bleiben erhalten. Jede Kopie besitzt den echten `LanderController`. `ClassicLanderFlight` liefert nur Eingabe und Testumgebung an diesen Controller; es enthält keine eigenen Fluggleichungen. Normales Spiel und Testszene verwenden denselben `ApplyFlightInput`-/`ApplyThrust`-Code. Der Testcontroller ist für automatische Game-/Save-Callbacks deaktiviert und wird gezielt vom Sandbox-Adapter aufgerufen. Schubkraft, Drehwerte, Gravitation, Verbrauch und sichere Landewerte stammen aus den Original-Prefabs. Die normale Hauptszene und die Lander-Prefab-Dateien werden nicht verändert. Auswahl über die zusätzlichen `Lander 01` bis `Lander 07`-Buttons oder Tab.

Bei den alten Schiffen: Maus gedrückt halten gibt Schub entlang der Nase; seitliche Mausposition kippt das Schiff. Die Bildschirmposition wird direkt an die originale Maussteuerung weitergegeben. Loslassen schaltet das Triebwerk ab, ohne automatisch zu bremsen oder zu schweben. Es gibt keine Start- oder Landeassistenz und keine WASD-/Q/E-Flugsteuerung für die klassischen Schiffe. Der Flugassistenz-Button ist bei diesen Schiffen deaktiviert. Ihr Inspector zeigt stattdessen **Klassische Lander-Steuerung**. Lokale Pad-Gravitation, Tankversorgung und die Teststation bleiben die Umgebung dieser Vergleichsszene.

| Datei | Rolle | Gedachtes Verhalten |
| --- | --- | --- |
| Courier.png | Kurier und erstes Raumschiff | Gutmütig, wendig, kräftige Bremsassistenz |
| Scout.png | Erkundung und Signale suchen | Schnell, leicht, große Reichweite |
| CargoTug.png | Abschleppen und Bergung | Viel Schub, präzise Bewegung mit angehängter Last |
| Freighter.png | Größere Frachttransporte | Hohe Kapazität, langsamere Beschleunigung |
| Rescue.png | Personen retten und transportieren | Ruhiges Flugverhalten und großzügiger Innenraum |
| Maintenance.png | Reparaturen und Wartung | Präzises Schweben, externer Greifarm |

Die Flugwerte sind implementiert. Spezielle Missionsfähigkeiten wie Scannen, Abschleppen mit diesen Schiffen, Reparieren oder Passagiertransport bleiben spätere Aufgaben. Die genauen Imagegen-Prompts liegen neben den jeweiligen PNGs.

## Testszene öffnen

`Assets/Scenes/SpacecraftFlightSandbox.unity` im Unity-Editor öffnen und Play drücken. Die Szene benutzt keine normale Spielrunde, keine Schiffsauswahl und keine Spielstanddateien. Sie ist nicht in den Build-Szenen eingetragen. Der Courier ist zunächst ausgewählt und geparkt.

- WASD/Pfeile oder Maus/Touch gedrückt halten: Bewegungsrichtung. Zum Starten nach oben steuern.
- Beim Start steigt das Schiff zuerst parallel zum Pad auf. Drehung und seitliche Bewegung werden erst freigegeben, sobald auch ein gedrehter Rumpf das Pad sicher verfehlen würde. Die benötigte Höhe wird aus den Schiff-Collidern bestimmt. Loslassen bremst auch während dieser Phase. `Takeoff Speed` und `Takeoff Clearance` sind am Schiff einstellbar.
- Loslassen: automatisches Bremsen bei aktiver Flugassistenz.
- Leertaste: aktiv bremsen, auch bei abgeschalteter Assistenz.
- Q/E: separat drehen; ohne manuelle Drehung richtet sich die Nase zur Flugrichtung aus. Beim Landeanflug richtet sich das Schiff zum Pad aus.
- F oder UI-Button: am normalen Pad aussteigen beziehungsweise beim nahen geparkten Schiff einsteigen. Zu Fuß A/D/Pfeile oder Maus benutzen; die normalen Pads sind über einen durchgehenden Laufweg verbunden.
- Tab oder Schiffbutton: anderes Schiff auswählen, solange das aktuelle Schiff geparkt oder beschädigt ist; zu Fuß ist der Direktwechsel ebenfalls möglich.
- R: ausgewähltes Schiff auf sein Startpad zurücksetzen, sofern dieses frei ist.
- Mausrad: Kamera näher/heraus zoomen.
- Flugassistenz-Button: automatische Bremse, Schwebe- und Ausrichtungshilfe abschalten oder einschalten.

Nahe einem freien Pad reduziert die Landehilfe das seitliche Tempo und die Sinkgeschwindigkeit. Eine langsame Landung mit passender Ausrichtung parkt das Schiff fest; im Stillstand wird automatisch aufgetankt. Zu harte Zusammenstöße markieren das Schiff als beschädigt. Ohne Treibstoff funktionieren Schub und Assistenz nicht mehr. Wand- und Deckenpads erlauben anders ausgerichtete Landungen, aber keinen Ausstieg für den Astronauten.

## Unterschiedliche Werte

Alle Werte sind im `AssistedSpacecraft`-Inspector am Schiff beziehungsweise Prefab bearbeitbar, auch im Play Mode. Play-Mode-Änderungen anschließend außerhalb des Play Mode im Prefab speichern.

Zum gemeinsamen Abstimmen im Play Mode das GameObject **Spacecraft Sandbox** auswählen. Sein Inspector zeigt unter **Steuerung des aktiven Schiffs** automatisch die Einstellungen des gerade ausgewählten Schiffs, mit deutschen Bezeichnungen und Tooltips: Drehgeschwindigkeit, Beschleunigung, Bremse, Starttempo, Startabstand, seitliches Anflugtempo, Sinktempo, Aufsetztempo und Landewinkel. Beim Schiffwechsel wechselt auch dieser Einstellbereich. Änderungen gelten für das jeweilige Schiff.

Maus- und Touch-Gesten über dem HUD steuern das Schiff nicht. Ein UI-Gestus bleibt bis zum Loslassen für den Flug gesperrt, auch wenn der Zeiger danach aus dem Button herausgezogen wird.

Der Kameraabstand im Schiff ist im `SpacecraftSandbox` unter `Camera Size` eingestellt (Standard 22); `Foot Camera Size` gilt zu Fuß (15). Unabhängig vom gewünschten Zoom hält die Kamera mindestens so viel Abstand, dass das gesamte gedrehte Schiff samt Sicherheitsrand im verfügbaren Bildbereich bleibt. Bildschirmformat und HUD werden berücksichtigt. Beim Einsteigen oder Schiffwechsel wird der Bildausschnitt sofort angepasst.

| Schiff | Masse | Max. Tempo | Beschleunigung | Bremse | Seitlicher Schub | Drehen °/s | Tank | Verbrauch/s bei vollem Schub |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Courier | 1 | 12 | 9 | 14 | 9 | 180 | 100 | 0.55 |
| Scout | 0.8 | 17 | 11 | 16 | 10 | 220 | 140 | 0.45 |
| Schlepper | 2.5 | 8 | 12 | 16 | 11 | 120 | 160 | 0.9 |
| Frachter | 5 | 7 | 4.5 | 8 | 4 | 70 | 240 | 1.2 |
| Rettungsschiff | 2 | 10 | 7 | 14 | 8 | 140 | 160 | 0.65 |
| Wartungsschiff | 1.8 | 7 | 6 | 15 | 10 | 150 | 130 | 0.6 |

Zusätzlich verbrauchen Drehen und das Ausgleichen lokaler Gravitation Treibstoff. Die Werte sind Startwerte für manuelles Ausprobieren, keine abschließend abgestimmte Balance.

## Station erweitern

Die gemeinsamen Prefabs liegen in `Assets/Prefabs/Spacecraft/AssistedFlight`:

- `FlightTestStation`: eine lange Reihe aus 17 normalen Pads, anschließend ein Wand- und ein umgedrehtes Pad. Die sieben neuen Schiffe stehen auf Pads 01 bis 07; die sieben alten Modelle auf Pads 11 bis 17. Drei normale Pads bleiben für Landemanöver frei.
- `DockingBay`: weiteres Pad in die Szene ziehen oder bestehendes duplizieren. `LeftConnection` und `RightConnection` helfen beim Anschluss an Laufwege. Der Root liegt auf der Oberfläche. Die Root-Drehung legt die Landerichtung fest. Für Wand/Decke `Allow Exit` deaktivieren.
- Schiff-Prefabs: weiteres Schiff hinzufügen, freien `Home Pad` zuweisen und im Editor platzieren. Eindeutigen Namen mit laufender Nummer verwenden; der Testcontroller findet alle Schiffe beim Start und sortiert nach Namen. Neue Schiffe sind automatisch über Tab und Einsteigen erreichbar. Zusätzliche Direktwahlbuttons können aus `SandboxButton` angelegt und über `SelectShip(index)` verbunden werden.
- `SandboxPilot` und `SandboxButton`: wiederverwendbare Figur und UI-Button.

Die Stationsgeometrie, Collider, Startplätze und das HUD sind im Editor bearbeitbar. `Tools > Spacecraft > Build Flight Sandbox` erzeugt diese Assets neu; Änderungen an der generierten Testszene vorher sichern. Die normale Hauptszene und die bisherigen Lander werden dabei nicht geändert.

`Tools > Spacecraft > Add Classic Ships To Sandbox` ergänzt die alten Modelle in einer bestehenden Testszene, ohne die sechs neuen Schiff-Prefabs erneut zu erzeugen. `Tools > Spacecraft > Use Original Lander Controls` übernimmt die Steuerungswerte aus den Original-Landern in die Testkopien; dabei werden zuvor manuell geänderte klassische Flugwerte ersetzt. Diese Flugwerte und die gemeinsame Tankgröße sind im gleichen Steuerungs-Inspector einstellbar.

## Verifikation

Unity-Kompilierung, Asset-Erstellung und statische Editor-Vorschau wurden durchgeführt. Keine Play-Mode- oder automatisierten Tests ausgeführt. Noch manuell prüfen: Starten/Bremsen, Ausrichtung, Landen auf den drei Pad-Ausrichtungen, Schiffwechsel, Aus-/Einsteigen, Laufwege, Betankung und Rücksetzen nach einem Zusammenstoß. Kamerabild insbesondere im Hochformat und mit dem gedrehten Frachter prüfen.
