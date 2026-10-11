using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

public static class SpaceExperienceAuthoring
{
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
    public static void Build()
    {
        RepairResidents();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var resident in Object.FindObjectsByType<StationResident>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var data = new SerializedObject(resident); var comments = data.FindProperty("comments");
            if (comments.arraySize == 0)
            {
                comments.arraySize = 3;
                for (int i = 0; i < 3; i++) comments.GetArrayElementAtIndex(i).stringValue = "[[station.ambient." + i + "]]";
            }
            var text = data.FindProperty("bubbleText").objectReferenceValue as TMP_Text;
            if (text) { text.SetLocalizedText(""); text.gameObject.SetActive(false); }
            data.FindProperty("commentDistance").floatValue = 4;
            data.FindProperty("commentHideDistance").floatValue = 6;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
        }
        var navigation = BuildNavigation(); PrefabUtility.InstantiatePrefab(navigation, scene);
        MoonSurface(GravityManager2D.Instance ? GravityManager2D.Instance.gameObject : Object.FindFirstObjectByType<GravityManager2D>().gameObject);
        var ui = Object.FindFirstObjectByType<LanderUI>();
        var earth = new GameObject("IndicatorEarth", typeof(RectTransform), typeof(CanvasRenderer), typeof(MoonNavigationGraphic));
        earth.transform.SetParent(ui.indicatorMoon.parent, false); var earthRect = earth.GetComponent<RectTransform>();
        earthRect.sizeDelta = new Vector2(70, 70); earth.GetComponent<MoonNavigationGraphic>().raycastTarget = false;
        var earthGraphic = new SerializedObject(earth.GetComponent<MoonNavigationGraphic>());
        earthGraphic.FindProperty("earth").boolValue = true; earthGraphic.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAssetAndConnect(earth, "Assets/Prefabs/UI/EarthNavigationIcon.prefab", InteractionMode.AutomatedAction);
        var uiData = new SerializedObject(ui); uiData.FindProperty("indicatorEarth").objectReferenceValue = earthRect; uiData.ApplyModifiedPropertiesWithoutUndo();
        Target(Object.FindFirstObjectByType<GravityManager2D>().gameObject, SpaceNavigationTarget.ObjectKind.Moon, "navigation.moon", "moon", 1800, 35);
        var earthTarget = new GameObject("EarthNavigationTarget");
        Target(earthTarget, SpaceNavigationTarget.ObjectKind.Earth, "navigation.earth", "earth", 2500, 60);
        Target(Object.FindFirstObjectByType<SpaceStation>().gameObject, SpaceNavigationTarget.ObjectKind.Station, "game.station", "station", 700, 45);
        Target(Object.FindFirstObjectByType<AsteroidOutpost>().gameObject, SpaceNavigationTarget.ObjectKind.Outpost, "cargo.outpost.title", "outpost", 500, 50);
        var chooser = new SerializedObject(Object.FindFirstObjectByType<LanderChooserManager>());
        var ships = chooser.FindProperty("landerPrefabs");
        for (int i = 0; i < ships.arraySize; i++)
        {
            var asset = ships.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (!asset) continue;
            string path = AssetDatabase.GetAssetPath(asset); var root = PrefabUtility.LoadPrefabContents(path);
            try { Target(root, SpaceNavigationTarget.ObjectKind.Ship, "navigation.ship", "ship." + root.GetComponent<LanderController>().landerIndex, 180, 10); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (var ship in Object.FindObjectsByType<LanderController>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(s => s.gameObject.name))
            Target(ship.gameObject, SpaceNavigationTarget.ObjectKind.Ship, "navigation.ship", "ship." + ship.landerIndex, 180, 10);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Space experience authored: walkable lunar craters, flag, navigation prefab, Earth icon and resident bubbles.");
    }
    static void Target(GameObject root, SpaceNavigationTarget.ObjectKind kind, string key, string id, float far, float near)
    {
        var target = root.GetComponent<SpaceNavigationTarget>() ?? root.AddComponent<SpaceNavigationTarget>();
        target.kind = kind; target.titleKey = key; target.signalId = id; target.detectionDistance = far; target.closeDistance = near;
        EditorUtility.SetDirty(target);
    }
    static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static TMP_Text Text(Transform parent, string name, Vector2 size, Vector2 position, float fontSize)
    {
        var text = Rect(parent, name, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font; text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false; text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    static GameObject BuildNavigation()
    {
        var marker = Rect(null, "SpaceNavigationMarker", new Vector2(150, 110), Vector2.zero).gameObject;
        var controller = marker.AddComponent<SpaceNavigationMarker>();
        var arrow = Rect(marker.transform, "Arrow", new Vector2(56, 56), new Vector2(0, 24));
        var graphic = arrow.gameObject.AddComponent<NavigationArrowGraphic>(); graphic.raycastTarget = false;
        controller.arrow = arrow; controller.graphic = graphic;
        controller.distanceText = Text(marker.transform, "Distance", new Vector2(160, 40), new Vector2(0, -28), 25);
        var markerAsset = PrefabUtility.SaveAsPrefabAsset(marker, "Assets/Prefabs/UI/SpaceNavigationMarker.prefab"); Object.DestroyImmediate(marker);
        var root = Rect(null, "SpaceFlightNavigation", Vector2.zero, Vector2.zero).gameObject;
        var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 46;
        var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var markers = Rect(root.transform, "DirectionMarkerSystem", Vector2.zero, Vector2.zero); markers.anchorMin = Vector2.zero; markers.anchorMax = Vector2.one;
        var markerSystem = markers.gameObject.AddComponent<DirectionMarkerSystem>();
        var aside = Text(root.transform, "PeripheralComment", new Vector2(900, 180), new Vector2(0, 180), 30);
        PlaceComment(aside.rectTransform);
        var source = new SerializedObject(markerSystem);
        source.FindProperty("markerPrefab").objectReferenceValue = markerAsset.GetComponent<SpaceNavigationMarker>();
        source.FindProperty("markerRoot").objectReferenceValue = markers;
        source.ApplyModifiedPropertiesWithoutUndo();
        var navigation = new SerializedObject(root.AddComponent<SpaceFlightNavigation>());
        navigation.FindProperty("asideText").objectReferenceValue = aside;
        navigation.ApplyModifiedPropertiesWithoutUndo();
        var asset = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/UI/SpaceFlightNavigation.prefab"); Object.DestroyImmediate(root); return asset;
    }
    public static void AddUnknownFlightMarker()
    {
        string path = "Assets/Prefabs/UI/SpaceNavigationMarker.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var marker = root.GetComponent<SpaceNavigationMarker>();
        var existing = root.transform.Find("Unknown");
        var text = existing ? existing.GetComponent<TMP_Text>() : Text(root.transform, "Unknown", new Vector2(40, 40), new Vector2(-40, 24), 34);
        text.text = "?"; text.fontStyle = FontStyles.Bold; text.gameObject.SetActive(false);
        marker.unknownText = text;
        PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets();
        Debug.Log("Unknown flight marker authored: upright question mark beside rotating arrow.");
    }
    public static void RefinePanelBackdrops()
    {
        string backdropPath = "Assets/Prefabs/UI/PanelBackdrop.prefab";
        var root = PrefabUtility.LoadPrefabContents(backdropPath);
        var image = root.GetComponent<Image>(); if (image) Object.DestroyImmediate(image);
        var graphic = root.GetComponent<TextBackdropGraphic>() ?? root.AddComponent<TextBackdropGraphic>();
        graphic.color = new Color(0, 0, 0, .9f); graphic.raycastTarget = false;
        PrefabUtility.SaveAsPrefabAsset(root, backdropPath); PrefabUtility.UnloadPrefabContents(root);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(backdropPath);
        foreach (string path in new[] { "Assets/Prefabs/UI/Panels/StartPanel.prefab", "Assets/Prefabs/UI/Panels/ResultPanel.prefab", "Assets/Prefabs/UI/StoryDialog.prefab" })
        {
            root = PrefabUtility.LoadPrefabContents(path);
            AddPanelBackdrop(root.transform, asset);
            if (root.GetComponent<StoryDialog>() && root.TryGetComponent<Image>(out var background)) background.color = new Color(0, 0, 0, .12f);
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        foreach (var panel in new Component[] { ui.startPanel, ui.resultPanel }) AddPanelBackdrop(panel.transform, asset);
        var dialog = Object.FindFirstObjectByType<StoryDialog>(FindObjectsInactive.Include);
        if (dialog)
        {
            AddPanelBackdrop(dialog.transform, asset);
            if (dialog.TryGetComponent<Image>(out var background)) background.color = new Color(0, 0, 0, .12f);
        }
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Panel backdrops refined: compact text plates, visible scene between content, light dialogue dimming.");
    }
    public static void PolishFlightReadability()
    {
        var backdrop = Rect(null, "PanelBackdrop", Vector2.zero, Vector2.zero);
        var image = backdrop.gameObject.AddComponent<TextBackdropGraphic>(); image.color = new Color(0, 0, 0, .9f); image.raycastTarget = false;
        backdrop.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var backdropAsset = PrefabUtility.SaveAsPrefabAsset(backdrop.gameObject, "Assets/Prefabs/UI/PanelBackdrop.prefab");
        Object.DestroyImmediate(backdrop.gameObject);
        foreach (string path in new[] { "Assets/Prefabs/UI/Panels/StartPanel.prefab", "Assets/Prefabs/UI/Panels/ResultPanel.prefab", "Assets/Prefabs/UI/StoryDialog.prefab", "Assets/Prefabs/UI/StationWorldText.prefab", "Assets/Prefabs/Station01/StationInterior.prefab", "Assets/Prefabs/Station01/SpaceStation.prefab", "Assets/Prefabs/Missions/AsteroidOutpost.prefab", "Assets/Prefabs/Missions/PassengerCapsule.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (path.Contains("/Panels/") || root.GetComponent<StoryDialog>()) AddPanelBackdrop(root.transform, backdropAsset);
            if (root.GetComponent<StoryDialog>() && root.TryGetComponent<Image>(out var background)) background.color = new Color(0, 0, 0, .12f);
            RemoveWorldTitles(root);
            AssignConversationPartner(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var root in scene.GetRootGameObjects()) { RemoveWorldTitles(root); AssignConversationPartner(root); }
        var ui = Object.FindFirstObjectByType<LanderUI>();
        foreach (var panel in new Component[] { ui.startPanel, ui.resultPanel })
            AddPanelBackdrop(panel.transform, backdropAsset);
        var dialog = Object.FindFirstObjectByType<StoryDialog>(FindObjectsInactive.Include);
        if (dialog) AddPanelBackdrop(dialog.transform, backdropAsset);
        if (dialog && dialog.TryGetComponent<Image>(out var dialogBackground)) dialogBackground.color = new Color(0, 0, 0, .12f);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Readable panel backdrops, conversation partner and world-title cleanup authored.");
    }
    static void AddPanelBackdrop(Transform parent, GameObject asset)
    {
        var fullPanel = parent.Find("PanelBackdrop");
        if (fullPanel) Object.DestroyImmediate(fullPanel.gameObject);
        foreach (var text in parent.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponentInParent<Button>()) continue;
            string name = "TextBackdrop_" + text.name;
            var existing = text.transform.parent.Find(name);
            var rect = existing ? existing.GetComponent<RectTransform>() : ((GameObject)PrefabUtility.InstantiatePrefab(asset, text.transform.parent)).GetComponent<RectTransform>();
            rect.name = name;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.zero;
            rect.SetAsFirstSibling();
            var data = new SerializedObject(rect.GetComponent<TextBackdropGraphic>());
            data.FindProperty("source").objectReferenceValue = text;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    static void RemoveWorldTitles(GameObject root)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            var canvas = text.GetComponentInParent<Canvas>();
            if (canvas && canvas.renderMode != RenderMode.WorldSpace) continue;
            string source = text.LocalizationSource();
            if (source != "[[game.station]]" && source != "Station"
                && text.name != "OutpostName" && text.name != "PadMark" && text.name != "PassengerDeliverySign" && text.name != "PassengerLabel") continue;
            text.SetLocalizedText("");
            text.gameObject.SetActive(false);
            EditorUtility.SetDirty(text);
            if (text.TryGetComponent<LocalizedText>(out var localized)) EditorUtility.SetDirty(localized);
        }
    }
    static void AssignConversationPartner(GameObject root)
    {
        foreach (var conversation in root.GetComponentsInChildren<StationConversation>(true))
        {
            var talk = conversation.GetComponentsInChildren<StationInteraction>(true).FirstOrDefault(t => t.action == StationInteraction.Action.Talk);
            if (!talk) talk = root.GetComponentsInChildren<StationInteraction>(true).FirstOrDefault(t => t.action == StationInteraction.Action.Talk);
            if (!talk) continue;
            var partner = talk.transform.parent.GetComponentsInChildren<CharacterVisual>(true)
                .OrderBy(c => Vector2.Distance(c.transform.position, talk.transform.position)).FirstOrDefault();
            if (!partner) continue;
            var data = new SerializedObject(conversation);
            data.FindProperty("conversationPartner").objectReferenceValue = partner;
            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("Conversation faces player: " + partner.transform.parent.name);
        }
    }
    public static void AddUnknownStationNavigation()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        var data = new SerializedObject(ui);
        var station = data.FindProperty("indicatorStation").objectReferenceValue as RectTransform;
        if (!station)
        {
            var text = Text(ui.indicatorMoon.parent, "IndicatorStation", new Vector2(70, 70), Vector2.zero, 58);
            text.text = "?";
            text.fontStyle = FontStyles.Bold;
            station = text.rectTransform;
            AddHeightArrows(station, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/NavigationHeightArrow.prefab"));
            station.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(station.gameObject, "Assets/Prefabs/UI/UnknownStationNavigationIcon.prefab", InteractionMode.AutomatedAction);
        }
        data.FindProperty("indicatorStation").objectReferenceValue = station;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Unknown station marker authored in lower navigation bar.");
    }
    public static void AddNavigationHeightIndicators()
    {
        string arrowPath = "Assets/Prefabs/UI/NavigationHeightArrow.prefab";
        var arrow = Rect(null, "NavigationHeightArrow", new Vector2(24, 24), Vector2.zero);
        arrow.gameObject.AddComponent<NavigationArrowGraphic>().raycastTarget = false;
        var arrowAsset = PrefabUtility.SaveAsPrefabAsset(arrow.gameObject, arrowPath);
        Object.DestroyImmediate(arrow.gameObject);
        foreach (string path in new[] { "Assets/Prefabs/UI/EarthNavigationIcon.prefab", "Assets/Prefabs/UI/MoonNavigationIcon.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            AddHeightArrows(root.GetComponent<RectTransform>(), arrowAsset);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        foreach (var indicator in new[] { ui.indicatorPad, ui.indicatorMoon,
            (RectTransform)new SerializedObject(ui).FindProperty("indicatorEarth").objectReferenceValue })
            if (indicator && !indicator.GetComponent<NavigationHeightIndicator>()) AddHeightArrows(indicator, arrowAsset);
        foreach (var legacy in Object.FindObjectsByType<StationSignalIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            legacy.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene);
        var legacyRoot = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StationSignal.prefab");
        legacyRoot.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(legacyRoot, "Assets/Prefabs/UI/StationSignal.prefab");
        PrefabUtility.UnloadPrefabContents(legacyRoot);
        AssetDatabase.SaveAssets();
        Debug.Log("Navigation height arrows authored; legacy station signal disabled.");
    }
    static void AddHeightArrows(RectTransform root, GameObject arrowAsset)
    {
        var indicator = root.GetComponent<NavigationHeightIndicator>() ?? root.gameObject.AddComponent<NavigationHeightIndicator>();
        var data = new SerializedObject(indicator);
        for (int i = 0; i < 2; i++)
        {
            string name = i == 0 ? "Above" : "Below";
            var existing = root.Find(name);
            var arrow = existing ? existing.GetComponent<RectTransform>()
                : ((GameObject)PrefabUtility.InstantiatePrefab(arrowAsset, root)).GetComponent<RectTransform>();
            arrow.name = name;
            arrow.anchoredPosition = new Vector2(0, (root.rect.height * .5f + 12) * (i == 0 ? 1 : -1));
            arrow.localRotation = Quaternion.Euler(0, 0, i == 0 ? 90 : -90);
            arrow.gameObject.SetActive(false);
            data.FindProperty(i == 0 ? "above" : "below").objectReferenceValue = arrow.GetComponent<NavigationArrowGraphic>();
        }
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void PolishHUD()
    {
        foreach (string path in new[] { "Assets/Prefabs/UI/SpaceFlightNavigation.prefab", "Assets/Prefabs/UI/SpaceNavigationMarker.prefab",
            "Assets/Prefabs/UI/CargoMissionHUD.prefab", "Assets/Prefabs/Astronaut.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var markerSystem = root.GetComponent<DirectionMarkerSystem>();
                if (markerSystem)
                {
                    var data = new SerializedObject(markerSystem); data.FindProperty("maximumMarkers").intValue = 3;
                    data.FindProperty("orbitRadius").floatValue = 260; data.FindProperty("metresPerUnit").floatValue = 10;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var navigation = root.GetComponent<SpaceFlightNavigation>();
                if (navigation)
                {
                    var data = new SerializedObject(navigation);
                    var text = (TMP_Text)data.FindProperty("asideText").objectReferenceValue;
                    PlaceComment(text.rectTransform); data.ApplyModifiedPropertiesWithoutUndo();
                }
                var marker = root.GetComponent<SpaceNavigationMarker>();
                if (marker)
                {
                    if (!marker.arrow.GetComponent<CanvasRenderer>()) marker.arrow.gameObject.AddComponent<CanvasRenderer>();
                    marker.arrow.sizeDelta = new Vector2(56, 56);
                    marker.distanceText.rectTransform.sizeDelta = new Vector2(160, 40); marker.distanceText.fontSize = 25;
                }
                var cargo = root.GetComponent<CargoMissionUI>();
                if (cargo) PlaceComment(((TMP_Text)new SerializedObject(cargo).FindProperty("status").objectReferenceValue).rectTransform);
                var astronaut = root.GetComponent<AstronautMoonController>();
                if (astronaut)
                {
                    var data = new SerializedObject(astronaut); data.FindProperty("touchWalkDistance").floatValue = .015f;
                    data.FindProperty("touchSprintDistance").floatValue = .1f; data.FindProperty("minimumWalkMultiplier").floatValue = .6f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets(); Debug.Log("HUD polished: upper-third side text, rendered arrows, short distances and compact walking input range.");
    }
    static void PlaceComment(RectTransform rect)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 2f / 3);
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(900, 180);
    }
    public static void FitFlightWarning()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        var parent = (RectTransform)ui.warningTitle.transform.parent;
        foreach (var fitter in parent.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
        foreach (var layout in parent.GetComponents<LayoutGroup>()) Object.DestroyImmediate(layout);
        foreach (var text in new[] { ui.warningTitle, ui.warningMessage })
        {
            foreach (var fitter in text.GetComponents<ContentSizeFitter>()) Object.DestroyImmediate(fitter);
            foreach (var element in text.GetComponents<LayoutElement>()) Object.DestroyImmediate(element);
            text.raycastTarget = false;
        }
        parent.anchorMin = new Vector2(.08f, 2f / 3); parent.anchorMax = new Vector2(.92f, 2f / 3);
        parent.pivot = new Vector2(.5f, .5f); parent.anchoredPosition = Vector2.zero; parent.sizeDelta = new Vector2(0, 260);
        var title = ui.warningTitle.rectTransform;
        title.anchorMin = new Vector2(0, 1); title.anchorMax = Vector2.one; title.pivot = new Vector2(.5f, 1);
        title.anchoredPosition = Vector2.zero; title.sizeDelta = new Vector2(0, 160);
        ui.warningTitle.textWrappingMode = TextWrappingModes.Normal;
        ui.warningTitle.enableAutoSizing = true; ui.warningTitle.fontSizeMin = 26; ui.warningTitle.fontSizeMax = 42;
        ui.warningTitle.overflowMode = TextOverflowModes.Ellipsis; ui.warningTitle.alignment = TextAlignmentOptions.Center;
        var counter = ui.warningMessage.rectTransform;
        counter.anchorMin = Vector2.zero; counter.anchorMax = new Vector2(1, 0); counter.pivot = new Vector2(.5f, 0);
        counter.anchoredPosition = Vector2.zero; counter.sizeDelta = new Vector2(0, 80);
        ui.warningMessage.fontSize = 56; ui.warningMessage.alignment = TextAlignmentOptions.Center;
        ui.warningTitle.SetLocalizedText("[[eva.zerog.warning]]"); ui.warningMessage.SetLocalizedText("");
        ui.warningTitle.gameObject.SetActive(false); ui.warningMessage.gameObject.SetActive(false);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("Flight warning constrained to screen width with wrapping, margins and separate countdown.");
    }
    static void RepairResidents()
    {
        foreach (var path in Directory.GetFiles("Assets/Prefabs", "*.prefab", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset || asset.GetComponentsInChildren<StationResident>(true).Length == 0) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var resident in root.GetComponentsInChildren<StationResident>(true))
                {
                    var data = new SerializedObject(resident); var text = data.FindProperty("bubbleText").objectReferenceValue as TMP_Text;
                    if (!text)
                    {
                        var bubble = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StationWorldText.prefab"), resident.transform);
                        text = bubble.GetComponentInChildren<TMP_Text>(true); data.FindProperty("bubbleText").objectReferenceValue = text;
                    }
                    text.SetLocalizedText(""); text.fontSize = 30;
                    text.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 130);
                    text.gameObject.SetActive(false);
                    if (data.FindProperty("comments").arraySize == 0)
                    {
                        var comments = data.FindProperty("comments"); comments.arraySize = 3;
                        for (int i = 0; i < 3; i++) comments.GetArrayElementAtIndex(i).stringValue = "[[station.ambient." + i + "]]";
                    }
                    data.FindProperty("commentDistance").floatValue = 4;
                    data.FindProperty("commentHideDistance").floatValue = 6;
                    data.FindProperty("commentDuration").floatValue = 4.5f;
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    public static void PreviewReadablePanel()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("Panel preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var camera = new GameObject("Panel preview camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .12f, .12f);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        for (int i = 0; i < 5; i++)
        {
            var stripe = Rect(root.transform, "World line", new Vector2(2000, 5), new Vector2(0, (i - 2) * 220));
            stripe.gameObject.AddComponent<Image>().color = Color.white;
            stripe.localRotation = Quaternion.Euler(0, 0, 25);
        }
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Panels/ResultPanel.prefab"), root.transform);
        var result = panel.GetComponent<ResultPanel>();
        result.title.SetLocalizedText("Sicher gelandet"); result.message.SetLocalizedText("Außenposten erreicht.\nZeit, nach der Fracht zu sehen.");
        result.score.gameObject.SetActive(false); result.totalScore.gameObject.SetActive(true);
        result.totalScore.SetLocalizedText("XP-PUNKTE 1250");
        result.nextLevelButton.gameObject.SetActive(false); result.refillButton.gameObject.SetActive(false);
        result.chooseShipButton.gameObject.SetActive(false); result.continueButton.gameObject.SetActive(false);
        result.retryButton.gameObject.SetActive(false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
        panel.SetActive(true);
        var target = new RenderTexture(1080, 1920, 24); camera.targetTexture = target;
        scaler.SendMessage("Handle"); Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rect); Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
        Directory.CreateDirectory("Documentation/SpaceExperience"); File.WriteAllBytes("Documentation/SpaceExperience/PanelReadability.png", pixels.EncodeToPNG());
        Debug.Log("Static panel bounds: " + rect.rect + "; no Play Mode executed.");
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
    }
    public static void PreviewFlightWarning()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var ui = Object.FindFirstObjectByType<LanderUI>();
        var root = new GameObject("Warning preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.layer = 5;
        var camera = new GameObject("Warning preview camera").AddComponent<Camera>();
        camera.orthographic = true; camera.cullingMask = 1 << 5; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
        var warning = Object.Instantiate(ui.warningTitle.transform.parent.gameObject, root.transform, false);
        foreach (var text in warning.GetComponentsInChildren<TMP_Text>(true))
        {
            text.gameObject.SetActive(true); text.color = Color.red;
            text.SetLocalizedText(text.name == ui.warningTitle.name ? "Kein Halt!\nZurück zur Gravitation." : "5");
        }
        var target = new RenderTexture(1080, 1920, 24); camera.targetTexture = target;
        scaler.SendMessage("Handle"); Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        var pixels = new Texture2D(1080, 1920, TextureFormat.RGB24, false); RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); pixels.Apply();
        Directory.CreateDirectory("Documentation/SpaceExperience"); File.WriteAllBytes("Documentation/SpaceExperience/ZeroGWarning.png", pixels.EncodeToPNG());
        Debug.Log("Static warning bounds: " + warning.GetComponent<RectTransform>().rect + "; no Play Mode executed.");
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        Object.DestroyImmediate(pixels); Object.DestroyImmediate(target); Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
    }
    static void MoonSurface(GameObject moon)
    {
        var collider = moon.GetComponent<PolygonCollider2D>();
        float radius = Mathf.Max(7.4f, collider.points.Average(p => p.magnitude));
        var points = new Vector2[640]; float[] craterAngles = { 72, 130, 215, 325 };
        for (int i = 0; i < points.Length; i++)
        {
            float degrees = i * 360f / points.Length;
            float r = radius + .035f * Mathf.Sin(degrees * Mathf.Deg2Rad * 13);
            foreach (float center in craterAngles)
            {
                float t = Mathf.Abs(Mathf.DeltaAngle(degrees, center)) / 13;
                if (t < 1) r -= .42f * (.5f + .5f * Mathf.Cos(t * Mathf.PI));
                else if (t < 1.3f) r += .07f * Mathf.Sin((t - 1) / .3f * Mathf.PI);
            }
            float a = degrees * Mathf.Deg2Rad; points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        collider.SetPath(0, points); OutlineWorldAuthoring.BuildMoon(moon);
        var surface = moon.transform.Find("DetailedSurface");
        for (int i = 0; i < 18; i++)
        {
            float a = (i * 20 + 8) * Mathf.Deg2Rad;
            Vector2 center = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (.79f + i % 3 * .035f);
            OutlineWorldAuthoring.Crater(surface, "EdgeCrater" + i, center, Vector2.one * (.14f + i % 3 * .05f), .025f, -10);
        }
        for (int i = 0; i < 24; i++)
        {
            int index = (i * 640 / 24 + 17) % 640;
            var up = points[index].normalized;
            var stone = new GameObject("SurfaceStone" + i).transform; stone.SetParent(surface, false);
            stone.localPosition = points[index]; stone.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90);
            float size = .07f + i % 3 * .025f;
            OutlineWorldAuthoring.Stroke(stone, "StoneOutline", new[] { new Vector2(-size, 0), new Vector2(-size * .6f, size), new Vector2(size * .2f, size * 1.2f), new Vector2(size, 0) }, .018f, -7);
            if (i % 3 == 0)
            {
                var hill = new GameObject("BackgroundHill" + i).transform; hill.SetParent(stone, false);
                Vector2[] ridge = { new(-.65f, -.06f), new(-.35f, .3f), new(-.1f, .45f), new(.15f, .28f), new(.7f, -.06f) };
                OutlineWorldAuthoring.Body(hill, "MoonBackgroundHill" + i, ridge, -15);
                OutlineWorldAuthoring.Stroke(hill, "Ridge", ridge, .025f, -14, false);
            }
        }
        var flag = new GameObject("MoonFlag").transform; flag.SetParent(surface, false);
        int flagIndex = 640 * 110 / 360;
        flag.localPosition = points[flagIndex]; flag.localRotation = Quaternion.Euler(0, 0, 110 - 90); flag.gameObject.AddComponent<MoonFlagDiscovery>();
        OutlineWorldAuthoring.Stroke(flag, "Pole", new Vector2[] { new(0, 0), new(0, .75f) }, .025f, -6, false);
        OutlineWorldAuthoring.Stroke(flag, "FlagOutline", new Vector2[] { new(0, .75f), new(.42f, .68f), new(.4f, .42f), new(0, .49f) }, .022f, -6);
        PrefabUtility.SaveAsPrefabAsset(surface.gameObject, "Assets/Prefabs/World/MoonSurfaceArt.prefab");
    }

    public static void FinalizeAndPreview()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var moon = Object.FindFirstObjectByType<GravityManager2D>().transform;
        var flag = moon.Find("DetailedSurface/MoonFlag");
        if (flag)
        {
            var points = moon.GetComponent<PolygonCollider2D>().GetPath(0);
            flag.localPosition = points[points.Length * 110 / 360]; flag.localRotation = Quaternion.Euler(0, 0, 20);
            PrefabUtility.SaveAsPrefabAsset(moon.Find("DetailedSurface").gameObject, "Assets/Prefabs/World/MoonSurfaceArt.prefab");
        }
        EditorSceneManager.SaveScene(scene);
        StoryImport.Build();
        var project = AssetDatabase.LoadAssetAtPath<StoryProject>(StoryImport.ProjectPath);
        var discovery = project.Find("game.oh.so.that.is.the.moon.minor.naming.issue");
        if (discovery != null && discovery.title.StartsWith("Da ist")) discovery.title = "Mond entdecken";
        if (project.Find("moon.flag.discovery") == null)
            project.nodes.Add(new StoryNode { id = "moon.flag.discovery", title = "Flagge: jemand war schon hier", chapter = project.ResolveChapter("Mond"),
                storyStepId = "overview.3", textKey = "moon.flag.discovery", sourceReference = "[[moon.flag.discovery]]", sourcePath = "Assets/Scripts/MoonFlagDiscovery.cs",
                sourceProperty = "text", position = new Vector2(400, 1800), status = StoryDevelopmentStatus.Implemented,
                purpose = "Einmalige beiläufige Bemerkung beim Vorbeilaufen; kein Dialog, keine Pause. Speichert story.moonFlagSeen." });
        EditorUtility.SetDirty(project); AssetDatabase.SaveAssets();
        var camera = new GameObject("Static lunar preview").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        Directory.CreateDirectory("Documentation/SpaceExperience");
        foreach (bool close in new[] { false, true })
        {
            camera.orthographicSize = close ? 10 : 30;
            camera.transform.position = moon.position + new Vector3(0, close ? 20 : 0, -100);
            var target = new RenderTexture(1000, 1000, 24); var pixels = new Texture2D(1000, 1000, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1000, 1000), 0, 0); pixels.Apply();
            File.WriteAllBytes("Documentation/SpaceExperience/" + (close ? "MoonSurface.png" : "MoonOverview.png"), pixels.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
        }
        Object.DestroyImmediate(camera.gameObject);
        Debug.Log("Story sources supplemented and static lunar previews rendered; no Play Mode executed.");
    }
}
