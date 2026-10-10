# Bestehende Story: Textpolitur

Stand: 10. Oktober 2026, Arbeitsbranch `codex/story-polish`.

Der vorhandene Abschnitt vom Landetraining über Mond, Firmenschiff, Station und Registrierung bis zu Rhekks Rettung ist in Deutsch und Englisch redaktionell überarbeitet. Keine neuen Missionen, Storyflags oder Entscheidungsfolgen eingeführt.

- 65 Sprachschlüssel in beiden Katalogen überarbeitet. Lange Monologe durch ein bis zwei kurze Sätze ersetzt; technische Transportanweisungen bleiben vollständig.
- Der Trainingspilot deutet seine frühere eigene Erfahrung kurz an: „Ich dachte früher auch, das wäre alles.“ Identität, Aufzeichnungen und genaue Herkunft des Trainings werden nicht vorweggenommen.
- Registrierung erklärt den vermissten Transport, gibt das Funkgerät auch nach Ablehnung aus und führt knapp zu FUNK / Signal orten. Reaktionen sind kurz genug, um gemeinsam mit der nächsten Frage lesbar zu bleiben.
- Rhekks Antworten folgen der jeweiligen Aussage: Herkunft, vermisster Pilot, Transportmöglichkeit, Ablage und Abschied. Überflüssige oder bereits beantwortete Fragen wurden ersetzt. Bestehende Antwortanzahl und Zielverbindungen bleiben erhalten.
- Seilaufnahme, freies Ende, Schlepppunkt, sanftes Absetzen, automatische Kupplung, eigenes Landen und Öffnen sind weiter erklärt. Humor steht vor allem in Begrüßung und Dank statt zwischen den Bedienungsschritten.
- Die Funknachrichten wiederholen nur den aktuell nötigen nächsten Schritt. Fehlertext erklärt weiterhin den Neustart ohne Verlust des übrigen Fortschritts.
- Generierte Storyboard-Titel an die neuen Texte angepasst; eigene Titel bleiben erhalten. Technische Schlüssel bleiben trotz ihrer ursprünglichen englischen Formulierung unverändert.
- Frühere englische Formulierungen bleiben als Lokalisierungs-Aliase erhalten, damit ältere gespeicherte Klartexte weiterhin auf dieselben Schlüssel verweisen können.

Beispiel nach der Befreiung:

> Danke, dass du mich hergebracht hast.
> Meine Organe hätten einen ruhigeren Flug bevorzugt.

Bearbeitung: `Assets/Resources/Localization/de.json`, `en.json` und generierte Titel in `Assets/Resources/Story/StoryProject.asset`.

Katalogstruktur und Texte redaktionell geprüft; keine Tests oder Play-Mode-Läufe gestartet. Die vollständige Wirkung im Spiel (Timing, Platz auf Mobile und Antwortübergänge) bleibt manuell abzunehmen. Die nächste Story nach Rhekks Ankunft ist weiterhin offen.
