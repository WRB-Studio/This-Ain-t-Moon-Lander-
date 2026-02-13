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

    const string KEY_SECRET_FOUND = "LANDER_SECRET_FOUND_";


    private void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        originBtnColor = btnOptionPrefab.GetComponent<Image>().color;

        btnLanderChooser.onClick.AddListener(() => OpenCloseChooser());

        panelChooser.gameObject.SetActive(false);
        btnLanderChooser.gameObject.SetActive(false);

        InitChooser();
        MarkSeen(0);
        RefreshChooser();     // Lock/Colors setzen
        ApplySelected();      // ausgewähltes Schiff anwenden (wenn erlaubt)
    }

    private void InitChooser()
    {
        selectedIndex = Mathf.Clamp(SaveLoadManager.Instance.Data.selectedLanderIndex, 0, landerPrefabs.Length - 1);

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
            bool secretHidden = IsSecret(index) && !IsSecretFound(index);
            imgSecret.SetActive(secretHidden);

            btn.onClick.AddListener(() => TryChoose(index));
        }
    }


    void TryChoose(int index)
    {
        if (!IsUnlocked(index)) return;

        MarkSeen(index); // <-- neu

        selectedIndex = index;
        SaveLoadManager.Instance.Data.selectedLanderIndex = selectedIndex;
        SaveLoadManager.Instance.Save();

        RefreshChooser();
        ApplySelected();
    }

    void MarkSeen(int index)
    {
        SaveLoadManager.Instance.Data.SetFlag("LANDER_SEEN_" + index, true);
        SaveLoadManager.Instance.Save();
    }

    bool HasSeen(int index)
    {
        return SaveLoadManager.Instance.Data.GetFlag("LANDER_SEEN_" + index, false);
    }


    bool IsSecret(int index)
        => landerPrefabs[index].GetComponent<LanderController>().isSecretLander;

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
        var lc = landerPrefabs[index].GetComponent<LanderController>();
        if (lc.isSecretLander) return IsSecretFound(index);

        int xp = ScoringController.Instance.CollectedScore;
        return xp >= lc.unlockCost;
    }


    public void RefreshChooser()
    {
        int xp = ScoringController.Instance.CollectedScore;

        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            var lc = landerPrefabs[i].GetComponent<LanderController>();
            var btn = allBtnOptions[i];
            var img = btn.GetComponent<Image>();

            var imgShip = btn.transform.GetChild(0).GetComponent<Image>();
            var imgSecret = btn.transform.Find("imgSecret")?.gameObject;

            bool secretHidden = lc.isSecretLander && !IsSecretFound(i);

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
            curPoly.isTrigger = prePoly.isTrigger;
            curPoly.compositeOperation = prePoly.compositeOperation;

            curPoly.pathCount = prePoly.pathCount;
            for (int p = 0; p < prePoly.pathCount; p++)
                curPoly.SetPath(p, prePoly.GetPath(p));
        }

        var lc = current.GetComponent<LanderController>();
        if (lc != null)
        {
            for (int i = current.transform.childCount - 1; i >= 0; i--)
            {
                var c = current.transform.GetChild(i);
                if (c.name.Contains("ThrustEffect"))
                    Destroy(c.gameObject);
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
