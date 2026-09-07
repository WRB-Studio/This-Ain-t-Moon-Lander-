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

    readonly List<Button> allBtnOptions = new();
    Color originBtnColor;
    int selectedIndex;

    const string LEGACY_SECRET_KEY = "LANDER_SECRET_FOUND_";
    const string LEGACY_SEEN_KEY = "LANDER_SEEN_";
    const string SECRET_KEY = "LANDER_SECRET_FOUND_ID_";
    const string SEEN_KEY = "LANDER_SEEN_ID_";

    void Awake() => Instance = this;

    public void Init()
    {
        originBtnColor = btnOptionPrefab.GetComponent<Image>().color;

        btnLanderChooser.onClick.RemoveAllListeners();
        btnLanderChooser.onClick.AddListener(OpenCloseChooser);

        panelChooser.gameObject.SetActive(false);
        btnLanderChooser.gameObject.SetActive(false);

        selectedIndex = ResolveSelectedIndex();
        BuildChooser();

        MarkSeen(selectedIndex, save: false);
        SaveSelection();
        RefreshChooser();
        ApplySelected();
    }

    int ResolveSelectedIndex()
    {
        if (landerPrefabs == null || landerPrefabs.Length == 0) return 0;

        SaveGame data = SaveLoadManager.Instance.Data;
        if (!string.IsNullOrWhiteSpace(data.selectedLanderId))
        {
            int idIndex = FindIndexById(data.selectedLanderId);
            if (idIndex >= 0) return idIndex;
        }

        return Mathf.Clamp(data.selectedLanderIndex, 0, landerPrefabs.Length - 1);
    }

    void BuildChooser()
    {
        allBtnOptions.Clear();

        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            int index = i;
            LanderController preset = GetPreset(index);
            if (!preset) continue;

            Button button = Instantiate(btnOptionPrefab, optionsParent);
            allBtnOptions.Add(button);

            Image shipImage = button.transform.Find("ImgLander")?.GetComponent<Image>();
            SpriteRenderer spriteRenderer = preset.GetComponent<SpriteRenderer>();
            if (shipImage) shipImage.sprite = spriteRenderer ? spriteRenderer.sprite : null;

            GameObject secretImage = button.transform.Find("imgSecret")?.gameObject;
            if (secretImage)
                secretImage.SetActive(preset.isSecretLander && !IsSecretFound(index));

            button.onClick.AddListener(() => TryChoose(index));
        }
    }

    void TryChoose(int index)
    {
        if (!IsValidIndex(index) || !IsUnlocked(index)) return;

        selectedIndex = index;
        MarkSeen(index, save: false);
        SaveSelection();

        RefreshChooser();
        ApplySelected();
    }

    void SaveSelection()
    {
        if (!IsValidIndex(selectedIndex)) return;

        SaveGame data = SaveLoadManager.Instance.Data;
        data.selectedLanderIndex = selectedIndex;
        data.selectedLanderId = GetPreset(selectedIndex).LanderId;
        SaveLoadManager.Instance.Save();
    }

    void MarkSeen(int index, bool save = true)
    {
        if (!IsValidIndex(index)) return;

        SaveGame data = SaveLoadManager.Instance.Data;
        data.SetFlag(SEEN_KEY + GetPreset(index).LanderId, true);
        data.SetFlag(LEGACY_SEEN_KEY + index, true);

        if (save) SaveLoadManager.Instance.Save();
    }

    bool HasSeen(int index)
    {
        if (!IsValidIndex(index)) return false;

        SaveGame data = SaveLoadManager.Instance.Data;
        return data.GetFlag(SEEN_KEY + GetPreset(index).LanderId) ||
               data.GetFlag(LEGACY_SEEN_KEY + index);
    }

    public bool IsSecretFound(int index)
    {
        if (!IsValidIndex(index)) return false;

        SaveGame data = SaveLoadManager.Instance.Data;
        return data.GetFlag(SECRET_KEY + GetPreset(index).LanderId) ||
               data.GetFlag(LEGACY_SECRET_KEY + index);
    }

    public void UnlockSecret(int index)
    {
        if (!IsValidIndex(index)) return;

        SaveGame data = SaveLoadManager.Instance.Data;
        data.SetFlag(SECRET_KEY + GetPreset(index).LanderId, true);
        data.SetFlag(LEGACY_SECRET_KEY + index, true);
        SaveLoadManager.Instance.Save();
        RefreshChooser();
    }

    public bool HasAnyUnlockedSecret()
    {
        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            LanderController preset = GetPreset(i);
            if (preset && preset.isSecretLander && IsSecretFound(i))
                return true;
        }

        return false;
    }

    bool IsUnlocked(int index)
    {
        LanderController preset = GetPreset(index);
        if (!preset) return false;
        if (preset.isSecretLander) return IsSecretFound(index);

        return ScoringController.Instance.CollectedScore >= preset.unlockCost;
    }

    public void RefreshChooser()
    {
        int optionCount = Mathf.Min(landerPrefabs.Length, allBtnOptions.Count);

        for (int i = 0; i < optionCount; i++)
        {
            LanderController preset = GetPreset(i);
            Button button = allBtnOptions[i];
            if (!preset || !button) continue;

            Image background = button.GetComponent<Image>();
            Image shipImage = button.transform.Find("ImgLander")?.GetComponent<Image>();
            GameObject secretImage = button.transform.Find("imgSecret")?.gameObject;
            TMP_Text unlockText = button.transform.GetChild(1).GetComponent<TMP_Text>();

            bool secretHidden = preset.isSecretLander && !IsSecretFound(i);
            bool unlocked = IsUnlocked(i);

            if (shipImage) shipImage.enabled = !secretHidden;
            if (secretImage) secretImage.SetActive(secretHidden);
            button.interactable = unlocked;

            unlockText.gameObject.SetActive(false);

            if (secretHidden)
            {
                background.color = lockedColor;
            }
            else if (!unlocked)
            {
                background.color = lockedColor;
                unlockText.gameObject.SetActive(true);
                unlockText.text = preset.unlockCost.ToString();
            }
            else
            {
                background.color = HasSeen(i) ? originBtnColor : newUnlockedColor;
            }

            if (i == selectedIndex)
            {
                background.color = selectedBtnColor;
                unlockText.gameObject.SetActive(false);
            }
        }
    }

    void ApplySelected()
    {
        if (!IsValidIndex(selectedIndex) || !LanderController.Active) return;

        GameObject current = LanderController.Active.gameObject;
        GameObject prefab = landerPrefabs[selectedIndex];
        LanderController preset = prefab.GetComponent<LanderController>();

        LanderController.Active.ApplyConfigurationFrom(preset);
        ApplyVisualsAndCollider(current, prefab);
        ApplyThrustEffects(current, prefab);

        Physics2D.SyncTransforms();
    }

    void ApplyVisualsAndCollider(GameObject current, GameObject prefab)
    {
        SpriteRenderer currentSprite = current.GetComponent<SpriteRenderer>();
        SpriteRenderer presetSprite = prefab.GetComponent<SpriteRenderer>();
        if (currentSprite && presetSprite) currentSprite.sprite = presetSprite.sprite;

        PolygonCollider2D currentCollider = current.GetComponent<PolygonCollider2D>();
        PolygonCollider2D presetCollider = prefab.GetComponent<PolygonCollider2D>();
        if (!currentCollider || !presetCollider) return;

        currentCollider.offset = presetCollider.offset;
        currentCollider.isTrigger = presetCollider.isTrigger;
        currentCollider.compositeOperation = presetCollider.compositeOperation;
        currentCollider.pathCount = presetCollider.pathCount;

        for (int p = 0; p < presetCollider.pathCount; p++)
            currentCollider.SetPath(p, presetCollider.GetPath(p));
    }

    void ApplyThrustEffects(GameObject current, GameObject prefab)
    {
        for (int i = current.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = current.transform.GetChild(i);
            if (child.name.Contains("ThrustEffect"))
                Destroy(child.gameObject);
        }

        for (int i = 0; i < prefab.transform.childCount; i++)
        {
            Transform source = prefab.transform.GetChild(i);
            if (!source.name.Contains("ThrustEffect")) continue;

            GameObject copy = Instantiate(source.gameObject, current.transform);
            copy.name = source.name;
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
        }

        LanderController.Active.RefreshThrustEffects();
    }

    LanderController GetPreset(int index)
        => IsValidIndex(index) ? landerPrefabs[index]?.GetComponent<LanderController>() : null;

    bool IsValidIndex(int index)
        => landerPrefabs != null && index >= 0 && index < landerPrefabs.Length && landerPrefabs[index];

    int FindIndexById(string id)
    {
        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            LanderController preset = GetPreset(i);
            if (preset && preset.LanderId == id) return i;
        }

        return -1;
    }

    void OpenCloseChooser() => OpenCloseChooser(!panelChooser.gameObject.activeSelf);

    void OpenCloseChooser(bool open)
    {
        panelChooser.gameObject.SetActive(open);
        if (open) RefreshChooser();
        LanderUI.Instance.RefreshPanel();
    }
}
