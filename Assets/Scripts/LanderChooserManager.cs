using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LanderChooserManager : MonoBehaviour
{
    public static LanderChooserManager Instance;

    [Header("UI")]
    public LandingPanel landingPanel;
    public CrashPanel crashPanel;
    public Button btnLanderChooser => landingPanel.chooseShipButton;
    public Button crashChooserButton => crashPanel.chooseShipButton;
    public Transform panelChooser;
    public Transform optionsParent;
    public Button btnOptionPrefab;
    public Button[] optionButtons;

    [Header("Content")]
    public GameObject[] landerPrefabs;

    [Header("Colors")]
    public Color selectedBtnColor;
    public Color lockedColor;
    public Color newUnlockedColor;
    private Color originBtnColor;

    private readonly List<Button> allBtnOptions = new();
    private int selectedIndex = 0;
    readonly List<LanderController> configurations = new();

    const string KEY_SECRET_FOUND = "LANDER_SECRET_FOUND_";
    [SerializeField, TextArea] string starterBlockedMessage = "As if I'd let you fly that thing to the Moon again.";

    bool IsStarterBlocked(int index) => index == 0
        && SaveLoadManager.Instance.Data.GetFlag("story.companyLanderReturned");

    private void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        if (configurations.Count > 0) return;
        originBtnColor = btnOptionPrefab.GetComponent<Image>().color;

        btnLanderChooser.onClick.AddListener(() => OpenCloseChooser());
        crashChooserButton.onClick.AddListener(() => OpenCloseChooser());

        panelChooser.gameObject.SetActive(false);
        SetChooserButtonsVisible(false);

        if (landerPrefabs == null || landerPrefabs.Length == 0 || optionButtons == null
            || optionButtons.Length != landerPrefabs.Length || System.Array.Exists(optionButtons, button => !button))
        {
            Debug.LogError("Configure lander prefabs and rebuild the chooser buttons in the Inspector.", this);
            enabled = false;
            return;
        }
        foreach (var prefab in landerPrefabs) configurations.Add(prefab.GetComponent<LanderController>());
        InitChooser();
        MarkSeen(0);
        RefreshChooser();
        ApplySelected();
        SaveLoadManager.Instance.Save();
    }

    private void InitChooser()
    {
        var data = SaveLoadManager.Instance.Data;
        selectedIndex = configurations.FindIndex(config => config.landerIndex == data.selectedLanderId);
        if (selectedIndex < 0) selectedIndex = Mathf.Clamp(data.selectedLanderIndex, 0, landerPrefabs.Length - 1);
        if (!IsUnlocked(selectedIndex))
        {
            selectedIndex = configurations.FindIndex(config => config.isSecretLander && IsSecretFound(config.landerIndex));
            if (selectedIndex < 0) selectedIndex = 0;
        }
        data.selectedLanderIndex = selectedIndex;
        data.selectedLanderId = configurations[selectedIndex].landerIndex;

        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            int index = i;
            Button btn = optionButtons[index];
            allBtnOptions.Add(btn);
            btn.onClick.AddListener(() => TryChoose(index));
        }
    }

    void TryChoose(int index)
    {
        if (IsStarterBlocked(index))
        {
            OpenCloseChooser(false);
            StoryTextController.Instance.ShowOperatorDialogue(starterBlockedMessage);
            return;
        }
        if (!IsUnlocked(index)) return;

        if (!GameController.Instance.CanChooseLander) return;
        MarkSeen(index);

        selectedIndex = index;
        SaveLoadManager.Instance.Data.selectedLanderIndex = selectedIndex;
        SaveLoadManager.Instance.Data.selectedLanderId = configurations[selectedIndex].landerIndex;
        SaveLoadManager.Instance.Save();

        RefreshChooser();
        if (GameController.Instance.Phase == GameController.GamePhase.Crashed) ApplySelected();
    }

    public LanderController SpawnSelectedLander()
    {
        var previous = LanderController.Instance;
        previous.Park();
        previous.isActive = false;
        LanderController.Instance = null;
        var next = Instantiate(landerPrefabs[selectedIndex], previous.transform.position, Quaternion.identity, previous.transform.parent)
            .GetComponent<LanderController>();
        next.isActive = true;
        LanderController.Instance = next;
        next.Init();
        return next;
    }

    public GameObject GetPrefab(int definitionId)
    {
        int index = configurations.FindIndex(config => config.landerIndex == definitionId);
        return index >= 0 ? landerPrefabs[index] : null;
    }

    void MarkSeen(int index)
    {
        SaveLoadManager.Instance.Data.SetFlag("LANDER_SEEN_" + configurations[index].landerIndex, true);
    }

    bool HasSeen(int index)
    {
        return SaveLoadManager.Instance.Data.GetFlag("LANDER_SEEN_" + configurations[index].landerIndex, false);
    }

    public bool IsSecretFound(int index)
        => SaveLoadManager.Instance.Data.GetFlag(KEY_SECRET_FOUND + index, false);

    public void UnlockSecret(int index)
    {
        SaveLoadManager.Instance.Data.SetFlag(KEY_SECRET_FOUND + index, true);
        SaveLoadManager.Instance.Save();
        RefreshChooser();
    }

    bool IsUnlocked(int index)
    {
        if (IsStarterBlocked(index)) return false;
        var lc = configurations[index];
        if (lc.isSecretLander) return IsSecretFound(lc.landerIndex);

        int xp = ScoringController.Instance.CollectedScore;
        return xp >= lc.unlockCost;
    }

    public void RefreshChooser()
    {
        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            var lc = configurations[i];
            var btn = allBtnOptions[i];
            var img = btn.GetComponent<Image>();

            var imgShip = btn.transform.GetChild(0).GetComponent<Image>();
            var imgSecret = btn.transform.Find("imgSecret")?.gameObject;
            var unavailable = btn.transform.Find("imgUnavailable")?.gameObject;
            bool blocked = IsStarterBlocked(i);
            if (unavailable) unavailable.SetActive(blocked);

            bool secretHidden = lc.isSecretLander && !IsSecretFound(lc.landerIndex);

            imgShip.enabled = !secretHidden;
            if (imgSecret) imgSecret.SetActive(secretHidden);

            bool unlocked = IsUnlocked(i);
            // Keep the blocked starter clickable for the operator's refusal, without selecting it.
            btn.interactable = unlocked || blocked;

            TMP_Text txtUnlock = btn.transform.GetChild(1).GetComponent<TMP_Text>();

            if (secretHidden)
            {
                img.color = lockedColor;
                txtUnlock.gameObject.SetActive(false); // kein Preis bei Secret
            }
            else if (blocked)
            {
                img.color = lockedColor;
                txtUnlock.gameObject.SetActive(false);
            }
            else if (!unlocked)
            {
                img.color = lockedColor;
                txtUnlock.gameObject.SetActive(true);
                txtUnlock.text = lc.unlockCost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                img.color = HasSeen(i) ? originBtnColor : newUnlockedColor;
                txtUnlock.gameObject.SetActive(false);
            }

            if (i == selectedIndex && !blocked)
            {
                img.color = selectedBtnColor;
                txtUnlock.gameObject.SetActive(false);
            }
        }
    }

    void ApplySelected()
    {
        ApplyVisualsAndColliderFromPrefab(LanderController.Instance.gameObject, landerPrefabs[selectedIndex]);
    }

    void ApplyVisualsAndColliderFromPrefab(GameObject current, GameObject prefab)
    {
        var curSR = current.GetComponent<SpriteRenderer>();
        var preSR = prefab.GetComponent<SpriteRenderer>();
        if (curSR && preSR) curSR.sprite = preSR.sprite;

        var curPoly = current.GetComponent<PolygonCollider2D>();
        var prePoly = prefab.GetComponent<PolygonCollider2D>();

        if (curPoly && prePoly)
        {
            curPoly.offset = prePoly.offset;
            // The runtime owns trigger state while the astronaut is outside.
            curPoly.compositeOperation = prePoly.compositeOperation;

            curPoly.pathCount = prePoly.pathCount;
            for (int p = 0; p < prePoly.pathCount; p++)
                curPoly.SetPath(p, prePoly.GetPath(p));
        }

        var lc = current.GetComponent<LanderController>();
        if (lc != null)
        {
            lc.ApplyConfiguration(prefab.GetComponent<LanderController>());
            for (int i = current.transform.childCount - 1; i >= 0; i--)
            {
                var c = current.transform.GetChild(i);
                if (c.name.Contains("ThrustEffect"))
                {
                    c.gameObject.SetActive(false);
                    Destroy(c.gameObject);
                }
            }

            lc.thrustEffects.Clear();
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                var pc = prefab.transform.GetChild(i);
                if (!pc.name.Contains("ThrustEffect")) continue;

                var copy = Instantiate(pc.gameObject, current.transform);
                copy.name = pc.name;
                copy.transform.localPosition = pc.localPosition;
                copy.transform.localRotation = pc.localRotation;
                copy.transform.localScale = pc.localScale;

                lc.thrustEffects.Add(copy.transform);
            }
        }

        Physics2D.SyncTransforms();
    }

    public bool HasFoundSecret
    {
        get
        {
            foreach (var config in configurations)
                if (config.isSecretLander && IsSecretFound(config.landerIndex)) return true;
            return false;
        }
    }

    public void SelectDiscoveredLander(LanderController lander)
    {
        int index = configurations.FindIndex(config => config.landerIndex == lander.landerIndex);
        if (index < 0) return;
        bool newlyFound = lander.isSecretLander && !IsSecretFound(lander.landerIndex);
        if (newlyFound)
        {
            SaveLoadManager.Instance.Data.SetFlag(KEY_SECRET_FOUND + lander.landerIndex, true);
            StoryTextController.Instance.Discover(StoryTextController.Discovery.AbandonedShip);
        }
        selectedIndex = index;
        MarkSeen(index);
        var data = SaveLoadManager.Instance.Data;
        data.selectedLanderIndex = index;
        data.selectedLanderId = lander.landerIndex;
        SaveLoadManager.Instance.Save();
        RefreshChooser();
    }

