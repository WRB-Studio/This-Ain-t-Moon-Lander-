# Kommunikationspanel-Vergleich

## Testszene

`Assets/Scenes/CommunicationPanelGallery.unity` öffnen und Play starten. Vier Spalten zeigen Gespräch, Funkgerät, Kurztext und Ergebnis. Ihre Varianten stehen jeweils darunter, mit 180 UI-Einheiten horizontalem und 280 UI-Einheiten vertikalem Abstand. Jeder Eintrag hat einen eigenen, dünnen Rahmen. Im Scene View `Panel variants / Grouped comparison / Four panel groups` auswählen und mit F einrahmen. In Play horizontal/vertikal ziehen oder mit dem Mausrad scrollen. Die Galerie zeigt nur die Panelansichten und ihre Gruppen-/Variantenüberschriften; Galerie-Steuerleiste, Sprachschalter und aktuelle Auswahl sind ausgeblendet. Beispielinhalte stehen auf Deutsch.

Alle Ansichten sind als bestehende Prefab-Instanzen in der Szene angelegt. Ihre Spielcontroller und `LocalizedText`-Komponenten sind nur in diesen Instanzen entfernt, damit die Galerie ohne Hauptszene, Radio-Besitz, Mission und Spielstand nutzbar ist. Der Sprachwechsel schreibt keine globalen Spracheinstellungen. Die Szene wird nicht zu den Build-Szenen hinzugefügt. Die statische Referenzwelt mit Schiff, Figur und weißen Linien hilft, Lesbarkeit und Sicht auf die Umgebung zu vergleichen. Die Sprechblase wird für den direkten Spaltenvergleich im Canvas gezeigt; im Spiel bleibt sie weiterhin ein World-Space-Element.

## Aktuelle Varianten

| Ansicht | Derzeitiger Einsatz | Vorhandene Basis | Zum Vergleichen |
| --- | --- | --- | --- |
| Einweisung / Storymeldung mit Weiter | Flugtrainer vor dem Countdown, Trainingshinweise und bestätigte Entdeckungen | `UI/TrainingBriefing.prefab` und `UI/StoryDialog.prefab` | Gemeinsames `DialogueContent`-Prefab für Sprecher, Text und Weiter; `TrainingBriefing` und `StoryDialog` bleiben getrennte äußere Prefabs mit eigenen Abläufen. |
| Persönliches Gespräch | Registrierung und Rhekk, mit Antwortauswahl | `UI/StoryDialog.prefab` | Nutzt bereits dieselbe Basis; Antwortbereich und optionales Porträt als Variante. |
| Funk / Porträt | Funkgerät mit Verlauf, Antworten und Missionszielen | `UI/Radio.prefab` | Gerätedarstellung mit eigenem Zweck. |
| Funk / nur Stimme | Gleiche Funkgerätansicht ohne sichtbare Person | `UI/Radio.prefab` | Variante desselben Radio-Prefabs. |
| Kurzer Nebentext | Flugkommentare und Entdeckungen, z. B. Mondflagge | `UI/SpaceFlightNavigation.prefab` | Kleine, nicht modale Anzeige bei etwa einem Drittel von oben. |
| NPC-Sprechblase | Kommentare beim Vorbeilaufen an Bewohnern | `UI/StationWorldText.prefab` | Weltposition über Kopf; getrennt von bildschirmfestem Nebentext. |
| Landung | Zustand und Aktionen wie Aussteigen | `UI/Panels/LandingPanel.prefab` | Gemeinsame ResultContent-Basis, eigene Aktionslogik. |
| Game over | Fehlschlag und Erneut versuchen | `UI/Panels/CrashPanel.prefab` | Gemeinsame ResultContent-Basis, kein Gespräch. |

Pfadangaben in der Tabelle sind relativ zu `Assets/Prefabs/`. Türen, Navigation, reine Aktionsbuttons und Starttitel sind kein Kommunikationsdialog und deshalb nicht als weitere Varianten enthalten. Station und Rhekk haben keine eigene Dialog-UI: beide verwenden StoryDialog. Die Funkporträts nutzen die vorhandene Station-Crew-Darstellung; beim persönlichen Gespräch fehlt derzeit das individuelle Porträt und ist in der Galerie entsprechend ausgeblendet.

## Gemeinsame Gestaltung im Spiel

Die Hauptszene nutzt jetzt drei gemeinsame Unter-Prefabs in `Assets/Prefabs/UI/Shared/`:

- **DialogueContent:** Einweisungen, Storymeldungen und persönliche Gespräche. Einweisung und Storymeldung mit „Weiter“ bilden einen Kommunikationstyp und nutzen dieselbe Ansicht. Persönliche Gespräche ergänzen darin die Antwortauswahl. TrainingBriefing und StoryDialog behalten ihre eigenen Ablaufcontroller. Die Welt bleibt zwischen Lesebereich und Aktionen sichtbar; der Hintergrund dimmt nur leicht.
- **ResultContent:** Landung und Game over. Einheitliche Überschrift, Text-/Scorebereiche und Aktionsbuttons. LandingPanel und CrashPanel behalten ihre jeweiligen Felder und ihre eigene Ergebnislogik, inklusive Auftanken, Aussteigen und erneut versuchen.
- **ShortCaption:** Nebentexte und NPC-Kommentare. Gemeinsame Schrift und kleine dunkle Textfläche mit dezentem Rand. Nebentexte bleiben bildschirmfest; NPC-Kommentare folgen weiterhin dem Kopf in der Spielwelt. Die alten Textobjekte im World-Text-Prefab bleiben für Türschild-Overrides erhalten.

**Radio** bleibt ein eigenes Geräte-Prefab. Sprecher-/Textstil und Aktionsbuttons sind angeglichen. Bis zu sechs Antworten liegen in zwei Spalten über einer getrennten Zielzeile; Missionsziel, Entfernung und Verfolgungsbutton haben eigene Bereiche. Mit und ohne Porträt bleiben Varianten derselben Geräteansicht. Storyzustände, Auswahlzweige, Audio, Sprachwechsel und Speichern werden weiterhin von den bisherigen Controllern gesteuert.

Gemeinsame Änderungen bevorzugt in den drei Shared-Prefabs vornehmen. Die bestehenden äußeren Prefabs bleiben die Einstiegspunkte für das Spiel. Die Galerie zeigt die aktualisierten Varianten in vier Gruppen. Einweisung und Storymeldung mit „Weiter“ erscheinen gemeinsam als eine Variante.

## Prüfstand

Unity-Kompilierung, native Prefab-/Szenenerstellung, Hauptszenen-Referenzen und statische Tutorial-/Gesprächs-/Funk-/Ergebnisvorschauen geprüft. Play Mode, Klickverhalten, Sprachwechsel und Hoch-/Querformat bitte in Unity prüfen; keine automatischen Tests ausgeführt.
