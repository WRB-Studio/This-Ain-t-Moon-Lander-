# Unterschiedliche Rollen und Silhouetten

Die aktive neue Testflotte hat sieben Modelle. Der bestätigte modulare Frachter bleibt erhalten; die anderen Formen sind nach Aufgabe deutlich unterschiedlich. Weiße Konturen und schwarze Flächen, Nase oben, Landefüße unten.

- Courier: kompakter kantiger Lieferrumpf mit abgeschrägten Ecken, gerahmter Paketluke, kleinen Stabilisatoren und Wartungspanel.
- Scout: schlanker geschlossener Instrumentenrumpf mit sichtbarem Sensor und Sonde.
- CargoTug: breiter gedrungener Schlepper, große seitliche Triebwerke und verstärkte Seilöse.
- Freighter: unveränderte drei Frachtmodule, größtes Schiff.
- Rescue: geräumige gerade Rettungskabine mit abgeschrägten Schultern, großer Tür, Fenstern und Rettungssymbol.
- Maintenance: asymmetrischer geschlossener Servicerumpf mit Greifarm und integriertem Werkzeugkasten.
- Interceptor: zurückgeholte schnittige Pfeilform mit spitzer Nase und Deltaflügeln. Die Landebeine wurden so verlängert, dass das Triebwerk Bodenfreiheit hat.

PNG-Master und genaue Imagegen-Prompts stehen in diesem Ordner. Die bisherige einheitlichere Flotte liegt unter ../ApprovedFleet/PreviousUniform. Der Frachter-Prompt steht unter ../SketchBased/FreighterRefined.prompt.txt.

Die vier nachbearbeiteten Modelle verwenden die Prompts *.refined.prompt.txt (integriertes Imagegen-Werkzeug). Die vorigen runden beziehungsweise offenen Varianten sind unter PreviousRoundAndFrame archiviert. Collider, Sprites und Größenübersicht wurden neu erstellt; Flugwerte und Modellhöhen bleiben erhalten.

Die abschließenden Courier-Details stehen in Courier.detailed.prompt.txt. Seine schlichtere kantige Zwischenversion liegt unter PreviousRoundAndFrame/CourierAngularPlain.png. Die anderen sechs Modelle bleiben erhalten.

Alle Modelle sind in Assets/Prefabs/Spacecraft/AssistedFlight und SpacecraftFlightSandbox.unity aktualisiert. Es gibt 14 auswählbare Schiffe: sieben neue und sieben klassische. Interceptor steht auf Pad 07; die klassischen Schiffe bleiben auf Pads 11 bis 17 und nutzen weiter den echten LanderController. Die Gesamtzahl der Pads bleibt 19; drei normale Pads sind frei.

Die Flugwerte der sechs bisherigen neuen Modelle sind unverändert. Interceptor: Masse 0.7, Tempo 20, Beschleunigung 13, Bremse 18, seitlicher Schub 13, Drehgeschwindigkeit 240 Grad/s, Tank 110, Schubverbrauch 0.75/s. Er ist 6.5 Einheiten hoch. Die anderen Höhen bleiben 7.5 (Courier), 9 (Scout), 8.5 (CargoTug), 14 (Freighter), 10 (Rescue), 8.5 (Maintenance). Gemessene Breiten und Fußpunkte stehen in Measurements.txt.

Rumpf-Collider und schwarzes Rumpf-Mesh werden je Modell aus den sichtbaren Kontur-Extremen als vereinfachte konvexe Hülle erzeugt; die zwei Fuß-Collider orientieren sich an den tatsächlichen Fußplatten. Diese Collider lassen sich im Editor nachbearbeiten. Der Größenvergleich steht in Assets/Scenes/ApprovedFleetScalePreview.unity und ScaleOverview.png.

Kompilierung, Asset-Erstellung und statische Vorschau durchgeführt; keine Play-Mode-Tests. Flug, Landen, neue Collider und die zusätzliche Auswahl müssen noch manuell ausprobiert werden. Spezielle Rollenfähigkeiten sind noch nicht implementiert. Die normale Hauptszene bleibt unverändert.
