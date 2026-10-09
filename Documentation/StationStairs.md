# Stationstreppen

Vier Bauformen im Schwarz-Weiß-Outline-Stil stehen jeweils nach links und rechts ansteigend bereit:

- `ExteriorLong`: lange offene Metalltreppe mit Geländer und Tragwerk.
- `ExteriorShort`: kürzere Außentreppe für Wartungsstege.
- `InteriorCompact`: kompakte Innentreppe ohne Geländer, mit geschlossener Seitenfläche.
- `InteriorService`: größere Innentreppe mit Geländer und geschlossener Seitenfläche.

Die acht Prefabs liegen unter `Assets/Prefabs/World/Stairs`. Der Root-Pivot ist der untere Einstieg auf die erste Stufe. `LowerConnection`, `UpperLandingStart` und `UpperConnection` markieren die Anschlusspositionen. Die linke Variante spiegelt Bild, Collider und Anschlussmarker gemeinsam.

Die Stufen sind grafisch sichtbar, der begehbare PolygonCollider läuft jedoch als glatte Steigung darunter und endet in einer waagerechten oberen Plattform. So vermeidet die EVA-Steuerung das Hängenbleiben an einzelnen Setzstufen. Ein `PlatformEffector2D` erlaubt den Eintritt von unten. Das tatsächliche Auf- und Abgehen wurde nicht im Play Mode geprüft.

Die Grafik liegt in `Assets/Images/World/Stairs/StationStairs.png`; `.layout.json` enthält Ausschnitte, Pivot und Treppenanschlüsse. Die genauen Imagegen-Prompts stehen in der benachbarten `.prompt.txt`. Die Transparenz bleibt erhalten, einschließlich der offenen Bereiche unter den Außentreppen.

Zum Platzieren ein Prefab in die Szene ziehen, seinen `LowerConnection` an den unteren Boden und `UpperConnection` an die obere Etage ausrichten. Für eine andere Gesamthöhe möglichst gleichmäßig skalieren. Renderer und Collider bleiben im Editor bearbeitbar. Die Treppen wurden als Bauteile angelegt und noch nicht automatisch in bestehende Stationen eingebaut.

`Tools > Art > Build Station Stairs` erzeugt die Prefabs erneut. `Assets/Scenes/StationStairsPreview.unity` zeigt die vier Bauformen; die Szene gehört nicht zu den Build-Szenen. Die statische Übersicht liegt in `Documentation/StationStairs.png`.
