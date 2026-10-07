using TMPro;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    TMP_Text label;
    [SerializeField, TextArea] string source;
    public string Source => string.IsNullOrEmpty(source) ? GetComponent<TMP_Text>().text : source;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
        if (string.IsNullOrEmpty(source)) source = label.text;
    }
    void OnEnable()
    {
        if (!label) Awake();
        Localization.Changed += Refresh;
        Refresh();
    }
    void OnDisable() => Localization.Changed -= Refresh;

    public void SetSource(string value)
    {
        if (!label) Awake();
        if (source == value)
        {
            if (!Application.isPlaying) Refresh();
            return;
        }
        source = value;
        Refresh();
    }
    void Refresh() => label.text = Localization.Resolve(source);
}

public static class LocalizedTextExtensions
{
    public static void SetLocalizedText(this TMP_Text label, string source)
    {
        if (label.TryGetComponent<LocalizedText>(out var localized)) localized.SetSource(source);
        else label.text = Localization.Resolve(source);
    }
    public static string LocalizationSource(this TMP_Text label)
        => label.TryGetComponent<LocalizedText>(out var localized) ? localized.Source : label.text;
}
