# Frachter nach Nutzerskizze

Direkte Überarbeitung von Reference.png: Bug/Kommandosektion oben, drei Frachtmodule entlang der aufrechten Längsachse, Haupttriebwerk unten, breite Heckverkleidung und zwei Landefüße. Perspektive und Grundanordnung der Skizze wurden beibehalten. Proportionen, Kopplungsabstände, Rahmen und Düsen sind vereinheitlicht; das Bugteil wurde verkürzt.

Freighter.png ist die neue Imagegen-Fassung. Freighter.prompt.txt enthält den exakten Prompt. Schwarzer Hintergrund, weiße Konturen; noch nicht in der Testszene oder den Prefabs ersetzt. Vorherige Varianten bleiben erhalten.

## Überarbeitete Proportionen und Größenvergleich

`FreighterRefined.png` hat breitere Fußplatten und kräftigere Streben, ein größeres Haupttriebwerk mit Bodenfreiheit, größere klare Steuerdüsen, vereinfachte Containerrahmen und ein einzelnes Cockpitfenster im Bugteil. `FreighterRefined.prompt.txt` enthält den Imagegen-Prompt.

Die editierbare, rein statische Unity-Szene `Assets/Scenes/FreighterScalePreview.unity` zeigt ihn zusammen mit dem vorhandenen Astronauten, dem Original-Lander 01 und dem Original-Testpad. Der Frachter ist auf 14 Unity-Einheiten Höhe gesetzt; seine Breite beträgt 7,62. Der Astronaut ist etwa 1,03 Einheiten hoch; das Pad ist 22 breit. Damit ist der Frachter etwa 13,6 Astronautenhöhen hoch. Diese Skalierung gilt als Vorschlag für den großen Frachter, nicht als Vorgabe für alle Schiffstypen.

`ScaleGameView.png` zeigt den Vergleich bei Kameragröße 22 wie in der Flug-Testszene. `ScaleDetail.png` zeigt ihn näher bei Kameragröße 10,5. Die tatsächlichen Messwerte stehen in `ScaleMeasurements.txt`. Referenzfiguren wurden mit ihren Boden-Collidern auf dem Pad platziert; transparente Sprite-Ränder bestimmen nicht die Bodenhöhe.

Das importierte Grafikmaster und Sprite-Asset liegen unter `Assets/Images/Spacecraft/SketchBased`. Der schwarze Bildhintergrund wird in der Vorschau mit dem bestehenden Outline-Material ausgeblendet. Die Grafik wurde noch nicht im fliegbaren Frachter-Prefab ersetzt. Unity-Kompilierung und statische Editor-Renderings sind erfolgt, keine Play-Mode- oder Physiktests.
