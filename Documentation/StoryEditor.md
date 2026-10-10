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

Die Übernahme erfasst die Storytexte aus der aktuellen Hauptszene, Registrierung, Rhekk-Mission, Funk und dem Story-Code. Sie ergänzt vorhandene Projekte, ohne bearbeitete Schritte neu zu erzeugen. Die 14 Ablaufknoten sind eine Entwicklungsübersicht, keine automatisch ausgeführten Missionen. Die drei früher separat angelegten Zukunftskonzepte wurden mit den passenden Ablaufknoten zusammengeführt; ihre Notizen bleiben erhalten. Aktuell liegen 109 Story-/Textnodes und 25 gemeinsame Auslöser vor; 56 Texte sind als Ereignismeldungen gekennzeichnet. Status: Idea, Planned, Implemented, Verified; importierte Inhalte sind Implemented, nicht automatisch Verified.

## Ereignismeldungen und Training

Im Textboard trennt der Filter **Alle / Dialoge / Ereignisse** die Inhalte. Ereignismeldungen erscheinen als orange **Auslöserkarte → Textkarte**. Sie haben keine künstliche Fortsetzung zu anderen Trainingskommentaren. Auslöserdaten werden aus der Hauptszene und dem vorhandenen `StoryTextController` eingelesen; sie sind als Entwicklungsansicht schreibgeschützt. Texte und Audio bleiben bearbeitbar. **Quellen ergänzen** aktualisiert diese Metadaten, ohne den Laufzeitablauf zu ersetzen.

Auslöser- und Textkarte besitzen unabhängige Layoutpositionen. Jede ist einzeln verschiebbar; bei gemeinsamer Auswahl bewegen sich beide genau einmal. Die Verbindung bleibt erhalten und heftet sich automatisch an die passenden Seiten. Verschieben ist rückgängig machbar und verändert keine Spielauslöser.

- Beim Trainingsstart wird die höchste passende Levelgruppe gewählt. Deren Texte sind Alternativen, keine Abfolge.
- Die festen Übertragungen bei Level **3, 6, 9** erscheinen beim Erreichen des Levels, nicht nach dessen Abschluss. Sie haben Vorrang, erscheinen nur vor der ersten Schwerelosigkeitsentdeckung und werden nach Bestätigung gespeichert.
- Ein bereits bekannter Mond ersetzt gewöhnliche Startkommentare. Levelübertragungen und Level 10+ haben weiterhin Vorrang.
- Ab Level 10 gibt es einen Zufallsversuch je neuem Level, mit der eingestellten Wahrscheinlichkeit und ohne direkte Wiederholung des letzten Kommentars.
- Entdeckungen sind einmalige Meldungen; unbestätigte Anzeigen können nach Unterbrechung wiederkehren. Die Anzeigeform (z. B. Overlay, Zeitlupe, Landeergebnis) bleibt erkennbar.
- Flugkommentare gehören zu Atmosphärenausflug, Erdrückkehr oder Mondnähe. Im Spiel gelten Startverzögerung, Cooldown, Chance und maximal ein Kommentar je Flugversuch; nach der Level-9-Verabschiedung bleiben diese Routinekommentare aus.

Das Trainingsboard ist nach Leveln geordnet: Level 1, Level 2, feste Meldung 3, Level 4, Level 5, feste Meldung 6, Level 7, Level 8, feste Meldung 9, danach Level 10+. Zufallsvarianten desselben Bereichs stehen nebeneinander. Unabhängige Entdeckungs- und Flugmeldungen stehen in einem separaten Bereich darunter; ihre Position bedeutet nicht, dass sie erst nach Level 10 ausgelöst werden. Die angezeigten normalen Levelbereiche schließen die festen Übertragungslevel aus. Diese Anordnung erzeugt keine neuen Dialogverbindungen und ändert keine Spielauslöser.

