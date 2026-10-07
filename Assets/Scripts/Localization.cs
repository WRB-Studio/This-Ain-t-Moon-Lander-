using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

public static class Localization
{
    [Serializable] public class Entry { public string key; public string text; public string[] aliases; }
    [Serializable] public class Language
    {
        public string code;
        public string nativeName;
        public Entry[] entries;
    }
    const string Preference = "settings.language";
    static readonly Dictionary<string, Dictionary<string, string>> catalogs = new();
    static readonly Dictionary<string, string> sourceKeys = new();
    static readonly List<KeyValuePair<Regex, string>> legacyFormats = new();
    static readonly Regex references = new(@"\[\[([a-zA-Z0-9_.-]+)(?:\|([^\]]*))?\]\]", RegexOptions.Compiled);
    static Language[] languages;
    static string current;
    public static event Action Changed;
    public static IReadOnlyList<Language> Languages { get { Initialize(); return languages; } }
    public static string CurrentLanguage { get { Initialize(); return current; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        catalogs.Clear(); sourceKeys.Clear(); legacyFormats.Clear(); languages = null; current = null; Changed = null;
    }

    static void Initialize()
    {
        if (languages != null) return;
        languages = Resources.LoadAll<TextAsset>("Localization")
            .Select(asset => JsonUtility.FromJson<Language>(asset.text))
            .Where(language => language != null && !string.IsNullOrEmpty(language.code) && language.entries != null)
            .OrderBy(language => language.code).ToArray();
        foreach (var language in languages)
        {
            var catalog = new Dictionary<string, string>();
            foreach (var entry in language.entries)
            {
                if (string.IsNullOrEmpty(entry.key)) continue;
                if (catalog.ContainsKey(entry.key)) Debug.LogError("Duplicate localization key: " + language.code + "/" + entry.key);
                catalog[entry.key] = entry.text;
                if (language.code == "en" && !string.IsNullOrEmpty(entry.text))
                {
                    sourceKeys[entry.text] = entry.key;
                    if (entry.aliases != null)
                        foreach (var alias in entry.aliases)
                            if (!string.IsNullOrEmpty(alias)) sourceKeys[alias] = entry.key;
                    if (entry.text.Contains("{0}"))
                        legacyFormats.Add(new KeyValuePair<Regex, string>(new Regex("^" + Regex.Escape(entry.text)
                            .Replace(@"\{0}", @"(-?\d+(?:[.,]\d+)?)") + "$"), entry.key));
                }
            }
            catalogs[language.code] = catalog;
        }
        var preferred = PlayerPrefs.GetString(Preference, Application.systemLanguage == SystemLanguage.German ? "de" : "en");
        current = catalogs.ContainsKey(preferred) ? preferred : "en";
    }

    public static void SetLanguage(string code)
    {
        Initialize();
        if (current == code || !catalogs.ContainsKey(code)) return;
        current = code;
        PlayerPrefs.SetString(Preference, code);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static string Get(string key)
    {
        Initialize();
        if (catalogs.TryGetValue(current, out var catalog) && catalog.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)) return value;
        if (catalogs.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out value)) return value;
        return key;
    }

    // English aliases keep older saves and authored content readable while new content stores stable references.
    public static string Reference(string source)
    {
        Initialize();
        if (source != null) source = source.Replace("{TargetDistance}", "{0}");
        if (source == null || source.Contains("[[")) return source;
        if (sourceKeys.TryGetValue(source, out var key)) return "[[" + key + "]]";
        foreach (var format in legacyFormats)
        {
            var match = format.Key.Match(source);
            if (match.Success) return "[[" + format.Value + "|" + match.Groups[1].Value + "]]";
        }
        return source;
    }

    public static string FormatReference(string reference, params object[] arguments)
    {
        reference = Reference(reference);
        if (reference == null || !reference.StartsWith("[[") || !reference.EndsWith("]]")) return reference;
        return reference.Substring(0, reference.Length - 2) + "|" + string.Join("|", arguments.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture))) + "]]";
    }

    public static string Resolve(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;
        source = Reference(source);
        if (!source.Contains("[["))
            source = string.Join("\n\n", source.Split(new[] { "\n\n" }, StringSplitOptions.None).Select(Reference));
        return references.Replace(source, match =>
        {
            string text = Get(match.Groups[1].Value);
            if (!match.Groups[2].Success) return text;
            try { return string.Format(CultureInfo.InvariantCulture, text, match.Groups[2].Value.Split('|').Cast<object>().ToArray()); }
            catch (FormatException) { Debug.LogError("Invalid localization format: " + match.Groups[1].Value); return text; }
        });
    }
}
