# Schwarz-Weiß-Outline-Grafik

Die Außenwelt bleibt dauerhaft bei schwarzen Flächen und weißen Konturen. Es gibt keine farbige Entwicklungsstufe und kein zusätzliches Stilwechsel-System. Bereits vorhandene Stationsinnenräume dürfen wie bisher etwas mehr Raum- und Ausstattungsdetails haben.

## Aktive Assets

- **Außenposten:** drei unterschiedlich hohe Gebäude mit erkennbaren Türen, Tankanlage, Vordach, klaren weißen Konturen und schwarzen Innenflächen. Vorder- und Hintergrund bleiben getrennt bearbeitbar.
- **Bestehende Schiffe:** die sieben ursprünglichen Lander-Prefabs einschließlich Original-Sprites und Original-Collider sind wiederhergestellt. Die Schiffsauswahl und Szeneninstanzen verwenden diese Grafiken.
- **Zusätzliche Schiffe:** Courier, Surveyor, CargoTug, Interceptor und RescueShuttle sind neu im gleichen sparsamen Outline-Stil gezeichnet. Sie bleiben vorbereitete Vorlagen für spätere Spielabschnitte.
- **Figuren:** ServiceBot, WorkBot, FoxCourier und BovineMechanic wurden als schwarze Körperteile mit weißen Konturen neu erzeugt. Das gemeinsame Biped-Rig und die vorhandenen Animationen bleiben bestehen. Rhekk und seine Transportkapsel verwenden ebenfalls Schwarz-Weiß.
- **Stationen:** WayfarerTradeStation, KeplerResearchStation und AtlasIndustrialStation behalten 2, 3 beziehungsweise 4 Etagen mit Außenwegen, Türen und Landeplätzen. Die Bauteile sind jetzt wieder Outline-Grafiken. Ihre spätere Stations- und Türlogik ist weiterhin anzubinden.

## Mond als Geometrie

Für einen sehr großen, stark herangezoomten Outline-Mond ist direkt gerenderte Geometrie die passende Lösung. Eine höhere PNG-Auflösung würde lediglich den Punkt verschieben, an dem Randpixel sichtbar werden. Der Mond besteht jetzt aus einem schwarzen Mesh und weißen Konturen/Kraterringen mit `LineRenderer`. Diese Geometrie wird im Editor erzeugt, als Assets gespeichert und im Spiel direkt gerendert. Es gibt keine unscharfe Oberflächentextur und keine SVG-Importer-Abhängigkeit.

Die begehbare Außenkontur verwendet exakt den vorhandenen Mond-PolygonCollider. Position und Kollisionsform bleiben erhalten. Die Grafik liegt in `Assets/Prefabs/World/MoonSurfaceArt.prefab`; Meshes und Materialien liegen unter `Assets/Images/World/Geometry` und `Assets/Materials/World`.

## Eigene Asteroidenformen

Unter `Assets/Prefabs/World/Asteroids` liegen sechs verschiedene kleine Asteroiden und ein eigener großer Außenposten-Asteroid:

- AsteroidChunky: unregelmäßiger kompakter Brocken.
- AsteroidAngular: kantiger Fels.
- AsteroidLong: von Anfang an länglich gestaltete Kontur.
- AsteroidNotched: eingekerbter Brocken.
- AsteroidTwin: zweiteilige Form mit schmalerer Mitte.
- AsteroidRounded: rundlicher, welliger Brocken.
- OutpostAsteroid: breite, eigenständig gezeichnete Form für den Außenposten.

Jede Form besitzt ihr eigenes schwarzes Mesh, ihre weiße Kontur und einen dazu passenden PolygonCollider. Die zwölf kleinen Feldinstanzen variieren Form, Drehung und Gesamtgröße. X, Y und Z werden jeweils gleich skaliert; keine Form wird durch unterschiedliche Achsenskalierung gequetscht. Der längliche Brocken und der breite Hauptasteroid erhalten ihre Proportionen bereits aus ihren Konturpunkten.

## Dateien und Bearbeitung

Aktive Bildquellen mit den genauen Imagegen-Prompts als benachbarte `.prompt.txt`:

- `Assets/Images/World/ArchitectureOutline.png`
- `Assets/Images/Spacecraft/FleetOutline.png`
- `Assets/Images/Characters/Expansion/Outline/ServiceBotParts.png`
- `Assets/Images/Characters/Expansion/Outline/WorkBotParts.png`
- `Assets/Images/Characters/Expansion/Outline/FoxCourierParts.png`
- `Assets/Images/Characters/Expansion/Outline/BovineMechanicParts.png`

Diese sechs Rasterquellen wurden mit dem eingebauten Imagegen-Werkzeug erzeugt. Mond und Asteroiden sind native Unity-Geometrie. Die früheren farbigen Versuche sind nicht mehr in den aktiven Prefabs verwendet.

Architekturmodule liegen in `Assets/Prefabs/World/Modules`; Stationen unter `Assets/Prefabs/World/Stations`; weitere Schiffe unter `Assets/Prefabs/Spacecraft`; die neuen Bewohner unter `Assets/Prefabs/Characters/Expansion`. Die zugehörigen `.layout.json`-Dateien definieren Sprite-Ausschnitte und Gelenkpunkte. `OutlineWorldAuthoring.cs` enthält die Konturpunkte der Asteroiden und die Mondkrater; die resultierenden Meshes, Collider und Linien sind auch direkt im Editor bearbeitbar.

`Tools > Art > Build Space Art Expansion` erzeugt die Outline-Assets erneut. Manuelle Änderungen an erzeugten Prefabs vorher sichern. `Assets/Scenes/SpaceArtGallery.unity` zeigt die Assets und gehört nicht zu den Spiel-Build-Szenen.

Vorschauen liegen unter `Documentation`: CargoOutpost, CargoAsteroidField, AsteroidVariants, StationExpansion, FleetExpansion, CharacterExpansion, MoonSurface und MoonSurfaceClose.

Unity-Kompilierung und statische Render-Vorschauen wurden in einer isolierten Projektkopie geprüft, da der Haupteditor bereits geöffnet war. Keine Spiel- oder automatischen Testläufe wurden gestartet. Die neuen Asteroiden-Collider und die Mondansicht sollten anschließend im Spiel geprüft werden. Mobile benötigt einen neuen Build.