In **Vorschau → Ereignis auslösen** Trainingslevel und Ereignis wählen. Die Liste zeigt alle aktuell passenden Textvarianten. **Ereignis auslösen** berücksichtigt Chance, Einmaligkeit und simulierte Flags; ein ausgelöster Entdeckungsmoment setzt seinen simulierten Entdeckungsflag, Weiter bestätigt ihn. Bei fehlenden Voraussetzungen oder verfehlter Zufallschance wird der Grund angezeigt. **Simulation zurücksetzen** setzt auch Zufallsversuche und Wiederholungsmerker zurück. Physische Nähe, Flugphasen und echtes Anzeigetiming werden nicht simuliert. Die Auswahl **Einzeltext / Gespräch** erlaubt weiterhin, einen konkreten Text unabhängig vom Auslöser anzusehen.

Die **Storyübersicht** bildet die Hauptgeschichte einschließlich geplanter späterer Abschnitte ab. Hier Reihenfolge, Zweck, offene Fragen und Entwicklungsstatus festhalten und die zugehörigen Gespräche öffnen. Storykapitel gliedern Handlungsschritte; sie sind keine Dialogsammlungen. Eine zusätzliche Gruppe Spätere Geschichte ist für dieselben Schritte nicht mehr nötig.

Die Storyübersicht ist inzwischen in **Training** (Landetraining und Aufbruch ins All), **Mond** (Entdeckung/Landung, Aussteigen, verlassenes Schiff) und **Ablauf** (Station, Rhekk und weitere geplante Schritte) gegliedert. Die Verbindungen zwischen den Kapiteln bleiben erhalten, auch wenn das Board nur das gewählte Kapitel zeigt. Gesprächsgruppen bleiben davon getrennt.

Auch die bisherige Gesprächsgruppe Training und Entdeckungen ist getrennt: **Training** enthält Trainingsstimme und Aufbruch, **Mond** Mondentdeckung, EVA, Schiff und Mondkommentare. Die drei Stations-/Kontaktmeldungen stehen unter **Funk**. Quellen ergänzen erhält diese Aufteilung.

Im Gesprächsboard gibt es **Textkarten und Antwortkarten**. Jede Antwort bleibt an ihren Text gebunden und besitzt einen separat wählbaren Folgetext. Mehrere Antworten dürfen auf denselben Text zusammenlaufen oder in verschiedene Zweige führen. Eine Antwortkarte auswählen, rechts **Folgetext** ändern und Antworttext beziehungsweise Bedingungen bearbeiten. Beide Kartentypen sind verschiebbar; Kartenpositionen und Verbindungen verwenden weiterhin dieselben Dialogdaten wie das Spiel. Bestehende Layouts wurden einmalig auseinandergezogen, um Platz für die Antwortkarten zu schaffen.

Verbindungen wählen automatisch das nächstgelegene Paar aus den vier Kartenseiten. Karten untereinander verbinden sich entsprechend oben/unten; seitlich angeordnete Karten seitlich. Die Kurventangenten folgen den gewählten Seiten. Die Charakteranimation wird über den Editor-Update-Takt mit bis zu 60 Neuzeichnungen pro Sekunde aktualisiert; Sampling findet nur beim Rendern statt. Damit hängt sie nicht mehr am langsamen Inspector-Refresh.

Jede Karte hat mittig an allen vier Seiten einen kleinen Verbindungspunkt. Mit links von einem Punkt auf eine passende Zielkarte oder deren Punkt ziehen: Die Vorschauleitung wird bei einem gültigen Ziel grün, beim Loslassen werden die Daten übernommen. Escape oder Loslassen auf leerer Fläche bricht ab. Undo macht die Änderung rückgängig.

- Storyschritt → Storyschritt setzt den Folgeschritt; bei mehreren bestehenden Verbindungen wird ein zusätzlicher Zweig angelegt.
- Antwort → Text setzt den Folgetext dieser Antwort. Eine geänderte Zielverbindung wird im Graphen verfolgt statt sofort an den bestehenden Controller zurückzugeben (`continueLegacy = false`).
- Text → Text nutzt die einzige vorhandene Antwort oder ergänzt eine Weiter-Antwort, wenn es keine eindeutige einzelne gibt.
- Text → Antwort ordnet eine bestehende Antwort diesem Text zu; ihre Position und ihr Folgetext bleiben erhalten. Eine bereits zugeordnete Antwort bleibt unverändert.
- Antwort → Antwort ist ungültig. Dialogtexte unterstützen maximal sechs Antworten; zusätzliche Zuweisungen werden abgelehnt.

