# Gemeinsames 2D-Charakter-Rig

`BipedRig.prefab` enthält das gemeinsame Gelenkrig. `AstronautVisual.prefab` ist eine Prefab Variant davon. Das bestehende `Astronaut.prefab` verwendet diese Variante als `VisualRoot`; Rigidbody, Collider und Spielsteuerung bleiben am übergeordneten Astronauten.

Die Figur wird aus starren Sprites animiert. Dadurch bleiben die weißen Konturen auch an den Gelenken klar. Vollständig gezeichnete, überlappende Teile und Gelenkabdeckungen verdecken die Übergänge. Die entfernten Glieder sind leicht abgedunkelt.

## Weitere Charaktere

1. Eine Prefab Variant von `BipedRig.prefab` erstellen.
2. Die Sprites unter den `Art`-Objekten ersetzen. Dort können auch Grafikmaßstab, Position und Ausrichtung angepasst werden.
3. Gelenknamen, Hierarchie und Gelenkabstände beibehalten. Die Sprite-Pivots müssen zu den jeweiligen Gelenken passen. So bleiben die gemeinsamen AnimationClips verwendbar.
4. Die neue Variante als visuelles Kind des jeweiligen Spielcharakters einsetzen. Dessen Steuerung ruft `CharacterVisual.SetWalking(walking, worldSpeed, onMoon)` und `FaceLeft(left)` auf. `onMoon` kann für normale Gehbewegungen weggelassen werden.

Der gemeinsame Aufbau ist:

```text
VisualRoot
  Facing                         <- nur hier wird links/rechts gespiegelt
    Rig                          <- gemeinsamer Animator
      Hips
        Spine
          Head
          Backpack
          ArmFar / Forearm / Hand
          ArmNear / Forearm / Hand
        LegFar / Shin / Foot
        LegNear / Shin / Foot
```

Die Animationen binden an die Pfade unter `Rig`. `Art` sowie die Gelenkabdeckungen `Elbow` und `Knee` sind visuelle Kinder. Ihr Austausch verändert die Bindungen der Animationen nicht. Ganz andere Körperproportionen brauchen angepasste Gelenkabstände und passende Clips.

## Animationen und Vorschau

- `BipedIdle.anim`: 2,4 Sekunden, ruhige Atmung und leichte Bewegung von Kopf und Armen.
- `BipedWalk.anim`: 0,96 Sekunden, Fersenaufsatz, Abrollen und Abstoß mit festen Kontaktpunkten. Der Fuß wird in der Schwungphase nach vorne geführt; Hüfte und Schultern bewegen sich leicht gegeneinander, Unterarme, Hände und Rucksack folgen etwas verzögert. Die Beinwinkel werden beim Erstellen berechnet und als normale Transform-Kurven gespeichert.
- `BipedMoonHop.anim`: 2,4 Sekunden für zwei wechselseitige Schritte. Jedes Bein übernimmt abwechselnd Bodenkontakt, Einfedern und Abstoß; zwischen den Schritten liegt eine kleine Schwebephase. Die Arme schwingen gegenläufig. Das federnde Gehen entsteht in der visuellen Rig-Animation. Der jeweilige Standfuß bewegt sich während des Bodenkontakts mit derselben Referenzgeschwindigkeit wie beim normalen Lauf.
- `Biped.controller`: gemeinsame Zustände `Idle`, `Walk` und `MoonHop`, Parameter `IsWalking`, `WalkSpeed` und `IsOnMoon`.
- `CharacterVisual`: spiegelt die ganze Figur und passt das Animationstempo an die Bewegungsgeschwindigkeit und den Maßstab des Charakters an. Die Referenz beträgt zwei lokale Einheiten pro Sekunde.
- `AstronautMoonController`: wählt den Mondgang anhand der bestehenden Stations-Schwerkrafterkennung aus; Station und Innenräume verwenden den normalen Lauf. `Moon Speed Multiplier` im Astronaut-Prefab steht auf 0,75: auf dem Mond bewegt sich die Figur dadurch 25 Prozent langsamer, und das Animationstempo folgt dieser Geschwindigkeit. `MoonJumpHeight` im Generator beträgt 0,35 lokale Einheiten.
- `Assets/Scenes/CharacterPreview.unity`: im Editor öffnen und Play drücken; links Idle, in der Mitte Stationslauf nach rechts, rechts Mondgang nach links. Am `CharacterAnimationPreview`-Component können Richtung und Mondgang umgestellt werden. Die Szene ist nicht in den Spiel-Build aufgenommen.

