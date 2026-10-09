# Rhekk und die letzte Abholung

Die Registrierung schickt den Spieler zum letzten bestätigten Abholort. Der Tracker gehört zu einer verschlossenen Passagierkapsel. Am verlassenen Außenposten K-17 wartet darin Rhekk, ein eigenständiger wiederkehrender Charakter. Sein Transportpilot ging zu einer Anlage hinter dem Außenposten und kehrte nicht zurück. Das Schiff startete ohne ihn und wurde später auf dem Mond gefunden. Die Anlage und die Suche nach dem Piloten sind die spätere Fortsetzung und noch kein begehbares Missionsziel.

## Spielablauf

1. Registrierung abschließen und im Funk die letzte Abholung verfolgen. Das Expeditionsschiff wird vollgetankt und erhält ausreichend Tankkapazität für den längeren Flug.
2. Zum Außenposten fliegen. Kleine Asteroiden bilden mit großzügigen Zwischenräumen ein Hindernisfeld. Auf dem großen Asteroiden stehen drei geschlossene Gebäude, ein Landeplatz und eine weiterhin funktionierende Tankanlage.
3. Landen, bei Bedarf auftanken und aussteigen. Die Kapsel steht draußen unter einem offenen Vordach. Wiederkehrendes Klopfen und metallische Geräusche führen zu ihr.
4. Mit Rhekk reden. Danach sind REDEN und VERBINDEN verfügbar. VERBINDEN nimmt das freie Stahlseilende in die Hand; die Kapsel bleibt stehen. Zum Schiff gehen und FESTMACHEN drücken. Erst dann ist EINSTEIGEN wieder verfügbar.
5. Die Kapsel mit einem `DistanceJoint2D` physikalisch abschleppen. Das Seil überträgt Zug, lässt aber Spielraum und wird nach dem Start langsam auf seine Fluglänge eingezogen. Das Stationsziel wird als Funknachricht mit verfolgbarem Signal hinzugefügt.
6. Die Kapsel auf der markierten Ablage zwischen Stationseingang und rechtem Schiffspad langsam absetzen. Sie muss vollständig auf der Ablage stehen, annähernd aufrecht sein und kurz ruhig stehen. Dann löst sich das Seil automatisch.
7. Selbst landen, aussteigen und die Kapsel öffnen. Rhekk bedankt sich mit Beschwerden über den Flug und geht über den Stationsflur zur Registrierung. Dort bleibt er neben der Registrierung stehen. Die Geschichte endet vorerst hier.

Harte Zusammenstöße der geschleppten Kapsel oder Verlust des Schleppschiffs lassen die Mission scheitern. Die Kapsel wird zum Wrack. NEU VERSUCHEN setzt diese Mission an den Außenposten zurück; übriger Fortschritt bleibt erhalten. Ein Scheitern bleibt auch nach Laden erhalten.

## Bearbeitung im Unity-Editor

- `Assets/Prefabs/Missions/AsteroidOutpost.prefab`: Asteroiden, Gebäude, Deck, Tankanlage, Kapselstart und Abstand zur Station.
- `Assets/Prefabs/Missions/PassengerCapsule.prefab`: Kapsel, Seil, Klopfgeräusch, Masse, Seillänge sowie sichere und zerstörerische Geschwindigkeit.
- `Assets/Prefabs/Characters/Rhekk.prefab`: eigener Charakter auf dem gemeinsamen Biped-Rig, mit Reiseanzug, Schal und Kennzeichnung. Die gemeinsame Reptilian-Grafik dient als Grundlage; bestehende Stationsbewohner bleiben unverändert.
- `Assets/Prefabs/UI/CargoMissionHUD.prefab`: Missionsstatus und Aktionen, mit dem vorhandenen `ActionButton`-Prefab.
- `Cargo Rescue Mission` in `MainScene`: Dialogseiten, Zielreferenzen und Missionsparameter.
- `Assets/Resources/Localization/de.json` und `en.json`: alle Missionsdialoge, Statusmeldungen und Beschriftungen unter `cargo.*`.

Der interne Generator `CargoMissionAuthoring.Build` erstellt die Erstintegration. Bei vorhandener Mission verhindert der Generator eine doppelte Szeneninstanz. Danach die vorhandenen Prefabs und Szeneninstanzen direkt bearbeiten.

Spielstandversion 6 speichert Kapselposition, Geschwindigkeit, Drehung, Missionsphase, Seillänge, Schleppschiff und den Weg der Figur. Bestehende Spielstände ohne diese Daten beginnen die neue Mission am Außenposten. Die bisherigen Storyflags bleiben erhalten. Die abgeschlossene Rettung bleibt zusätzlich als Storyflag erhalten.

## Manuelle Abnahme

Ein regulärer Lauf von der Registrierung bis zur Rettung, Speichern/Laden während Seilaufnahme und Schleppflug, ein harter Kapselaufprall mit anschließendem Neustart sowie Bedienung in deutscher und englischer Sprache auf Editor und Mobile decken die wesentlichen Fälle ab. Automatische Gameplay-Tests wurden für diese Änderung nicht gestartet.
