using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Standalone visual comparison: no story progression or save-game access.
public class CommunicationPanelGallery : MonoBehaviour
{
    [Serializable] public class SampleText
    {
        public TMP_Text label;
        [TextArea] public string german;
        [TextArea] public string english;
    }
    [Serializable] public class PanelCase
    {
        public string title;
        public string group;
        public Vector2 focus;
        [TextArea] public string purpose;
        public string sourcePrefab;
        [TextArea] public string comparison;
        public GameObject root;
        public SampleText[] texts;
        public Button[] demoButtons;
    }
    [SerializeField] PanelCase[] cases;
    [SerializeField] ScrollRect comparisonScroll;
    [SerializeField] int selected;

    void Start()
    {
        if (!comparisonScroll) return;
        var mask = comparisonScroll.viewport.GetComponent<RectMask2D>();
        if (mask) mask.enabled = true;
    }

    public void ShowCase(int index)
    {
        if (cases == null || cases.Length == 0) return;
        selected = (index + cases.Length) % cases.Length;
        foreach (var panel in cases)
            if (panel.root) panel.root.SetActive(true);
        if (comparisonScroll)
        {
            comparisonScroll.StopMovement();
            comparisonScroll.normalizedPosition = cases[selected].focus;
        }
    }
}
