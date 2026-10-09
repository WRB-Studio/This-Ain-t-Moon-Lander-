# Story-Editor und Charakterverwaltung

Öffnen: **Tools > Story > Story-Editor**. Das Projekt liegt unter `Assets/Resources/Story/StoryProject.asset`, Charaktere unter `Assets/Resources/Story/Characters`.

Das Tools-Menü wurde bereinigt: einmalige Aufbau- und Export-Einträge sind entfernt. Ihre Generatoren bleiben für interne Wiederherstellung und bestehende Code-Abhängigkeiten erhalten. Die Storyübernahme ist direkt im Story-Editor erreichbar; ein zweiter Menüeintrag ist nicht nötig. Der temporäre UI-Vorschau-Exporter wurde entfernt, seine fertigen Layoutbilder bleiben in `Documentation/StoryEditor`.

## Bereiche

- **Storyübersicht:** Nur Handlungsschritte, organisiert in Storykapiteln. Das Kapitel Ablauf zeigt den bisherigen Gesamtverlauf und vereinbarte spätere Ziele. Der Detailbereich enthält Folgeschritte und zugeordnete Gespräche; ein Klick öffnet die betreffende Gesprächszeile.
- **Gespräche:** Nur Dialoge, Funk und Kommentare, organisiert in Gesprächsgruppen. Jede Zeile kann einem Storyschritt zugeordnet werden; der Detailbereich bietet auch den Rücksprung zur Storyübersicht. Graue Verbindungen gehören zur bestehenden Missionsfortsetzung; neue Verzweigungen werden farbig dargestellt.
- **Charaktere:** Charaktere zentral erstellen, feste IDs und Namensschlüssel vergeben, Prefab und Porträt-Sprite zuweisen. Rechts ist das tatsächliche Prefab sichtbar. Alle Clips seiner Animator-Controller sind auswählbar, abspielbar und über einen Zeitregler betrachtbar. Dafür wird eine isolierte Vorschauinstanz verwendet, das Original-Prefab bleibt unverändert.
- **Vorschau:** Direkt einen Starttext wählen und **Gespräch starten** drücken, anschließend Antworten durchspielen, Sprache wechseln und optional Audio hören. Ohne zugewiesene Aufnahmen wird das ausdrücklich angezeigt; Audio stoppen startet kein Gespräch. Simulierte Flags lassen sich setzen und zurücksetzen. Es werden keine echten Spielstände oder Missionsaktionen ausgeführt.
- **Prüfung:** Fehlende Übersetzungen, Sprecher, Prefabs, Porträts, Ziele, Kapitel, Flag-IDs und ungültige ursprüngliche Antwortindizes sowie zu lange Texte werden angezeigt. Fehlende optionale Sprachaufnahmen verhindern keine Gespräche.

## Daten und Bearbeitung

Die Übernahme erfasst die Storytexte aus der aktuellen Hauptszene, Registrierung, Rhekk-Mission, Funk und dem Story-Code. Sie ergänzt vorhandene Projekte, ohne bearbeitete Schritte neu zu erzeugen. Die 14 Ablaufknoten sind eine Entwicklungsübersicht, keine automatisch ausgeführten Missionen. Die drei früher separat angelegten Zukunftskonzepte wurden mit den passenden Ablaufknoten zusammengeführt; ihre Notizen bleiben erhalten. Damit liegen aktuell 108 Nodes vor. Status: Idea, Planned, Implemented, Verified; importierte Inhalte sind Implemented, nicht automatisch Verified.

**Ablauf** ist die gesamte Hauptgeschichte einschließlich geplanter späterer Abschnitte. Hier Reihenfolge, Zweck, offene Fragen und Entwicklungsstatus festhalten und die zugehörigen Gespräche öffnen. Weitere Storykapitel sind optional für tatsächlich getrennte Abschnitte; sie sind keine Dialogsammlungen. Eine zusätzliche Gruppe Spätere Geschichte ist für dieselben Schritte nicht mehr nötig.

Im Gesprächsboard gibt es **Textkarten und Antwortkarten**. Jede Antwort bleibt an ihren Text gebunden und besitzt einen separat wählbaren Folgetext. Mehrere Antworten dürfen auf denselben Text zusammenlaufen oder in verschiedene Zweige führen. Eine Antwortkarte auswählen, rechts **Folgetext** ändern und Antworttext beziehungsweise Bedingungen bearbeiten. Beide Kartentypen sind verschiebbar; Kartenpositionen und Verbindungen verwenden weiterhin dieselben Dialogdaten wie das Spiel. Bestehende Layouts wurden einmalig auseinandergezogen, um Platz für die Antwortkarten zu schaffen.