Der angewählte Ausgangspunkt ist der Start der Drag-Vorschau. Nach dem Verbinden richten sich die dauerhaften Anheftungen weiterhin automatisch nach den nächstgelegenen Kartenseiten.

Das Flag-Feld in der Vorschau fügt einen booleschen Testzustand hinzu (z. B. `story.registrationComplete`). Mit dem Kontrollkästchen seinen Wert setzen; Dialogbedingungen lesen diese simulierten Werte. **Simulation zurücksetzen** löscht Flags, Gesprächszustand und Verlauf der Vorschau und stoppt Audio; Spielstände bleiben unverändert.

## Gemeinsame Auslöser und Boardgestaltung

Gleiche eingelesene Auslöser sind zu **25 gemeinsamen Auslöserkarten** zusammengeführt. Beispielsweise hat Level 1 einen Auslöser mit drei verbundenen Textvarianten. Eine Zufallsauswahl wählt daraus einen Text; es entsteht keine Abfolge aus drei Meldungen. Auslöser und Texte bleiben unabhängig verschiebbar.

Vom Verbindungspunkt eines Auslösers auf einen Text ziehen weist den Text diesem Auslöser zu. Ein Text hat genau einen Ereignisauslöser; mehrere Texte können denselben Auslöser teilen. Bei festen Meldungen wird der bisherige Text ersetzt. Im Detailbereich stehen die verbundenen Texte, ein Öffnen-Button und **Lösen**. Manuelle Zuordnungen bleiben bei erneuter Quellenübernahme erhalten. Die Trainingsauswahl und vorhandenen Ereignispools im Spiel lesen diese Verbindungen; eigene Textbedingungen werden berücksichtigt.

**Auslöser hinzufügen** erstellt einen eigenen Trainingsstart-Auslöser für Level 1–9. Levelbereich und Zufallsauswahl sind bearbeitbar. Es gilt die höchste passende Levelgrenze, bei Gleichstand der zuletzt angelegte passende Auslöser; feste Übertragungen in 3/6/9 sowie die Sonderregeln für bekannten Mond und Level 10+ behalten Vorrang. Andere eingelesene Auslöserbedingungen stammen weiterhin aus dem Spielsystem und bleiben schreibgeschützt.

**Titel hinzufügen** legt ein frei platzierbares Entwicklungslabel an. Selektieren und rechts Text, Schriftgröße, Textfläche und Farbe einstellen; der Eckgriff verändert die Textfläche. **Linie hinzufügen** legt eine gerade Orientierungslinie an. Selektiert lassen sich **Vollstrich/Gestrichelt**, Stärke, Farbe und Endpunkte bearbeiten. Ziehgriffe verändern ihre Endpunkte. Labels und Linien sind keine Missionen oder Dialoge; sie werden pro Ansicht und Kapitel/Gruppe gespeichert, unterstützen Mehrfachauswahl und Undo.

Navigation funktioniert mit gedrückter **rechter oder mittlerer Maustaste**. Das Mausrad ohne gedrückte Taste zoomt. Alle Boardelemente lassen sich gemeinsam markieren und verschieben.

Die Gesprächsboards wurden neu angeordnet: gemeinsame Auslöser links, verbundene Textvarianten rechts. Normale Dialoge werden nach ihren Verbindungen mit Antworten zwischen den Textkarten angeordnet. Labels und Orientierungslinien bleiben bei der automatischen Anordnung an ihren bisherigen Positionen. **Gesprächsboard anordnen** wiederholt dies für die aktuelle Gruppe; Undo stellt die alte Anordnung wieder her.

