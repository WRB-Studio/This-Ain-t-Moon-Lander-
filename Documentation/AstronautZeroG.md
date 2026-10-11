# Astronaut in Schwerelosigkeit

Der Astronaut nutzt `GravityManager2D.GetGravity` statt einer unbegrenzt wirkenden Zugkraft zum Mondzentrum. Damit gelten Erd-, Mond-, Stations- und Außenpostenfelder samt ihren Übergängen. Beschleunigung wird mit der Körpermasse angewendet; die globale Rigidbody-Gravitation bleibt deaktiviert.

Unter `Zero G Threshold` (standardmäßig 0,05) treiben Körper und Blickrichtung unverändert weiter. Touch/Maus erzeugen dort weder Laufen noch Antrieb. Arm- und Beinbewegungen sind rein visuell. Auch innerhalb eines Gravitationsfelds ist Laufen nur bei Bodenkontakt erlaubt; in der Luft bleibt der vorhandene Schwung erhalten. Der Bodenfühler prüft entlang der lokalen Schwerkraft und akzeptiert geneigte Böden.

`BipedFloat.anim` enthält eine langsame Vier-Sekunden-Schleife für Arme, Hände, Beine und leichte Körperbewegung. Der gemeinsame `Biped.controller` wechselt mit `IsFloating` weich hinein und zurück. Andere Figuren nutzen weiterhin ihre bisherigen Zustände; die Charakter-Animationspflege erhält den neuen Zustand bei einer Neuerstellung.

Freies Treiben löst eine sichtbare Warnung mit Countdown aus. Standardmäßig nach fünf Sekunden: Game Over „Im All verloren“. Die Anzeige bleibt stehen; erst „Erneut versuchen“ lädt den letzten sicheren Punkt. Gravitation setzt den Countdown zurück; Pause, Funk und Dialoge unterbrechen ihn. `Zero G Survival Time` am Astronauten ist einstellbar. Die rote Warnung hat feste Umbrüche, 8 % Seitenabstand und automatische Schriftgrößenanpassung. Das Schiff wird dabei nicht zerstört.

EVA-Speicherpunkte benötigen jetzt Bodenkontakt und Gravitation. Freies Treiben überschreibt keinen sicheren Punkt. Alte EVA-Speicherstände im Zero-G werden beim Rücksprung verworfen; ohne sicheren Punkt startet die bisherige Runden-Rücksetzung. Es gibt keinen automatischen Rücksprung.

Kompilierung und native Clip-/Controller-Erstellung geprüft. Keine Tests oder Play-Mode-Läufe ausgeführt. Noch im Spiel prüfen: Dock verlassen, freies Treiben bei Touch-Eingabe, Rückkehr in ein Gravitationsfeld, Landung/Bodenkontakt, stationäre und geneigte Wege sowie Speichern/Laden in Zero-G.
