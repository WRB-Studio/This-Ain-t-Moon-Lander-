# Aktive neue Flotte nach der bestätigten Frachtervorlage

Die aktive Flotte wurde inzwischen stärker nach Rollen unterschieden und um den schnittigen Interceptor ergänzt. Aktueller Stand: [RoleDistinctFleet](../RoleDistinctFleet/README.md). Die vorherigen gleichförmigeren Bilder liegen unter PreviousUniform; die folgenden Größenangaben dokumentieren diese frühere Fassung.

Alle sechs neuen Testschiffe verwenden dieselbe aufrechte Längsachse: Bug und Kommandosektion oben, funktionale Module entlang des Rumpfs, Haupttriebwerk unten. Weiße kräftigere Außenkonturen, dünnere Innenlinien, schwarze Flächen. Der bestätigte Frachter wurde unverändert als Familienreferenz übernommen; die fünf anderen Rollen wurden mit Imagegen neu erzeugt. Die genauen Prompts liegen neben den Bildern; der Frachter-Prompt steht unter ../SketchBased/FreighterRefined.prompt.txt.

| Schiff | Höhe | Breite | Rolle |
| --- | ---: | ---: | --- |
| Courier | 7.5 | 4.98 | Kleine Lieferungen, Paketkabine |
| Scout | 9 | 3.55 | Schmaler Erkunder mit Sensor |
| CargoTug | 8.5 | 5.96 | Kräftiger Schlepper mit Seilöse |
| Freighter | 14 | 7.62 | Drei große Frachtmodule |
| Rescue | 10 | 5.60 | Geräumige Rettungskabine |
| Maintenance | 8.5 | 4.68 | Werkzeugkasten und seitlicher Greifarm |

Einheiten sind Unity-Welteinheiten. Der vorhandene Astronaut ist rund 1.03 Einheiten hoch. Alle Modelle passen auf die 22 Einheiten breiten Testpads. Zwei Fußplatten wurden für jedes Bild erkannt und die Fuß-Collider daran ausgerichtet; Bodenfreiheit von Düsen und Anbauteilen ist in den statischen Vorschauen sichtbar.

Die sechs Prefabs in Assets/Prefabs/Spacecraft/AssistedFlight und ihre Instanzen in SpacecraftFlightSandbox.unity wurden aktualisiert. Flugwerte wurden beibehalten. Sprite, schwarzes Rumpf-Mesh, Rumpf-Collider, Füße, Nasenrichtung (90 Grad = oben), Ausstiegsmarker und Triebwerksanzeigen sind angepasst. Die sieben klassischen Testschiffe behalten ihren echten LanderController. Die Hauptszene wurde nicht geändert.

Die Grafikmaster liegen unter Assets/Images/Spacecraft/ApprovedFleet. Der bestehende Outline-Shader blendet die schwarzen Bildhintergründe aus. Frühere Entwürfe bleiben erhalten.

Assets/Scenes/ApprovedFleetScalePreview.unity ist ein editierbarer statischer Größenvergleich mit sechs Original-Testpads und je einem Astronauten. ScaleOverview.png zeigt diesen Vergleich; Measurements.txt enthält die beim Import gemessenen Werte. Der interne Generator ApprovedFleetAuthoring.Build wendet die Grafiken erneut an und erzeugt den Vergleich. Der vollständige Flight-Sandbox-Generator berücksichtigt diese Flotte ebenfalls, sofern alle sechs Master vorhanden sind.

Unity-Kompilierung, Asset-Erstellung und statisches Editor-Rendering durchgeführt. Keine Play-Mode-Tests: Starten, Drehen, seitliches Fliegen, Landen und Aus-/Einsteigen mit den neuen Collidergrößen müssen noch manuell geprüft werden. Funktionale Rollenfähigkeiten (Scannen, Reparatur, neue Abschleppmechanik) sind weiterhin späterer Umfang.