Unter **Raster** lassen sich Anzeige und Einrasten getrennt aktivieren. **Rasterweite** ist von 5 bis 200 Boardeinheiten einstellbar und wird mit dem Projekt gespeichert. Aktivieren des Einrastens richtet vorhandene Elemente aus; auch dies ist rückgängig machbar. Karten, Antwortkarten, Auslöser, Labels sowie Linienendpunkte rasten beim Verschieben ein. Kleine Mausbewegungen werden gesammelt, damit das Ziehen nicht am Raster hängen bleibt. **Auswahl am Raster ausrichten** richtet nur markierte Elemente aus. Bei starkem Herauszoomen zeigt das Board weniger Rasterlinien, während die gewählte Rasterweite fürs Einrasten erhalten bleibt.

**Auswahl entfernen** oder **Entf** (außerhalb eines Texteingabefelds) löscht die ganze markierte Auswahl: Story-/Textkarten, Antworten, Auslöser, Labels und Linien können gemischt ausgewählt sein. Gruppenlöschung bildet einen Undo-Schritt. Referenzen auf entfernte Storyschritte, Folgetexte oder Auslöser werden aufgeräumt; unmarkierte Gespräche bleiben erhalten. Strg+Z stellt auch diese Verbindungen wieder her. Das Löschen entfernt keine Sprachkatalogeinträge, Charakter-Prefabs oder Quelldaten der ursprünglichen Controller; Quellen ergänzen kann fehlende importierte Inhalte erneut übernehmen.

Storykapitel und Gesprächsgruppen sind getrennte Listen im Projekt. Bestehende Inhalte werden nach ihrer bisherigen Funktion getrennt; IDs, Texte, Verbindungen und Positionen bleiben erhalten. Die übernommenen Gesprächszeilen bekommen eine anfängliche Zuordnung zum passenden Ablaufknoten. Diese Zuordnung ist im Editor bearbeitbar und ersetzt keine Gameplay-Trigger.

DE und EN werden nebeneinander bearbeitet. **DE/EN-Text übernehmen** schreibt direkt die vorhandenen Sprachkataloge; Texte werden nicht im Board dupliziert. **Speichern** übernimmt zusätzlich alle offenen Textentwürfe und sichert die Story- und Charakter-Assets. Beim Schließen ohne Speichern bleiben nicht übernommene Entwürfe ungesichert. IDs und Quellenbindungen stabil halten.

**Rückgängig/Wiederholen** sind in der Toolbar verfügbar; alternativ Ctrl+Z sowie Ctrl+Y beziehungsweise Ctrl+Shift+Z. Kartenverschiebungen bilden je Drag einen Undo-Schritt. Offene Textentwürfe werden beim Toolbar-Undo als eigener rücknehmbarer Textschritt behandelt. In einem fokussierten Textfeld bleiben dessen normalen Tastenkürzel aktiv.

**Rechte oder mittlere Maustaste halten und ziehen** navigiert im Board. **Links auf leerer Fläche ziehen** spannt ein Auswahlrechteck auf; überlappende Text- und Antwortkarten werden markiert. Strg/Shift ergänzt die Auswahl, Strg-Klick auf eine markierte Karte entfernt sie. Linker Drag auf einer markierten Karte verschiebt die ganze Auswahl als einen Undo-Schritt. Antworten folgen ihrem Text; gemeinsam markierte Antworten werden dabei nicht doppelt verschoben. Escape bricht das Auswahlrechteck ab. Rechts stehen die Eigenschaften der aktiven Karte, keine automatische Massenbearbeitung.

Das Mausrad zoomt um den Mauszeiger; Drag-Navigation, Auswahlrechteck und Kartenbewegung berücksichtigen die Zoomstufe. **Inhalte fokussieren** setzt die Suche zurück und zentriert alle Nodes des aktuellen Kapitels mit passender Zoomstufe. Es gibt keine feste Canvas-Grenze, auch negative Koordinaten sind möglich. Der Inhaltsbereich wird aus den Karten des aktuellen Kapitels mit Rand berechnet und wächst mit ihnen.

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