Die Assets sind im Unity-Editor direkt bearbeitbar. `Tools > Characters > Export Animation Preview Frames` rendert die aktuellen Clips nach `Temp/CharacterPreview`. `Update Animations and Export Preview` erzeugt alle drei Clips und den gemeinsamen Animator erneut und rendert sie; die Clips erhalten dieselben Bindungen, damit Gelenke beim Wechsel der Gangart sauber zurückgesetzt werden. `Rebuild Astronaut Rig and Preview` erzeugt zusätzlich die Grafikaufteilung, das Basis-Prefab und die Astronautenvariante erneut; manuelle Änderungen an diesen erzeugten Assets werden dabei überschrieben. Die ursprüngliche Astronauten-Spritegrafik und deren alte Animationen bleiben für bestehende Stationsbewohner erhalten.

Für einen separaten Unity-Batch-Prozess kann `CHARACTER_PREVIEW_OUTPUT` einen eigenen Ausgabeordner festlegen. So bleiben die Renderbilder auch nach dem automatischen Aufräumen des Unity-Temp-Ordners erhalten.

## Stationsbewohner

Neun unterschiedliche Designs verwenden denselben Animator und dieselbe Gelenkhierarchie: Techniker, Wissenschaftlerin, Pilot, Sicherheitskraft, Operatorin, Reisender sowie Grey, Reptilian und Insectoid. Die drei Aliens haben jeweils zwei Arme und zwei Beine und unterscheiden sich durch Kopf, Hände, Füße und Körperdetails. Die visuellen Prefab Variants liegen unter `Assets/Prefabs/Characters/Station`; die vollständigen Bewohner-Varianten unter `Assets/Prefabs/Station01/Residents`.

Die vorhandenen Instanzen in `SpaceStation.prefab` und `StationInterior.prefab` wurden ersetzt. `MainScene` verwendet diese Stations-Prefabs und übernimmt die neuen Figuren. Die bisherigen Bewohner-IDs, Laufbereiche, Dialoge und das Verhalten des Stationsführers bleiben erhalten. Die Zuordnung lautet: ID 1 Pilot, ID 2 Techniker, ID 3 Sicherheitskraft, ID 4 Operatorin, ID 5 Wissenschaftlerin, ID 6 Reisender, ID 7 Operatorin an der Registrierung.

`StationResident` steuert Blickrichtung und Lauf über `CharacterVisual`. Bodenhöhe und Sprechblasenposition richten sich nach den Bounds der gesamten Figur. Der Stationslauf passt sein Tempo an die tatsächliche Bewegung an.

Zusätzliche Instanzen: ID 8 Grey auf dem Stationsdeck, ID 9 Reptilian und ID 10 Insectoid im Innenraum. Sie patrouillieren ohne neue Dialoge oder Story-Aktionen. Ihre unterschiedlichen IDs werden vom bestehenden Speichersystem erfasst. Der Generator ergänzt diese Instanzen nur, wenn die jeweilige ID noch fehlt.

Die Einzelteilgrafiken und Imagegen-Prompts liegen unter `Assets/Images/Characters/Station`. `Characters.layout.json` enthält die Sprite-Rechtecke, Gelenkpunkte und Grafikmaße. `StationCharactersPreview.unity` zeigt alle neun Designs in drei Reihen mit gemeinsamer Laufanimation. `Tools > Characters > Build Station Characters and Preview` erzeugt die Varianten; `Export Station Character Preview` rendert die vollständigen Bewohner-Prefabs. Die Renderausgabe kann für Batch-Aufrufe über `STATION_CHARACTER_PREVIEW_OUTPUT` festgelegt werden.
