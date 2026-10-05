# Projektspezifische Regeln

## Tests

- Tests nur ausführen, wenn der Nutzer dies ausdrücklich anfordert. Keine automatischen Testläufe nach Änderungen starten.
- Wenn Tests für eine Änderung besonders wichtig sind, darf gelegentlich kurz darauf hingewiesen werden. Ein Hinweis oder ausbleibende Antwort ist keine Erlaubnis, Tests auszuführen.

## UI und Prefabs

- UI-Struktur, Layout und Gestaltung möglichst in Szenen oder Prefabs anlegen und pflegen, damit sie im Unity-Editor direkt bearbeitet werden können. UI nur dann zur Laufzeit erzeugen oder strukturell beziehungsweise gestalterisch per Code verändern, wenn das gewünschte Verhalten es erfordert. Dynamische Inhalte und Zustände wie Texte, Fortschrittsanzeigen, Sichtbarkeit und Interaktivität dürfen weiterhin per Code aktualisiert werden.
- Wiederkehrende Elemente wie Buttons, Items und andere mehrfach verwendete UI- oder Spielobjekte als gemeinsame Prefabs anlegen und wiederverwenden. Bestehende passende Prefabs bevorzugen; Varianten bei Bedarf als Prefab Variants umsetzen.
