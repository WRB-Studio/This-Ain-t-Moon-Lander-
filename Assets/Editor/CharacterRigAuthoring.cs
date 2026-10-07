using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class CharacterRigAuthoring
{
    const string AtlasPath = "Assets/Images/Characters/AstronautParts.png";
    const string BasePath = "Assets/Prefabs/Characters/BipedRig.prefab";
    const string VisualPath = "Assets/Prefabs/Characters/AstronautVisual.prefab";
    const string AnimationFolder = "Assets/Animations/Characters";
    const string PreviewPath = "Assets/Scenes/CharacterPreview.unity";
    const float WalkDuration = 0.96f;
    const float ThighLength = 0.4f;
    const float ShinLength = 0.4f;
    const float AnkleY = -1.07f;
    const float LegOffsetX = -0.04f;

    // Atlas coordinates use a top-left origin; joint coordinates refer to the source image.
    struct Part
    {
        public string name;
        public Rect rect;
        public Vector2 joint;
        public Part(string name, int x, int y, int width, int height, float jointX, float jointY)
        {
            this.name = name;
            rect = new Rect(x, y, width, height);
            joint = new Vector2(jointX, jointY);
        }
    }

    static readonly Part[] Parts =
    {
        new("Helmet", 38, 161, 260, 249, 167, 390),
        new("Torso", 380, 193, 206, 203, 484, 379),
        new("Pelvis", 667, 251, 236, 151, 785, 295),
        new("Backpack", 999, 173, 201, 238, 1100, 292),
        new("UpperArm", 104, 495, 132, 265, 165, 550),
        new("Forearm", 418, 509, 147, 250, 468, 551),
        new("Hand", 725, 560, 151, 181, 781, 590),
        new("Elbow", 1035, 570, 121, 124, 1095, 630),
        new("Thigh", 85, 807, 165, 287, 148, 859),
        new("Shin", 418, 819, 115, 275, 475, 872),
        new("Boot", 678, 895, 214, 176, 739, 934),
        new("Knee", 1035, 912, 120, 120, 1095, 970)
    };

    [MenuItem("Tools/Characters/Rebuild Astronaut Rig and Preview")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        ImportParts();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AtlasPath).OfType<Sprite>().ToDictionary(s => s.name);
        var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Astronaut.prefab");
        var material = oldPrefab.GetComponentInChildren<SpriteRenderer>().sharedMaterial;
        var idle = MakeClip("BipedIdle", 2.4f, false);
        var walk = MakeClip("BipedWalk", WalkDuration, true);
        var controller = MakeController(idle, walk);
        var root = MakeRig(sprites, material, controller);
        idle.SampleAnimation(root.transform.Find("Facing/Rig").gameObject, 0f);
        var basePrefab = PrefabUtility.SaveAsPrefabAsset(root, BasePath);
        UnityEngine.Object.DestroyImmediate(root);

        var variant = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        variant.name = "AstronautVisual";
        var visualPrefab = PrefabUtility.SaveAsPrefabAsset(variant, VisualPath);
        UnityEngine.Object.DestroyImmediate(variant);
        UpdateAstronaut(visualPrefab);
        AssetDatabase.SaveAssets();
        MakePreviewScene(visualPrefab);
        AssetDatabase.SaveAssets();
        ExportFrames(visualPrefab, idle, walk);
        Debug.Log("Character authoring complete: shared rig, astronaut variant, idle/walk clips and preview scene.");
    }

    [MenuItem("Tools/Characters/Update Animations and Export Preview")]
    public static void UpdateAnimations()
    {
        var idle = MakeClip("BipedIdle", 2.4f, false);
        var walk = MakeClip("BipedWalk", WalkDuration, true);
        AssetDatabase.SaveAssets();
        ExportFrames(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath), idle, walk);
        Debug.Log("Character animations updated: heel strike, toe roll and delayed upper-body motion.");
    }

    static void ImportParts()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var previousIds = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = Parts.Select(p => new SpriteRect
        {
            name = p.name,
            rect = new Rect(p.rect.x, texture.height - p.rect.yMax, p.rect.width, p.rect.height),
            alignment = SpriteAlignment.Custom,
            pivot = new Vector2((p.joint.x - p.rect.x) / p.rect.width, (p.rect.yMax - p.joint.y) / p.rect.height),
            spriteID = previousIds.TryGetValue(p.name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }

    static GameObject MakeRig(Dictionary<string, Sprite> sprites, Material material, RuntimeAnimatorController controller)
    {
        var root = new GameObject("BipedRig");
        root.AddComponent<SortingGroup>();
        var facing = Bone(root.transform, "Facing", Vector2.zero);
        var rig = Bone(facing, "Rig", Vector2.zero);
        var animator = rig.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        var visual = root.AddComponent<CharacterVisual>();
        var serialized = new SerializedObject(visual);
        serialized.FindProperty("facingRoot").objectReferenceValue = facing;
        serialized.FindProperty("animator").objectReferenceValue = animator;
        serialized.FindProperty("referenceWalkSpeed").floatValue = 2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var hips = Bone(rig, "Hips", new Vector2(0f, -0.23f));
        Art(hips, "Pelvis", 0.28f, 0f, 8);
        var spine = Bone(hips, "Spine", new Vector2(0f, 0.14f));
        Art(spine, "Torso", 0.315f, 0f, 10);
        var head = Bone(spine, "Head", new Vector2(0.02f, 0.56f));
        Art(head, "Helmet", 0.35f, 0f, 13);
        var backpack = Bone(spine, "Backpack", new Vector2(-0.38f, 0.29f));
        Art(backpack, "Backpack", 0.242f, 0f, 2);

        MakeArm("ArmFar", new Vector2(-0.10f, 0.43f), 3, 0.6f);
        MakeLeg("LegFar", new Vector2(-0.065f + LegOffsetX, -0.07f), 0, 0.6f);
        MakeLeg("LegNear", new Vector2(0.075f + LegOffsetX, -0.07f), 9, 1f);
        MakeArm("ArmNear", new Vector2(0.075f, 0.43f), 20, 1f);
        return root;

        void Art(Transform parent, string part, float scale, float angle, int order, float brightness = 1f)
        {
            var art = Bone(parent, "Art", Vector2.zero);
            art.localScale = Vector3.one * scale;
            art.localRotation = Quaternion.Euler(0f, 0f, angle);
            var renderer = art.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprites[part];
            renderer.sharedMaterial = material;
            renderer.sortingOrder = order;
            renderer.color = new Color(brightness, brightness, brightness, 1f);
        }

        void MakeArm(string name, Vector2 position, int order, float brightness)
        {
            var arm = Bone(spine, name, position);
            Art(arm, "UpperArm", 0.20f, -2.44f, order, brightness);
            var forearm = Bone(arm, "Forearm", new Vector2(0f, -0.33f));
            Art(forearm, "Forearm", 0.174f, -14.58f, order + 1, brightness);
            var elbow = Bone(forearm, "Elbow", Vector2.zero);
            Art(elbow, "Elbow", 0.15f, 0f, order + 2, brightness);
            var hand = Bone(forearm, "Hand", new Vector2(0f, -0.31f));
            Art(hand, "Hand", 0.14f, 0f, order + 3, brightness);
        }

        void MakeLeg(string name, Vector2 position, int order, float brightness)
        {
            var thigh = Bone(hips, name, position);
            Art(thigh, "Thigh", 0.21f, -16.81f, order, brightness);
            var shin = Bone(thigh, "Shin", new Vector2(0f, -ThighLength));
            Art(shin, "Shin", 0.2162f, 0f, order + 1, brightness);
            var knee = Bone(shin, "Knee", Vector2.zero);
            Art(knee, "Knee", 0.17f, 0f, order + 2, brightness);
            var foot = Bone(shin, "Foot", new Vector2(0f, -ShinLength));
            Art(foot, "Boot", 0.17f, 0f, order + 3, brightness);
        }
    }

    static Transform Bone(Transform parent, string name, Vector2 position)
    {
        var bone = new GameObject(name).transform;
        bone.SetParent(parent, false);
        bone.localPosition = position;
        return bone;
    }

    static AnimationClip MakeClip(string name, float duration, bool walking)
    {
        string path = $"{AnimationFolder}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = 60f;
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.startTime = 0f;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        Curve("Hips", "m_LocalPosition.x", HipX);
        Curve("Hips", "m_LocalPosition.y", HipY);
        Rotation("Hips", HipsRotation);
        Rotation("Hips/Spine", SpineRotation);
        Curve("Hips/Spine", "m_LocalPosition.y", phase => walking ? 0.14f + 0.008f * Mathf.Sin((phase - 0.055f) * Mathf.PI * 4f) : 0.14f);
        Curve("Hips/Spine", "m_LocalScale.y", phase => walking ? 1f : 1f + 0.009f * Mathf.Sin(phase * Mathf.PI * 2f));
        Rotation("Hips/Spine/Head", phase => walking ? -SpineRotation(phase) - 0.8f * HipsRotation(phase)
            + 0.6f * Mathf.Sin((phase - 0.10f) * Mathf.PI * 2f) : -0.8f * Mathf.Sin(phase * Mathf.PI * 2f));
        Rotation("Hips/Spine/Backpack", phase => walking ? 2.1f * Mathf.Sin((phase - 0.075f) * Mathf.PI * 4f)
            + 0.6f * Mathf.Sin((phase - 0.045f) * Mathf.PI * 2f) : 0f);
        foreach (bool near in new[] { false, true })
        {
            string arm = "Hips/Spine/" + (near ? "ArmNear" : "ArmFar");
            float offset = near ? 0f : 0.5f;
            Curve(arm, "m_LocalPosition.x", phase => (near ? 0.075f : -0.10f)
                + (walking ? 0.008f * Mathf.Cos((phase + offset - 0.035f) * Mathf.PI * 2f) : 0f));
            Curve(arm, "m_LocalPosition.y", phase => walking ? 0.43f + 0.012f * Mathf.Sin((phase + offset - 0.035f) * Mathf.PI * 2f) : 0.43f);
            Rotation(arm, phase => walking ? -25f * Mathf.Cos((phase + offset - 0.055f) * Mathf.PI * 2f)
                - 2f * Mathf.Sin((phase + offset - 0.025f) * Mathf.PI * 4f)
                : (near ? 3f : -4f) + 1.2f * Mathf.Sin((phase + offset) * Mathf.PI * 2f));
            Rotation(arm + "/Forearm", phase => walking ? 23f - 10f * Mathf.Cos((phase + offset - 0.09f) * Mathf.PI * 2f) : 12f);
            Rotation(arm + "/Forearm/Hand", phase => walking ? -5f * Mathf.Sin((phase + offset - 0.12f) * Mathf.PI * 2f) : -2f);
            string leg = "Hips/" + (near ? "LegNear" : "LegFar");
            Rotation(leg, phase => LegPose(phase, near).x);
            Rotation(leg + "/Shin", phase => LegPose(phase, near).y);
            Rotation(leg + "/Shin/Foot", phase => LegPose(phase, near).z);
        }
        EditorUtility.SetDirty(clip);
        return clip;

        float HipY(float phase) => walking ? -0.395f + 0.10f * Mathf.Pow(Mathf.Sin(phase * Mathf.PI * 2f), 2f)
            + 0.006f * Mathf.Sin((phase - 0.02f) * Mathf.PI * 2f)
            : -0.23f + 0.003f * Mathf.Sin(phase * Mathf.PI * 2f);

        float HipX(float phase) => walking ? 0.012f * Mathf.Sin(phase * Mathf.PI * 4f) : 0f;

        float HipsRotation(float phase) => walking ? 2.2f * Mathf.Sin(phase * Mathf.PI * 2f)
            + 0.35f * Mathf.Sin(phase * Mathf.PI * 4f - 0.5f) : 0f;

        float SpineRotation(float phase) => walking ? -3f - 1.4f * Mathf.Sin((phase - 0.04f) * Mathf.PI * 2f)
            + 0.8f * Mathf.Cos((phase - 0.02f) * Mathf.PI * 4f) : 0.6f * Mathf.Sin(phase * Mathf.PI * 2f);

        Vector2 RollOffset(float pitch, Vector2 contact)
        {
            return contact - (Vector2)(Quaternion.Euler(0f, 0f, pitch) * new Vector3(contact.x, contact.y));
        }

        Vector3 LegPose(float phase, bool near)
        {
            float hipX = (near ? 0.075f : -0.065f) + LegOffsetX;
            float hipsRotation = HipsRotation(phase);
            Vector2 hip = (Vector2)(Quaternion.Euler(0f, 0f, hipsRotation) * new Vector3(hipX, -0.07f))
                + new Vector2(HipX(phase), HipY(phase));
            Vector2 foot = new((near ? 0.13f : -0.14f) + LegOffsetX, AnkleY);
            float footPitch = 0f;
            if (walking)
            {
                float legPhase = Mathf.Repeat(phase + (near ? 0f : 0.5f), 1f);
                Vector2 heel = new(-0.075f, -0.217f);
                Vector2 toe = new(0.24f, -0.217f);
                if (legPhase < 0.5f)
                {
                    foot.x = Mathf.Lerp(0.48f, -0.48f, legPhase * 2f);
                    float heelDown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.09f, legPhase));
                    float push = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.5f, legPhase));
                    footPitch = 12f * (1f - heelDown) - 24f * push;
                    // Compensate around the planted heel/toe so rolling keeps the contact point on the ground.
                    foot += RollOffset(footPitch, footPitch > 0f ? heel : toe);
                }
                else
                {
                    float swing = (legPhase - 0.5f) * 2f;
                    // Match the backward contact velocity at both ends of the forward swing.
                    foot.x = -0.48f - 0.96f * swing + 5.76f * swing * swing - 3.84f * swing * swing * swing;
                    foot.y += 0.21f * Mathf.Sin(swing * Mathf.PI);
                    foot += Vector2.Lerp(RollOffset(-24f, toe), RollOffset(12f, heel), Mathf.SmoothStep(0f, 1f, swing));
                    footPitch = swing < 0.35f ? Mathf.Lerp(-24f, 2f, Mathf.SmoothStep(0f, 1f, swing / 0.35f))
                        : Mathf.Lerp(2f, 12f, Mathf.SmoothStep(0f, 1f, (swing - 0.35f) / 0.65f));
                }
                foot.x += hipX;
            }
            // Bake two-bone IK into ordinary transform curves; no runtime solver is needed.
            Vector2 delta = foot - hip;
            float length = Mathf.Clamp(delta.magnitude, 0.01f, ThighLength + ShinLength - 0.001f);
            float knee = -Mathf.Acos(Mathf.Clamp((length * length - ThighLength * ThighLength - ShinLength * ShinLength)
                / (2f * ThighLength * ShinLength), -1f, 1f));
            float thigh = Mathf.Atan2(delta.x, -delta.y) - Mathf.Atan2(ShinLength * Mathf.Sin(knee), ThighLength + ShinLength * Mathf.Cos(knee));
            return new Vector3(thigh * Mathf.Rad2Deg - hipsRotation, knee * Mathf.Rad2Deg,
                footPitch - (thigh + knee) * Mathf.Rad2Deg);
        }

        void Rotation(string bone, Func<float, float> value) => Curve(bone, "localEulerAnglesRaw.z", value);

        void Curve(string bone, string property, Func<float, float> value)
        {
            const int samples = 64;
            var keys = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float phase = i / (float)samples;
                float before = value(Mathf.Repeat(phase - 1f / samples, 1f));
                float after = value(Mathf.Repeat(phase + 1f / samples, 1f));
                float slope = (after - before) * samples / (2f * duration);
                keys[i] = new Keyframe(phase * duration, value(phase == 1f ? 0f : phase), slope, slope);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bone, typeof(Transform), property), new AnimationCurve(keys));
        }
    }

    static AnimatorController MakeController(AnimationClip idle, AnimationClip walk)
    {
        string path = $"{AnimationFolder}/Biped.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = "IsWalking", type = AnimatorControllerParameterType.Bool },
            new AnimatorControllerParameter { name = "WalkSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f }
        };
        var machine = controller.layers[0].stateMachine;
        foreach (var state in machine.states) machine.RemoveState(state.state);
        var idleState = machine.AddState("Idle");
        idleState.motion = idle;
        idleState.writeDefaultValues = false;
        machine.defaultState = idleState;
        var walkState = machine.AddState("Walk");
        walkState.motion = walk;
        walkState.writeDefaultValues = false;
        walkState.speedParameter = "WalkSpeed";
        walkState.speedParameterActive = true;
        var toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.12f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsWalking");
        var toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.16f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsWalking");
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void UpdateAstronaut(GameObject visualPrefab)
    {
        const string path = "Assets/Prefabs/Astronaut.prefab";
        var astronaut = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var oldVisual = astronaut.transform.Find("VisualRoot");
            if (oldVisual) UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, astronaut.transform);
            visual.name = "VisualRoot";
            PrefabUtility.SaveAsPrefabAsset(astronaut, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(astronaut); }
    }

    static void MakePreviewScene(GameObject visualPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Preview Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 1.65f;
        camera.backgroundColor = Color.black;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        for (int i = 0; i < 3; i++)
        {
            var character = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, scene);
            character.name = i == 0 ? "Idle" : i == 1 ? "Walk Right" : "Walk Left";
            character.transform.position = new Vector3((i - 1) * 1.6f, 0f, 0f);
            character.transform.localScale = Vector3.one * 0.8f;
            var preview = character.AddComponent<CharacterAnimationPreview>();
            var serialized = new SerializedObject(preview);
            serialized.FindProperty("character").objectReferenceValue = character.GetComponent<CharacterVisual>();
            serialized.FindProperty("walking").boolValue = i > 0;
            serialized.FindProperty("faceLeft").boolValue = i == 2;
            serialized.FindProperty("worldSpeed").floatValue = 1.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene, PreviewPath);
    }

    [MenuItem("Tools/Characters/Export Animation Preview Frames")]
    public static void ExportPreview()
    {
        ExportFrames(AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath),
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationFolder}/BipedIdle.anim"),
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{AnimationFolder}/BipedWalk.anim"));
    }

    static void ExportFrames(GameObject visualPrefab, AnimationClip idle, AnimationClip walk)
    {
        string folder = Path.GetFullPath(Environment.GetEnvironmentVariable("CHARACTER_PREVIEW_OUTPUT") ?? "Temp/CharacterPreview");
        Directory.CreateDirectory(folder);
        var scene = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("Render Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.scene = scene;
        camera.orthographic = true;
        camera.orthographicSize = 1.55f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        var target = new RenderTexture(1024, 512, 24);
        camera.targetTexture = target;
        var pixels = new Texture2D(1024, 512, TextureFormat.RGB24, false);
        var characters = new GameObject[3];
        var rigs = new GameObject[3];
        for (int i = 0; i < characters.Length; i++)
        {
            characters[i] = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, scene);
            characters[i].transform.position = new Vector3((i - 1) * 1.75f, 0f, 0f);
            characters[i].transform.localScale = Vector3.one * 0.9f;
            characters[i].GetComponent<CharacterVisual>().FaceLeft(i == 2);
            rigs[i] = characters[i].transform.Find("Facing/Rig").gameObject;
            rigs[i].GetComponent<Animator>().enabled = false;
        }
        try
        {
            const int frameCount = 144;
            for (int frame = 0; frame < frameCount; frame++)
            {
                float time = frame / 30f;
                for (int i = 0; i < characters.Length; i++)
                {
                    var clip = i == 0 ? idle : walk;
                    clip.SampleAnimation(rigs[i], time % clip.length);
                }
                if (GraphicsSettings.currentRenderPipeline)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 512), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"frame-{frame:0000}.png"), pixels.EncodeToPNG());
            }
            Debug.Log($"Preview frames exported to {folder}");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
