using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
public class RandomLandscape : MonoBehaviour
{
    public static RandomLandscape Instance;
    public enum TerrainType { Automatic, Hills, Craters, MountainRanges }

    [Header("Shape")]
    [Tooltip("Number of terrain vertices. More points give smoother curves but larger colliders.")]
    [Min(4)] public int points = 80;
    [Min(1f)] public float width = 40f;
    public float baseY = -6f;
    public float amplitude = 3f;
    [FormerlySerializedAs("noiseScale")]
    [Tooltip("Higher values produce shorter hills. Detail is limited by vertex spacing.")]
    [Min(0.001f)] public float terrainFrequency = 0.12f;
    public int seed;
    [Tooltip("Reuse this seed for new rounds. Level and terrain settings also affect the result.")]
    public bool lockSeed;
    public TerrainType terrainType = TerrainType.Automatic;

    [Header("Landing Zone")]
    [FormerlySerializedAs("padClearRadius")]
    [Tooltip("Flat zone half-width at maximum difficulty. Automatically enlarged to fit the pad.")]
    [Min(1f)] public float landingZoneHalfWidth = 3f;
    [FormerlySerializedAs("padFlatStrength")]
    [Tooltip("Width of the smooth shoulders around the flat zone: 0 = narrow, 1 = wide.")]
    [Range(0f, 1f)] public float landingTransition = 1f;

    [Header("Terrain Features")]
    [FormerlySerializedAs("mountainChance")]
    [Tooltip("Controls feature spacing rather than a probability per terrain vertex.")]
    [Range(0f, 0.25f)] public float featureDensity = 0.10f;
    [FormerlySerializedAs("mountainHeight")]
    [Min(0f)] public float featureHeight = 2.5f;
    [FormerlySerializedAs("mountainWidth")]
    [Min(1f)] public float featureWidth = 8f;
    [Tooltip("Peak profile for Mountain Ranges only; hills and craters use rounded profiles.")]
    public AnimationCurve mountainShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Editor Preview")]
    public bool livePreviewInEditor = true;

    public Vector2 LandingZone { get; private set; }
    public float LandingHalfWidth { get; private set; }
    public TerrainType GeneratedType { get; private set; }
    public int GeneratedLevel { get; private set; }
    public bool HasLayout => terrainPoints != null && terrainPoints.Length >= 4;

    LineRenderer line;
    EdgeCollider2D edge;
    Vector3[] terrainPoints;
#if UNITY_EDITOR
    bool previewPending;
#endif

    void Awake() => Instance = this;
    public void Init() => CacheReferences();

    void CacheReferences()
    {
        if (!line) line = GetComponent<LineRenderer>();
        if (!edge) edge = GetComponent<EdgeCollider2D>();
        if (line) line.useWorldSpace = true;
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (!livePreviewInEditor || previewPending) return;
        previewPending = true;
        // Renderer and collider updates must run outside Unity's validation callback.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (!this) return;
            previewPending = false;
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || !livePreviewInEditor) return;
            var game = FindFirstObjectByType<GameController>();
            if (game) GenerateFromSeed(seed, game.level);
        };