Verbindungen wählen automatisch das nächstgelegene Paar aus den vier Kartenseiten. Karten untereinander verbinden sich entsprechend oben/unten; seitlich angeordnete Karten seitlich. Die Kurventangenten folgen den gewählten Seiten. Die Charakteranimation wird über den Editor-Update-Takt mit bis zu 60 Neuzeichnungen pro Sekunde aktualisiert; Sampling findet nur beim Rendern statt. Damit hängt sie nicht mehr am langsamen Inspector-Refresh.

Das Flag-Feld in der Vorschau fügt einen booleschen Testzustand hinzu (z. B. `story.registrationComplete`). Mit dem Kontrollkästchen seinen Wert setzen; Dialogbedingungen lesen diese simulierten Werte. **Simulation zurücksetzen** löscht Flags, Gesprächszustand und Verlauf der Vorschau und stoppt Audio; Spielstände bleiben unverändert.

Storykapitel und Gesprächsgruppen sind getrennte Listen im Projekt. Bestehende Inhalte werden nach ihrer bisherigen Funktion getrennt; IDs, Texte, Verbindungen und Positionen bleiben erhalten. Die übernommenen Gesprächszeilen bekommen eine anfängliche Zuordnung zum passenden Ablaufknoten. Diese Zuordnung ist im Editor bearbeitbar und ersetzt keine Gameplay-Trigger.

DE und EN werden nebeneinander bearbeitet. **DE/EN-Text übernehmen** schreibt direkt die vorhandenen Sprachkataloge; Texte werden nicht im Board dupliziert. **Speichern** übernimmt zusätzlich alle offenen Textentwürfe und sichert die Story- und Charakter-Assets. Beim Schließen ohne Speichern bleiben nicht übernommene Entwürfe ungesichert. IDs und Quellenbindungen stabil halten.

**Rückgängig/Wiederholen** sind in der Toolbar verfügbar; alternativ Ctrl+Z sowie Ctrl+Y beziehungsweise Ctrl+Shift+Z. Kartenverschiebungen bilden je Drag einen Undo-Schritt. Offene Textentwürfe werden beim Toolbar-Undo als eigener rücknehmbarer Textschritt behandelt. In einem fokussierten Textfeld bleiben dessen normalen Tastenkürzel aktiv.

Das Board wird mit der linken Maustaste auf leerer Fläche oder mit mittlerer/rechter Maustaste überall verschoben. Linker Drag auf einer Karte verschiebt den Storyschritt. Das Mausrad zoomt um den Mauszeiger; Drag-Navigation und Kartenbewegung berücksichtigen die Zoomstufe. **Inhalte fokussieren** setzt die Suche zurück und zentriert alle Nodes des aktuellen Kapitels mit passender Zoomstufe. Es gibt keine feste Canvas-Grenze, auch negative Koordinaten sind möglich. Der Inhaltsbereich wird aus den Karten des aktuellen Kapitels mit Rand berechnet und wächst mit ihnen.

Die Seitenleisten sind über die schmalen Trennlinien zum Board in der Breite verstellbar. Kapitelübersicht, Kapitelname und Storyaktionen sind mit Abstand gruppiert. Beschriftungen für Texteingaben stehen über dem Feld, damit dessen ganze Breite nutzbar ist; DE/EN-Schreibfelder haben zusätzlichen Innenabstand, Zeilenumbruch und mindestens 110 Pixel Höhe. Navigationshilfe ist einklappbar.

Kapitel über das Namensfeld und **Kapitel umbenennen** ändern. Zugehörige Schritte werden mitgeführt; die Quellenübernahme berücksichtigt den neuen Namen auch für später ergänzte Inhalte. **Quellen ergänzen** (vorher Übernahme ergänzen) liest Spielquellen erneut und fügt fehlende Schritte hinzu. Vorhandene Texte und bearbeitete Schritte bleiben erhalten; die UI-Prefabs werden dabei nicht erneut aufgebaut.

**Inhalte: Deutsch/English** wählt ausschließlich die Sprache der Spielinhalte: automatisch aus Spieltexten abgeleitete Kartentitel, Charakternamen, Dialoge, Antworten und die passende Sprachaufnahme in der Vorschau. Die Editoroberfläche bleibt deutsch. Der Schalter verändert keine Spiel-Sprachpräferenz. Eigene Kapitel- und Entwicklungstitel werden nicht automatisch übersetzt; beide Textspalten bleiben sichtbar.

