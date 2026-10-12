# Manuelles Storyboard

Öffnen: **Tools > Story > Story-Editor**.

Dieses Board ist ausschließlich Planung und Schreiben. **Story speichern hat keine Wirkung auf das Spiel.** Neue Storykarten, Dialoge, Reihenfolgen, Antworten oder Missionen werden erst nach einem ausdrücklichen Umsetzungsauftrag gezielt in Code und Spielassets übernommen. Es gibt keinen Import, Rückexport oder automatischen Sync.

## Daten klar getrennt

- **Planungsboard:** `Assets/StoryPlanning/StoryPlan.asset` – Unity ScriptableObject/YAML, im Editor und durch Codex direkt bearbeitbar. Texte und deutsche/englische Entwürfe liegen als `planningTexts` in diesem Asset.
- **Planungscharaktere:** `Assets/StoryPlanning/Characters/` – unabhängige Kopien. Namen, Porträt-/Prefabzuweisung und neue Figuren im Charaktertab ändern ausschließlich Planungsdaten. Prefabs werden in einer isolierten Vorschauinstanz angezeigt, nicht bearbeitet.
- **Bestehendes Spiel:** `Assets/Resources/Story/StoryProject.asset`, seine ursprünglichen Charakterassets und `Assets/Resources/Localization/de.json` / `en.json` bleiben die bisherigen Laufzeitquellen. Das Spiel lädt das Planungsboard nicht.

Bei der Entkopplung wurden die vorhandenen Storytexte, Antworten, IDs, Missionen, Gesprächszuordnungen und Notizen in den Planungsbestand kopiert. Frühere Auslöser-/Kontextkarten sind jetzt voll bearbeitbare manuelle **Storykarten**. Ihre bisherigen Beschreibungen wurden ohne Verlust als Planungstext übernommen; ursprüngliche Kontextangaben bleiben zusätzlich im gespeicherten Bestand erhalten. Graue Kontextverbindungen ordnen Notizen zu Texten; sie lösen nichts aus. Historische Quellenverweise bleiben schreibgeschützte Herkunftsinformationen. Die Spielauslöser und Ereignisregeln wurden nicht entfernt oder geändert.

## Schreiben und organisieren

**Kapitel → Missionen → Gespräche → Texte und Antworten** bleibt die Organisation; sichtbar sind die Tabs **Storyboard** und **Charaktere**.

- Links genau zwei Button-Ebenen: Kapitel und eingerückte Missionen. Kapitelbutton klappt Missionsbuttons auf/zu; Missionbutton zeigt sofort alle zugehörigen Karten im mittleren Raster. Gesprächsmetadaten bleiben intern erhalten; es gibt keinen zusätzlichen Gesprächsfilter.
- **Kapitel hinzufügen**, **Mission hinzufügen** stehen über der Hierarchie.
- Das Missionspanel zeigt nur das feste Kapitel und den **Missionsnamen** als DE/EN-Eingabefeld. Sprache wird ausschließlich oben gewechselt. Inhalt/Ziel, offene Fragen, Entwicklungsstand und weitere technische Details sind dort ausgeblendet; vorhandene Daten bleiben erhalten. Kapitelwechsel/Reihenfolge erfolgen weiterhin per Drag/drop links. **Mission entfernen** steht als hoher roter Button unten; bei zugehörigen Inhalten ist er gesperrt (Hinweis im Tooltip).
- Kapitel und Missionen am Griff oder Button ziehen. Blaue Linie: davor/dahinter einsortieren. Mission auf Kapitelbutton ziehen: in dieses Kapitel verschieben. Ein Drop klappt nicht versehentlich die Hierarchie um. Undo macht die Verschiebung rückgängig.
- Storykarte wählen: rechts **Kartentitel → Charakter über das Such-Dropdown wählen → Storytext → Audio → Antwort hinzufügen → Storykarte entfernen**. Das Charakterfeld öffnet eine Dropdownliste mit direkt fokussierter Suche: tippen, Treffer wählen; Pfeiltasten/Enter wählen, Escape schließt. Kein separates Suchfeld im Panel. Charaktere stammen nur aus den Planungscharakteren; „Kein Charakter“ ist möglich.
- Der mittig platzierte **DE/EN-Toggle oben** schaltet Kartentitel, Text und Audio gemeinsam um. Im rechten Panel gibt es keine separaten Sprachbuttons. Titel und Texte werden je Sprache unabhängig gespeichert; bestehende Titel wurden als deutscher Ausgangstext erhalten, englische Titel können separat ergänzt werden.
- Über der Textfläche steht **Storytext**. Leere Flächen zeigen einen erklärenden Platzhalter; er ist kein Inhalt und wird nicht gespeichert. Die Textfläche am unteren Griff ziehen (Mindesthöhe vorhanden); längere Texte scrollen innerhalb der Fläche. Beide Sprachentwürfe werden unabhängig gespeichert.
- **Antwortkarte** steht rechts neben **Storykarte**. Antwortkarten frei ins Grid ziehen oder per Klick erstellen, ohne Auswahl, Dialog oder automatische Zuordnung. Danach einen Verbindungspunkt der Antwortkarte mit einer Storykarte verbinden (oder umgekehrt); rechts **Antwort hinzufügen** verwendet denselben Erstellungsweg. Die Antwort bleibt bis zur manuellen Verbindung selbstständig; beim Verbinden wird sie der Storykarte zugeordnet, maximal sechs verbundene Antworten pro Storykarte. Die Palette benötigt keine ausgewählte oder bereits vorhandene Storykarte. Inhalt und Position freier Antwortkarten sind speicherbar und per Undo bearbeitbar. **Danach** an einer ausgewählten Antwort wählt Folgetext oder Gesprächsende.
- Bereits gespeicherte Gesprächsart und Kontext bleiben interne Planungsmetadaten; sie führen keine Spielaktionen aus.
- **Werkzeuge** ist dauerhaft sichtbar, gegliedert in **Storyelemente** und **Raster**. **Storykarte**, **Label** und **Linie** am sichtbaren Griff ins Grid ziehen oder zum Hinzufügen klicken. Ein blauer Zielrahmen und Positionskreuz markieren einen gültigen Drop. Der Drop-Punkt wird mit Pan/Zoom in Weltkoordinaten umgerechnet und bei aktivem Raster eingerastet. Storykarten gehören zur gewählten Mission. Jede Erstellung bleibt per Undo rückgängig machbar und betrifft nur Planung.
- **Fokussieren** zeigt alle Inhalte des aktuellen Boards. Es ändert nur Zoom und Ansicht, keine Karten-, Antwort-, Notiz-, Label- oder Linienpositionen und keine Verbindungen. Die automatische Anordnung ist entfernt. Es gibt keine Auslöserbearbeitung.
- Karten ziehen, Strg/Shift für Mehrfachauswahl; leere Fläche ziehen für Auswahlrechteck. Rechts/Mausrad gedrückt ziehen: Board bewegen. Mausrad: Zoom. **Fokussieren** rahmt das aktuelle Board ein.
- Suche findet Namen, Kontext, Sprecher sowie DE/EN-Texte und Antworten. Texte kurz halten: ein bis zwei kurze Sätze mit einer Aussage.