#endif
    }

    public void GenerateNewLevel()
    {
        int nextSeed = lockSeed ? seed : UnityEngine.Random.Range(1, int.MaxValue);
        var game = GameController.Instance ? GameController.Instance : FindFirstObjectByType<GameController>();
        GenerateFromSeed(nextSeed, game ? game.level : 1);
    }

    // The same seed, level and Inspector settings reproduce the complete terrain and landing zone.
    public void GenerateFromSeed(int layoutSeed, int level)
    {
        CacheReferences();
        if (!line || !edge) return;
        seed = layoutSeed;
        GeneratedLevel = Mathf.Max(1, level);
        float difficulty = Mathf.Clamp01((GeneratedLevel - 1f) / 29f);
        GeneratedType = terrainType == TerrainType.Automatic
            ? (TerrainType)(1 + ((GeneratedLevel - 1) / 3) % 3) : terrainType;
        var random = new System.Random(seed);
        int count = Mathf.Clamp(points, 4, 10000);
        float span = Mathf.Max(1f, width);
        float step = span / (count - 1);
        float startX = transform.position.x - span * 0.5f;
        var pad = LandingPadPlacer.Instance ? LandingPadPlacer.Instance : FindFirstObjectByType<LandingPadPlacer>();
        float requiredHalfWidth = pad ? pad.GetRequiredGroundHalfWidth() : 1f;
        float minimumZone = Mathf.Max(requiredHalfWidth + step, step * 2f);
        LandingHalfWidth = Mathf.Max(minimumZone, Mathf.Max(1f, landingZoneHalfWidth) * Mathf.Lerp(1.8f, 1f, difficulty));
        float shoulder = Mathf.Max(step * 2f, LandingHalfWidth * Mathf.Lerp(1.5f, 2.5f, landingTransition));
        float range = pad ? Mathf.Clamp(pad.padSpawnRange, 0.1f, 1f) : 0.5f;
        int margin = Mathf.Min((count - 1) / 2, Mathf.CeilToInt((LandingHalfWidth + shoulder) / step));
        int first = Mathf.Clamp(Mathf.CeilToInt((count - 1) * (1f - range) * 0.5f), margin, count - 1 - margin);
        int last = Mathf.Clamp(Mathf.FloorToInt((count - 1) * (1f + range) * 0.5f), first, count - 1 - margin);
        int zoneIndex = random.Next(first, last + 1);
        float zoneX = startX + zoneIndex * step;
        float height = Mathf.Max(0f, amplitude) * Mathf.Lerp(1f, 2f, difficulty);
        float wavelength = Mathf.Max(step * 6f, 6f / Mathf.Max(0.001f, terrainFrequency));
        float phase = (float)random.NextDouble() * Mathf.PI * 2f;

        terrainPoints = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            float x = startX + i * step;
            float broad = 0.5f + 0.5f * Mathf.Sin((x - startX) * Mathf.PI * 2f / wavelength + phase);
            float detail = Mathf.Sin((x - startX) * Mathf.PI * 2f / (wavelength * 0.43f) + phase * 1.7f);
            terrainPoints[i] = new Vector3(x, transform.position.y + baseY + height * (0.2f + 0.65f * broad + 0.15f * detail), transform.position.z);
        }
        BuildTerrainFeatures(random, difficulty, step, span);
        float zoneY = terrainPoints[zoneIndex].y;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = terrainPoints[i];
            float distance = Mathf.Abs(point.x - zoneX);
            float transition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LandingHalfWidth, LandingHalfWidth + shoulder, distance));
            point.y = Mathf.Lerp(zoneY, point.y, transition);
            terrainPoints[i] = point;
        }
        LandingZone = new Vector2(zoneX, zoneY);
        ApplyTerrain();
    }

    void BuildTerrainFeatures(System.Random random, float difficulty, float step, float span)
    {
        float radiusScale = Mathf.Max(step * 6f, Mathf.Max(1f, featureWidth) * (GeneratedType == TerrainType.Craters ? 4f : 3f));
        float spacing = radiusScale * Mathf.Lerp(2.5f, 1.25f, Mathf.Clamp01(featureDensity * 4f + difficulty * 0.3f));
        int featureCount = Mathf.Clamp(Mathf.CeilToInt(span / spacing), 1, 256);
        float strength = Mathf.Max(0f, featureHeight) * Mathf.Lerp(0.6f, 1.4f, difficulty);
        float startX = terrainPoints[0].x;
        float shapeLow = mountainShape != null ? mountainShape.Evaluate(0f) : 0f;
        float shapeHigh = mountainShape != null ? mountainShape.Evaluate(1f) : 1f;
        for (int feature = 0; feature < featureCount; feature++)
        {
            float center = startX + span * (feature + 0.2f + (float)random.NextDouble() * 0.6f) / featureCount;
            float radius = radiusScale * (0.7f + (float)random.NextDouble() * 0.6f);
            float peak = strength * (0.65f + (float)random.NextDouble() * 0.35f);
            for (int i = 0; i < terrainPoints.Length; i++)
            {
                float distance = Mathf.Abs(terrainPoints[i].x - center) / radius;
                if (distance >= 1f) continue;
                float bell = 0.5f + 0.5f * Mathf.Cos(distance * Mathf.PI);
                if (GeneratedType == TerrainType.Craters)
                {
                    float rim = Mathf.Exp(-Mathf.Pow((distance - 0.72f) / 0.15f, 2f));
                    terrainPoints[i].y += peak * (rim * 0.45f - bell * 0.7f);
                }
                else
                {
                    if (GeneratedType == TerrainType.MountainRanges && Mathf.Abs(shapeHigh - shapeLow) > 0.0001f)
                        bell = Mathf.Clamp01((mountainShape.Evaluate(bell) - shapeLow) / (shapeHigh - shapeLow));
                    terrainPoints[i].y += peak * bell * (GeneratedType == TerrainType.Hills ? 0.35f : 1f);
                }
            }
        }
    }

    void ApplyTerrain()
    {
        line.positionCount = terrainPoints.Length;
        line.SetPositions(terrainPoints);
        var colliderPoints = new Vector2[terrainPoints.Length];
        for (int i = 0; i < colliderPoints.Length; i++)
            colliderPoints[i] = transform.InverseTransformPoint(terrainPoints[i]);
        edge.points = colliderPoints;
        Physics2D.SyncTransforms();
    }

    public void CaptureWorld(WorldSave world)
    {
        world.seed = seed;
        world.terrainLevel = GeneratedLevel;
        world.terrainType = GeneratedType;
        world.terrain = (Vector3[])terrainPoints.Clone();
        world.landingZone = LandingZone;
        world.landingHalfWidth = LandingHalfWidth;
    }

    public void RestoreWorld(WorldSave world)
    {
        CacheReferences();
        seed = world.seed;
        GeneratedLevel = world.terrainLevel;
        GeneratedType = world.terrainType;
        LandingZone = world.landingZone;
        LandingHalfWidth = world.landingHalfWidth;
        terrainPoints = (Vector3[])world.terrain.Clone();
        ApplyTerrain();
    }

    public float GetHighestGround(float fromX, float toX)
    {
        if (!HasLayout) return transform.position.y + baseY;
        float step = terrainPoints[1].x - terrainPoints[0].x;
        float start = terrainPoints[0].x;
        int first = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(fromX, toX) - start) / step), 0, terrainPoints.Length - 1);
        int last = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(fromX, toX) - start) / step), first, terrainPoints.Length - 1);
        float highest = float.NegativeInfinity;
        for (int i = first; i <= last; i++) highest = Mathf.Max(highest, terrainPoints[i].y);
        return highest;
    }

    public float ClampSpawnX(float x, float margin)
    {
        if (!HasLayout) return x;
        float inset = Mathf.Min(Mathf.Max(0f, margin), (terrainPoints[^1].x - terrainPoints[0].x) * 0.45f);
        return Mathf.Clamp(x, terrainPoints[0].x + inset, terrainPoints[^1].x - inset);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
