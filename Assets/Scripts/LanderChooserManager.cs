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
            GameObject prefab = landerPrefabs[index];

            var sr = prefab.GetComponent<SpriteRenderer>();
            Sprite sprite = sr ? sr.sprite : null;

            Button btn = Instantiate(btnOptionPrefab, optionsParent);
            btn.transform.GetChild(0).GetComponent<Image>().sprite = sprite;

            allBtnOptions.Add(btn);

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


    bool IsUnlocked(int index)
    {
        int xp = ScoringController.Instance.CollectedScore;
        int req = landerPrefabs[index].GetComponent<LanderController>().unlockCost;
        return xp >= req;
    }

    public void RefreshChooser()
    {
        int xp = ScoringController.Instance.CollectedScore;

        for (int i = 0; i < landerPrefabs.Length; i++)
        {
            int req = landerPrefabs[i].GetComponent<LanderController>().unlockCost;

            var btn = allBtnOptions[i];
            var img = btn.GetComponent<Image>();

            TMP_Text txtUnlockCondition = txtUnlockCondition = btn.transform.GetChild(1).GetComponent<TMP_Text>();

            bool unlocked = xp >= req;
            btn.interactable = unlocked;

            if (!unlocked)
            {
                img.color = lockedColor;
                txtUnlockCondition.gameObject.SetActive(true);
                txtUnlockCondition.text = req.ToString();
            }
            else
            {
                img.color = HasSeen(i) ? originBtnColor : newUnlockedColor;
                txtUnlockCondition.gameObject.SetActive(false);
            }

            if (i == selectedIndex)
            {
                img.color = selectedBtnColor;
                txtUnlockCondition.gameObject.SetActive(false);
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
    }

}