## Speichern und Rückgängig

**Story speichern** übernimmt offene Texte und speichert ausschließlich Planungsboard und ausgewählten Planungscharakter. Auch beim Fokusverlust oder Schließen werden Entwürfe übernommen. Strg+Z / Strg+Y und Unity-Undo bleiben erhalten; die Toolbar-Schaltflächen dafür sind ausgeblendet. Vorschau und Prüfung sind ebenfalls vorerst nicht sichtbar.

Beim Entfernen einer Storykarte bleiben ihre Antwortkarten mit Inhalt und bisheriger Weltposition als freie Karten erhalten. Auch vorherige/nachfolgende Antwortkarten bleiben bestehen; nur Verbindungen zur entfernten Storykarte werden gelöst. Antwortkarten lassen sich separat entfernen. Gespräch entfernen löscht seine Planungsnodes; vorhandene Planungstext-Einträge bleiben erhalten. Kapitel/Missionen erst entfernen, wenn ihre Inhalte verschoben oder entfernt wurden. Keine dieser Aktionen löscht oder verändert Spielinhalte.

## Zusammenarbeit und Umsetzung

Der Nutzer schreibt und plant im Board; Codex bearbeitet denselben YAML-Bestand nach Vorgaben. Ein späterer Auftrag wie **„Schau aufs Storyboard und implementiere diese Mission“** ist ein separater Umsetzungsschritt. Erst dabei werden nötige Dialoge, Lokalisierung und Spielabläufe gezielt angepasst. Das Board ist kein ausführbares Missionssystem.

Statisch geprüft werden Daten-/Referenztrennung, Syntax und Diff. Manuell in Unity noch prüfen: Planungstext ändern und speichern, Charaktername ändern, Undo/Redo, Notizen verbinden, Drag/drop und erneutes Öffnen. Die ursprünglichen Laufzeitdateien sollten dabei unverändert bleiben. Keine Tests oder Builds für die Entkopplung gestartet.

## Kapitelansicht und Missionsrahmen

- Kapitel zeigen Missionsrahmen und reine Kapitelkarten. Story-/Antwortkarten lassen sich direkt dort erstellen.
- Rahmen tragen ihren Titel links darüber und wachsen mit ihren zugeordneten Karten, Antworten und Layoutobjekten. Vorhandene Kartenpositionen werden nicht automatisch umgeordnet.
- In einen Rahmen ablegen: Mission zuordnen. Außerhalb ablegen: nur Kapitel zuordnen. In der direkten Missionsansicht behalten verschobene/neue Karten ihre Missionszuordnung.
- Dropbereiche werden vor dem Verschieben festgehalten. Automatisches Wachsen nimmt keine fremden Karten auf. Bei überlappenden Rahmen zählt der kleinste Treffbereich; die Zielmission wird hervorgehoben. Undo stellt Position und Zuordnung gemeinsam wieder her.
- **Mission hinzufügen** erzeugt eine Mission ohne Kapitel. Sie steht links unter **Nicht zugeordnete Missionen**. In ein Kapitel ziehen zum Zuordnen; auf die Bereichsüberschrift ziehen zum Lösen. Leere Missionen bekommen beim Zuordnen einen freien Rahmen-Startplatz, ohne bestehende Karten zu verschieben.
- **Gespräch hinzufügen** und Gesprächsfilter entfallen. Vorhandene Gesprächsmetadaten und Originalzuordnungen bleiben erhalten. Neue Zuordnungen sind eigenständige Planungsfelder; das Spiel nutzt diese nicht.

Manuell prüfen: Drop im/außerhalb eines Rahmens, Karten zwischen Mission und Kapitel verschieben, Rahmenwachstum mit Antworten, Undo, unzugeordnete Mission zuordnen/lösen, speichern und erneut öffnen. Keine Tests oder Builds gestartet.
