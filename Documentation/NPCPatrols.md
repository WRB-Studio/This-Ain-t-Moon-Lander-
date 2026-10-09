# NPC-Patrouillen

Im `StationResident`-Inspector stehen zwei Modi bereit:

- **MinMaxX:** Zwischen `Min X` und `Max X` laufen. Die Koordinaten beziehen sich auf das Elternobjekt. `Initial Floor Y` setzt nur die anfängliche Fußhöhe; danach übernimmt die Physik.
- **Waypoints:** Leere GameObjects auf Bodenhöhe platzieren und in Laufreihenfolge im Feld `Waypoints` zuweisen. `PingPong` läuft hin und zurück, `Loop` kehrt vom letzten zum ersten Punkt zurück. Die Punkte außerhalb des NPCs platzieren, damit sie nicht mitlaufen.

`Walk Speed`, `Pause Duration` und `Arrival Distance` regeln Tempo, zufällige Pausen und die Ankunftstoleranz. Wegpunkte vor und nach Treppen setzen. Es gibt keine automatische Wegfindung und keine Sprünge: Alle Strecken müssen über begehbare Böden und Treppen verbunden sein.

`NPCMotor2D` benutzt einen dynamischen Rigidbody und einen CapsuleCollider. Die Gravitation stammt aus `GravityManager2D`; ohne diesen gilt die einstellbare Ersatzgravitation. Auf Treppen folgt die Bewegung der glatten Collider-Steigung. Die Laufanimation startet nur bei Bodenkontakt.

Der Layer **NPC** kollidiert ausschließlich mit **NPCGround**. Spieler, andere NPCs, Schiffe und Gegenstände blockieren NPCs nicht. Tragende Böden, Mondoberfläche, Landepads und Treppen sind als NPCGround eingerichtet. Neue begehbare Flächen ebenfalls diesem Layer zuweisen; Wände und Dekoration behalten ihre bisherigen Layer. NPCGround behält die bestehenden Kontakte mit anderen Layern.

Vorhandene Bewohner verwenden weiterhin MinMaxX. Neue Speicherstände behalten Position, Geschwindigkeit, Patrouillenrichtung, Pausen und Wegpunktindex; alte Speicherstände werden weiterhin eingelesen.

Rhekk nutzt denselben Physikbaustein für seinen Missionsweg. In ausgeblendeten Stationsräumen ohne aktive Boden-Collider läuft seine Raumdurchquerung logisch weiter; beim Raumwechsel wird er auf den jeweiligen Boden gesetzt.

`Assets/Scenes/NPCPatrolPreview.unity` enthält beide Patrouillenarten auf getrennten Treppenstrecken. Sie gehört nicht zu den Build-Szenen. `Tools > Characters > Configure NPC Physics and Patrols` richtet die vorhandenen Prefabs und die Hauptszene erneut ein und erstellt diese Vorschau. Nach einer erneuten Generierung von Charakteren oder Weltbauteilen diesen Schritt wiederholen.

Die Skripte wurden im Unity-Editor kompiliert und die Assets gespeichert. Play-Mode-Tests wurden nicht ausgeführt. Manuell prüfen: Auf-/Absteigen, Fallen an Kanten, Pausen/Umkehr, Wegpunktroute, Speichern/Laden und Durchlaufen von Spieler/anderen NPCs.