#if UNITY_EDITOR
    public void DebugSetSecretFound(bool found)
    {
        foreach (var config in configurations)
            if (config.isSecretLander) SaveLoadManager.Instance.Data.SetFlag(KEY_SECRET_FOUND + config.landerIndex, found);
        RefreshChooser();
    }

    public void DebugSelectLander(bool secret)
    {
        selectedIndex = secret ? configurations.FindIndex(config => config.isSecretLander) : 0;
        if (selectedIndex < 0) selectedIndex = 0;
        SaveLoadManager.Instance.Data.selectedLanderIndex = selectedIndex;
        SaveLoadManager.Instance.Data.selectedLanderId = configurations[selectedIndex].landerIndex;
        ApplySelected();
        RefreshChooser();
    }
#endif

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    public void SetChooserButtonsVisible(bool visible)
    {
        btnLanderChooser.gameObject.SetActive(visible);
        crashChooserButton.gameObject.SetActive(visible);
    }

    private void OpenCloseChooser()
    {
        OpenCloseChooser(!panelChooser.gameObject.activeSelf);
    }

    private void OpenCloseChooser(bool open)
    {
        panelChooser.gameObject.SetActive(open);

        if (open) RefreshChooser();

        LanderUI.Instance.RefreshPanel();
    }

}
