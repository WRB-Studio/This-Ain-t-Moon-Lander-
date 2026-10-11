# Sichere Speicherpunkte und Fracht-Neustart

Welt-Speicherpunkte werden zentral in `GameController.CaptureWorld` begrenzt. Das gilt auch für Speichern beim Pausieren und Beenden sowie für Story- und Funkereignisse; diese behalten den letzten sicheren Weltzustand, wenn der Pilot gerade fliegt oder abstürzt. Einstellungen und sonstige Fortschrittsdaten werden weiterhin geschrieben.

- EVA: jederzeit beim Laufen beziehungsweise mit Bodenkontakt und Gravitation. Freies Treiben und fallende Astronauten überschreiben keinen sicheren Punkt.
- Landepad: bei bestätigter Landung sofort; gespeicherte Schiff-Geschwindigkeit und Drehgeschwindigkeit werden auf null gesetzt.
- Sonst: Schiff maximal 0,15 Einheiten/s und 5 Grad/s, mindestens 0,75 Sekunden lang ruhig. Ein geschleppter Container muss ebenfalls ruhig sein. Pausenzeit erzeugt keinen künstlichen Stillstand.
- Trainingsstart: Countdown als ruhiger Startpunkt.
- Absturz: überschreibt keinen sicheren Welt-Speicherpunkt. „Neu versuchen“ lädt den letzten sicheren Punkt; ohne gültigen älteren Punkt bleibt der bisherige Runden-Neustart als Rückfall erhalten.

Alle Grenzwerte und das Autosave-Intervall sind am `GameController` einstellbar. Ein neuer zulässiger Zustand löst unmittelbar einen Speicherpunkt aus. Während eines unsicheren Flugs wartet das Autosave auf den nächsten zulässigen Zustand, statt die Datei pro Frame erneut zu schreiben.

Bei zerstörter Fracht oder verlorenem Schleppschiff erscheint der Fehlschlag kurz (standardmäßig zwei Sekunden). Danach werden Seil, Trümmerzustand und Bewegung zurückgesetzt; die intakte Kapsel steht am Außenposten. Der Tracker zeigt wieder auf die Abholung. Ein Runden-Neustart setzt eine fehlgeschlagene Mission ebenfalls zurück. Bereits gespeicherte `Failed`-Zustände werden beim Laden repariert.

Der vorhandene sichere Weltstand bekommt sofort eine zurückgestellte Kapsel, damit Beenden während der Fehlschlaganzeige keine zerstörte oder noch angeschlossene Fracht konserviert. Lieferung, Freilassung und Abschluss bleiben ebenfalls erhalten, wenn der Pilot danach ohne neuen Speicherpunkt weiterfliegt. Andere Missionen und Fortschrittsflags werden nicht zurückgesetzt.

Kompilierung geprüft; keine Tests oder Play-Mode-Läufe gestartet. Im Spiel noch abnehmen: Container zerbrechen, Schleppschiff abstürzen, während der Fehlschlaganzeige beenden, Retry vom Landepad/EVA-Speicherpunkt, Weiterflug nach Lieferung und Speichern/Laden während EVA. Alte Spielstände ohne früheren sicheren Punkt können erst nach der nächsten sicheren Speicherung das neue Rückkehrverhalten nutzen.
