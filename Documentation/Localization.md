# Sprache, Texte und Pausemenü

In `MainScene` öffnet **PAUSE** oder **Escape** das Pausemenü. **Spiel beenden / Exit game** speichert den Spielstand und beendet die Anwendung; im Unity-Editor wird der Play Mode gestoppt. Unter Einstellungen stehen Sprache, Musiklautstärke und Effektlautstärke zur Verfügung. Escape schließt zunächst die Sprachauswahl oder die Einstellungen und anschließend das Menü. Bei geöffnetem Funkgerät schließt Escape zunächst das Funkgerät.

Die erste Sprachwahl folgt der Gerätesprache: Deutsch für deutsche Geräte, sonst Englisch. Eine bewusste Auswahl wird in `PlayerPrefs` unter `settings.language` gespeichert und ist unabhängig vom Spielstand. Lautstärken verwenden die vorhandenen Spielstandfelder. Die Simulation und Audioausgabe pausieren; beim Fortsetzen wird auch der Zustand eines bereits geöffneten Gesprächs oder Funkgeräts wiederhergestellt.

## Textkataloge

`Assets/Resources/Localization/en.json` und `de.json` enthalten dieselben stabilen Schlüssel mit den jeweiligen Texten. Es gibt 277 Einträge für Menüs, HUD, Ergebnisse, Story, Stationsschilder, Bewohnerkommentare, Gespräche und Funknachrichten. Spieltitel, Zahlen und Symbole bleiben unverändert.

```json
{
  "code": "en",
  "nativeName": "English",
  "entries": [
    { "key": "menu.resume", "text": "Resume" }
  ]
}
```

Schlüssel bleiben bestehen, auch wenn sich Wortlaut oder Bedeutung leicht ändern. Neue Texte bekommen einen neuen, aussagekräftigen Schlüssel in beiden Dateien. Zeilenumbrüche werden als `\n` gespeichert. `en` ist die Rückfallsprache für fehlende Übersetzungen.

## Texte verwenden

In serialisierten Textfeldern steht die Referenz, beispielsweise `[[menu.resume]]`. Das gilt auch für Dialogseiten, Antworten, Reaktionen und Funknachrichten. Dadurch speichern Nachrichten ihre Referenz und wechseln später weiterhin die Sprache.

Statische TMP-Labels besitzen die Komponente `LocalizedText`. Ihr Feld **Source** enthält die Referenz; das TMP-Textfeld zeigt eine Vorschau. Dynamische UI-Texte werden über den bestehenden Presenter geschrieben:

```csharp
label.SetLocalizedText("[[menu.resume]]");
label.SetLocalizedText("[[hud.level]] " + level);
label.SetLocalizedText(Localization.FormatReference("[[hud.refilling]]", percent));
```

`{0}`, `{1}` usw. sind Platzhalter in einem Katalogtext. Formatierte Referenzen behalten ihre Argumente beim Sprachwechsel. Argumente in dieser kompakten Referenzform dürfen keine `|` oder `]` enthalten; für freie Texte Referenzen und Inhalte zusammensetzen. Übersetzungen werden erst bei der Darstellung aufgelöst. Bereits aufgelöste Übersetzungen sollten daher nicht als Spielstandinhalt gespeichert werden.

Die Editoraktion **Tools → Localization → Apply Catalog to Game** verbindet bestehende Textfelder mit passenden Katalogeinträgen und ergänzt `LocalizedText` an TMP-Labels in Spielprefabs und Szenen. Sie erzeugt keine neuen Übersetzungen. Vor der Aktion Szenenänderungen speichern. Die dynamischen Labels des Sprachauswahlfelds werden vom TMP-Dropdown verwaltet.

## Weitere Sprache hinzufügen

Eine zusätzliche JSON-Datei im selben Ordner anlegen, einen eindeutigen `code` und den muttersprachlichen `nativeName` setzen und die vorhandenen Schlüssel übersetzen. Die Einstellungen finden die Datei automatisch. Für neue Schriftsysteme muss die verwendete TMP-Schrift passende Glyphen unterstützen. Die Erstwahl anhand der Gerätesprache berücksichtigt bislang Deutsch und Englisch; weitere Sprachen sind über die Auswahl verfügbar.

## Bestehende Spielstände und Wortlautänderungen

Alte englische Nachrichten, Gesprächsreaktionen und Ergebnistexte werden anhand des englischen Katalogs zu Referenzen normalisiert. Spielstandversion, Fortschrittsflags und Nachrichten-IDs bleiben erhalten. Wenn ein englischer Text geändert wird, kann sein früherer Wortlaut im englischen Eintrag unter `aliases` erhalten bleiben:

```json
{
  "key": "menu.resume",
  "text": "Resume game",
  "aliases": ["Resume"]
}
```

## Bearbeitung und Prüfung

Layout und Bedienelemente liegen in `Assets/Prefabs/UI/PauseMenu.prefab`; die Buttons verwenden das vorhandene `ActionButton`-Prefab. Das Menü ist in `MainScene` eingebunden. **Tools → UI → Build Pause Menu** baut das Ausgangslayout neu und überschreibt manuelle Änderungen am Menü-Prefab.

Für diese Umsetzung wurden die Skripte mit Unity kompiliert und native UI-Vorschauen für Deutsch/Englisch sowie 1080×1920, 1080×2400 und 1920×1080 gerendert. Lange Registrierungsseiten wurden zusätzlich visuell geprüft. Keine automatischen Testläufe oder Spieltests wurden ausgeführt.

Im Spiel prüfen: Pause während Flug, Countdown und EVA; Sprache bei geöffnetem Gespräch und Funkgerät umschalten; Lautstärken ändern; fortsetzen und erneut laden.
