using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LanderChooserManager : MonoBehaviour
{
    public static LanderChooserManager Instance;

    [Header("UI")]
    public Button btnLanderChooser;
    public Transform panelChooser;
    public Transform optionsParent;
    public Button btnOptionPrefab;

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

    private void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        if (configurations.Count > 0) return;
        originBtnColor = btnOptionPrefab.GetComponent<Image>().color;

        btnLanderChooser.onClick.AddListener(() => OpenCloseChooser());

        panelChooser.gameObject.SetActive(false);
        btnLanderChooser.gameObject.SetActive(false);

        if (landerPrefabs == null || landerPrefabs.Length == 0)
        {
            Debug.LogError("No lander prefabs configured.", this);
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
        if (!IsUnlocked(selectedIndex)) selectedIndex = 0;
        data.selectedLanderIndex = selectedIndex;
        data.selectedLanderId = configurations[selectedIndex].landerIndex;

        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            int index = i;
            var prefab = landerPrefabs[index];

            var sr = prefab.GetComponent<SpriteRenderer>();
            Sprite sprite = sr ? sr.sprite : null;

            Button btn = Instantiate(btnOptionPrefab, optionsParent);
            allBtnOptions.Add(btn);

            var imgShip = btn.transform.Find("ImgLander").GetComponent<Image>();
            imgShip.sprite = sprite;
            
            var imgSecret = btn.transform.Find("imgSecret").gameObject;
            bool secretHidden = IsSecret(index) && !IsSecretFound(configurations[index].landerIndex);
            imgSecret.SetActive(secretHidden);

            btn.onClick.AddListener(() => TryChoose(index));
        }
    }

    void TryChoose(int index)
    {
        if (!IsUnlocked(index)) return;

        if (!GameController.Instance.CanChooseLander) return;
        MarkSeen(index);

        selectedIndex = index;
        SaveLoadManager.Instance.Data.selectedLanderIndex = selectedIndex;
        SaveLoadManager.Instance.Data.selectedLanderId = configurations[selectedIndex].landerIndex;
        SaveLoadManager.Instance.Save();

        RefreshChooser();
        ApplySelected();
    }

    void MarkSeen(int index)
    {
        SaveLoadManager.Instance.Data.SetFlag("LANDER_SEEN_" + configurations[index].landerIndex, true);
    }

    bool HasSeen(int index)
    {
        return SaveLoadManager.Instance.Data.GetFlag("LANDER_SEEN_" + configurations[index].landerIndex, false);
    }

    bool IsSecret(int index)
        => configurations[index].isSecretLander;

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

            bool secretHidden = lc.isSecretLander && !IsSecretFound(lc.landerIndex);

            imgShip.enabled = !secretHidden;
            if (imgSecret) imgSecret.SetActive(secretHidden);

            bool unlocked = IsUnlocked(i);
            btn.interactable = unlocked;

            TMP_Text txtUnlock = btn.transform.GetChild(1).GetComponent<TMP_Text>();

            if (secretHidden)
            {
                img.color = lockedColor;
                txtUnlock.gameObject.SetActive(false); // kein Preis bei Secret
            }
            else if (!unlocked)
            {
                img.color = lockedColor;
                txtUnlock.gameObject.SetActive(true);
                txtUnlock.text = lc.unlockCost.ToString();
            }
            else
            {
                img.color = HasSeen(i) ? originBtnColor : newUnlockedColor;
                txtUnlock.gameObject.SetActive(false);
            }

            if (i == selectedIndex)
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
            StoryTextController.Instance.Show("Guess the moon landing was real after all.");
            StoryTextController.Instance.Show("Okay… maybe this is a Moon Lander game. 😄");
        }
        selectedIndex = index;
        MarkSeen(index);
        var data = SaveLoadManager.Instance.Data;
        data.selectedLanderIndex = index;
        data.selectedLanderId = lander.landerIndex;
        SaveLoadManager.Instance.Save();
        RefreshChooser();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
