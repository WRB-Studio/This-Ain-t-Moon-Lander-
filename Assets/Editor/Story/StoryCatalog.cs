using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class StoryCatalog
{
    class CatalogUndo : ScriptableObject { public string de, en; }
    static CatalogUndo history;
    static string appliedDE, appliedEN;
    static readonly Dictionary<string, (DateTime stamp, Localization.Language catalog)> cache = new();
    [Serializable] class TextValue { public string value; }
    static string Quote(string value)
    {
        string json = JsonUtility.ToJson(new TextValue { value = value });
        return json.Substring(9, json.Length - 10);
    }
    static void EnsureHistory()
    {
        if (history) return;
        history = ScriptableObject.CreateInstance<CatalogUndo>(); history.hideFlags = HideFlags.HideAndDontSave;
        history.de = File.ReadAllText("Assets/Resources/Localization/de.json"); history.en = File.ReadAllText("Assets/Resources/Localization/en.json");
        appliedDE = history.de; appliedEN = history.en;
        Undo.undoRedoPerformed += Restore;
    }
    static void Restore()
    {
        if (!history) return;
        foreach (string language in new[] { "de", "en" })
        {
            string path = "Assets/Resources/Localization/" + language + ".json";
            string snapshot = language == "de" ? history.de : history.en;
            string applied = language == "de" ? appliedDE : appliedEN;
            if (snapshot == applied) continue;
            File.WriteAllText(path, snapshot); AssetDatabase.ImportAsset(path);
            if (language == "de") appliedDE = snapshot; else appliedEN = snapshot;
        }
    }
    public static string Key(string reference)
    {
        var match = Regex.Match(reference ?? "", @"\[\[([a-zA-Z0-9_.-]+)");
        return match.Success ? match.Groups[1].Value : reference ?? "";
    }
    public static Localization.Language Load(string language)
    {
        string path = "Assets/Resources/Localization/" + language + ".json"; var stamp = File.GetLastWriteTimeUtc(path);
        if (cache.TryGetValue(language, out var item) && item.stamp == stamp) return item.catalog;
        var catalog = JsonUtility.FromJson<Localization.Language>(File.ReadAllText(path)); cache[language] = (stamp, catalog); return catalog;
    }
    public static string Text(string key, string language) => Load(language).entries.FirstOrDefault(e => e.key == key)?.text ?? "";
    public static void Write(string key, string language, string text)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        string path = "Assets/Resources/Localization/" + language + ".json";
        EnsureHistory();
        history.de = File.ReadAllText("Assets/Resources/Localization/de.json"); history.en = File.ReadAllText("Assets/Resources/Localization/en.json");
        Undo.RegisterCompleteObjectUndo(history, "Storytext " + language);
        string source = File.ReadAllText(path);
        string encodedKey = Quote(key), encodedText = Quote(text);
        var block = Regex.Match(source, @"(?s)\{\s*""key""\s*:\s*" + Regex.Escape(encodedKey) + @".*?\n\s*\}");
        string result;
        if (block.Success)
        {
            string replacement = Regex.Replace(block.Value, @"""text""\s*:\s*""(?:\\.|[^""\\])*""", _ => "\"text\": " + encodedText);
            result = source.Substring(0, block.Index) + replacement + source.Substring(block.Index + block.Length);
        }
        else
        {
            int end = source.LastIndexOf(']');
            if (end < 0) throw new InvalidDataException("Sprachkatalog hat keine Eintragsliste.");
            string newline = source.Contains("\r\n") ? "\r\n" : "\n";
            string head = source.Substring(0, end).TrimEnd();
            if (head.EndsWith("}")) head += ",";
            result = head + newline + "    {" + newline + "      \"key\": " + encodedKey + "," + newline
                + "      \"text\": " + encodedText + newline + "    }" + newline + "  " + source.Substring(end);
        }
        if (language == "de") history.de = result; else history.en = result;
        File.WriteAllText(path, result);
        if (language == "de") appliedDE = result; else appliedEN = result;
        AssetDatabase.ImportAsset(path);
    }
    public static bool TooLong(string text) => (text?.Length ?? 0) > 220 || Regex.Matches(text ?? "", @"[.!?](?:\s|$)").Count > 2;
}
