# Gemeinsames 2D-Charakter-Rig

`BipedRig.prefab` enthält das gemeinsame Gelenkrig. `AstronautVisual.prefab` ist eine Prefab Variant davon. Das bestehende `Astronaut.prefab` verwendet diese Variante als `VisualRoot`; Rigidbody, Collider und Spielsteuerung bleiben am übergeordneten Astronauten.

Die Figur wird aus starren Sprites animiert. Dadurch bleiben die weißen Konturen auch an den Gelenken klar. Vollständig gezeichnete, überlappende Teile und Gelenkabdeckungen verdecken die Übergänge. Die entfernten Glieder sind leicht abgedunkelt.

## Weitere Charaktere

1. Eine Prefab Variant von `BipedRig.prefab` erstellen.
2. Die Sprites unter den `Art`-Objekten ersetzen. Dort können auch Grafikmaßstab, Position und Ausrichtung angepasst werden.
3. Gelenknamen, Hierarchie und Gelenkabstände beibehalten. Die Sprite-Pivots müssen zu den jeweiligen Gelenken passen. So bleiben die gemeinsamen AnimationClips verwendbar.
4. Die neue Variante als visuelles Kind des jeweiligen Spielcharakters einsetzen. Dessen Steuerung ruft `CharacterVisual.SetWalking(walking, worldSpeed)` und `FaceLeft(left)` auf.

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
- `Biped.controller`: gemeinsame Zustände `Idle` und `Walk`, Parameter `IsWalking` und `WalkSpeed`.
- `CharacterVisual`: spiegelt die ganze Figur und passt das Animationstempo an die Bewegungsgeschwindigkeit und den Maßstab des Charakters an. Die Referenz beträgt zwei lokale Einheiten pro Sekunde.
- `Assets/Scenes/CharacterPreview.unity`: im Editor öffnen und Play drücken; links Idle, in der Mitte Lauf nach rechts, rechts Lauf nach links. Die Szene ist nicht in den Spiel-Build aufgenommen.

Die Assets sind im Unity-Editor direkt bearbeitbar. `Tools > Characters > Export Animation Preview Frames` rendert die aktuellen Clips nach `Temp/CharacterPreview`. `Update Animations and Export Preview` erzeugt nur Idle und Walk erneut und rendert sie; beide Clips erhalten dieselben Bindungen, damit zusätzliche Schulterbewegungen beim Übergang zu Idle zurückgesetzt werden. `Rebuild Astronaut Rig and Preview` erzeugt zusätzlich die Grafikaufteilung, das Basis-Prefab und die Astronautenvariante erneut; manuelle Änderungen an diesen erzeugten Assets werden dabei überschrieben. Die ursprüngliche Astronauten-Spritegrafik und deren alte Animationen bleiben für bestehende Stationsbewohner erhalten.

Für einen separaten Unity-Batch-Prozess kann `CHARACTER_PREVIEW_OUTPUT` einen eigenen Ausgabeordner festlegen. So bleiben die Renderbilder auch nach dem automatischen Aufräumen des Unity-Temp-Ordners erhalten.