Ein Schritt enthält Zweck, offene Fragen, Ort, Asset-/Quellenverweise, Sprecher, Gesprächsart, Audio und Flags. Persönliche Gespräche und Funk mit Porträt verwenden das zugewiesene Charakterbild; reiner Funk zeigt kein Porträt. Die Gesprächsvorschau ist eine funktionale Editorvorschau, kein pixelgenaues Abbild des Mobile-HUDs.

Für jede Zeile lassen sich `voiceDE`, `voiceEN` und `sound` zuweisen. Wiedergabe erfolgt im Editor ohne Play Mode. Sprache ist optional, Texte bleiben vollständig nutzbar. Dialog- und Funkansicht spielen zugewiesene Clips auch im Spiel ab. Noch keine Sprachaufnahmen oder neu gezeichneten Porträts erzeugt.

## Entscheidungen und bestehende Missionsaktionen

Antworten besitzen Sprachschlüssel, optional einen Reaktionsschlüssel, ein Ziel sowie Bedingungen und Flagänderungen. Bedingungen prüfen boolesche Storyflags; Änderungen setzen sie wahr oder falsch. Damit sind spätere Reaktionen und verfügbare Gesprächsschritte steuerbar. Neue gameplayseitige Aufgaben benötigen weiterhin einen passenden Trigger, der diese Flags prüft; das Board führt keine beliebigen Scripts oder Weltaktionen aus.

Bei übernommenen Gesprächen ist **continueLegacy** aktiviert: Eine Antwort übergibt ihren `legacyAnswer`-Index an den bestehenden Controller. Registrierung, Funkvergabe, Missionsfortschritt und Rhekks Weg bleiben dessen Aufgabe. Das sichtbare Ziel zeigt, wohin dieser Ablauf normalerweise führt; die Editorvorschau folgt dem Ziel ohne echte Missionsaktionen.

Für einen zusätzlichen Dialogzweig die betreffende Antwort auf einen neuen Schritt verweisen lassen und **continueLegacy** dort deaktivieren. Der ursprüngliche Antwortindex wird bei der ersten Auswahl gespeichert. Am Ende des zusätzlichen Zweigs eine Antwort mit **continueLegacy** aktivieren, um den ursprünglichen Missionsschritt fortzusetzen. So können leichte Unterschiede entstehen und wieder zusammenlaufen. Ein direktes Springen zwischen übernommenen Missionsseiten ersetzt nicht deren Gameplay-Aktionen.

Eigene Gespräche können über `StoryDialog.ShowStory(nodeId, completed)` gestartet werden. Der aufrufende Spielcontroller ist für Interaktionsbedingungen, Gameplay-Pause und Wiederaufnahme zuständig. Neue Gesprächsbedingungen werden geprüft; bestehende Missions-Trigger bleiben erhalten. Quellengebundene Varianten werden über die originale Textreferenz gefunden. Ein neuer Schritt ohne explizite Antworten erhält eine Weiter-Option.

Die vorhandenen Dialog- und Funk-Prefabs enthalten sechs bearbeitbare Antwortplätze auf Basis des gemeinsamen Button-Prefabs. Mehr als sechs Antworten erfordern eine bewusste UI-Erweiterung und werden als Hinweis gemeldet. Funkzweige speichern ihre aktuelle Schritt-ID und ursprüngliche Antwort im vorhandenen Nachrichten-Spielstand; persönliche Gespräche nutzen weiterhin die vorhandene Missionsfortsetzung beim Wiederöffnen.

## Prüfung und offene Abnahme

Unity-Kompilierung und Asset-Übernahme im isolierten Editor-Projekt durchgeführt. Alle bisherigen DE/EN-Texte wurden unverändert erhalten; nur Charakter-Namen ergänzt. Keine Tests oder Play-Mode-Läufe gestartet.

Im Editor noch manuell abnehmen: Board-Navigation und Undo/Redo, Textübernahme beider Sprachen, Charakterzuweisung, Vorschau mit Bedingungen/Flags und echten Audioclips. Im Spiel: Registrierung, Rhekk-Rettung, neue Entscheidungszweige, Funk-Speichern/Laden, Porträt- und Audiowiedergabe sowie Mobile-Layout. Bestehende lange Dialoge sind nur markiert, nicht gekürzt.
