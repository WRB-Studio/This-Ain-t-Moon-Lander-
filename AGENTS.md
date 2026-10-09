# Projektspezifische Regeln

## Git und Branches

- Änderungen immer auf einem separaten Arbeitsbranch umsetzen, niemals direkt auf `main`. Vor der ersten Änderung einen passenden Arbeitsbranch mit dem Präfix `codex/` erstellen oder einen zur Aufgabe gehörenden Arbeitsbranch verwenden.
- Änderungen nur auf ausdrückliche Anweisung des Nutzers nach `origin/main` übernehmen und pushen. Direkt auf `main` arbeiten ist nur erlaubt, wenn der Nutzer für die konkrete Aufgabe ausdrücklich eine Ausnahme anordnet.

## Tests

- Tests nur ausführen, wenn der Nutzer dies ausdrücklich anfordert. Keine automatischen Testläufe nach Änderungen starten.
- Wenn Tests für eine Änderung besonders wichtig sind, darf gelegentlich kurz darauf hingewiesen werden. Ein Hinweis oder ausbleibende Antwort ist keine Erlaubnis, Tests auszuführen.

## Spieltexte und Gespräche

- Verbindliche Gestaltungsregel: Alle Spieltexte und Gespräche kurz, klar und auf Mobile schnell erfassbar halten. Gilt für Deutsch und Englisch sowie mit oder ohne Vertonung.
- Pro Dialogschritt möglichst ein bis zwei kurze Sätze mit einer wesentlichen Aussage. Auch das gesamte Gespräch kurz halten; lange Monologe nicht bloß auf viele Dialogseiten verteilen.
- Nur nötige Informationen vermitteln, Wiederholungen vermeiden und Sarkasmus beziehungsweise Humor knapp einsetzen. Zusätzliche Hintergrundinformationen freiwillig und in kurzen Antworten anbieten.
- Bestehende Dialoge sind zur späteren Kürzung vorgemerkt, insbesondere Rhekk. Bei neuen oder überarbeiteten Spieltexten diese Regel anwenden; keine globale Textüberarbeitung ohne entsprechenden Auftrag starten.

## UI und Prefabs

- UI-Struktur, Layout und Gestaltung möglichst in Szenen oder Prefabs anlegen und pflegen, damit sie im Unity-Editor direkt bearbeitet werden können. UI nur dann zur Laufzeit erzeugen oder strukturell beziehungsweise gestalterisch per Code verändern, wenn das gewünschte Verhalten es erfordert. Dynamische Inhalte und Zustände wie Texte, Fortschrittsanzeigen, Sichtbarkeit und Interaktivität dürfen weiterhin per Code aktualisiert werden.
- Wiederkehrende Elemente wie Buttons, Items und andere mehrfach verwendete UI- oder Spielobjekte als gemeinsame Prefabs anlegen und wiederverwenden. Bestehende passende Prefabs bevorzugen; Varianten bei Bedarf als Prefab Variants umsetzen.
