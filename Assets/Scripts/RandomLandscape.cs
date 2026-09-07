using UnityEngine;

[RequireComponent(typeof(LineRenderer), typeof(EdgeCollider2D))]
public class RandomLandscape : MonoBehaviour
{
    public static RandomLandscape Instance;

    [Header("Shape")]
    [Min(4)] public int points = 80;
    public float width = 40f;
    public float baseY = -6f;
    public float amplitude = 3f;
    public float noiseScale = 0.12f;
    public int seed;

    [Header("Landing Pad Freihalten (optional)")]
    public float padClearRadius = 3f;
    [Range(0f, 1f)] public float padFlatStrength = 1f;

    [Header("Outlier Mountains (breite Berge)")]
    [Range(0f, 1f)] public float mountainChance = 0.10f;
    public float mountainHeight = 2.5f;
    public float mountainWidth = 8f;
    public AnimationCurve mountainShape = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Editor Preview")]
    public bool livePreviewInEditor = true;

    LineRenderer lineRenderer;
    EdgeCollider2D edgeCollider;
    Transform landingPad;
    float noiseOffset;

    void Awake() => Instance = this;

    public void Init()
    {
        CacheReferences();
    }

    void OnValidate()
    {
        if (!livePreviewInEditor || Application.isPlaying) return;

        CacheReferences();
        if (!lineRenderer || !edgeCollider) return;

        Generate(firstTime: true, forceNewSeed: false);
    }

    public void GenerateNewLevel()
        => Generate(firstTime: false, forceNewSeed: true);

    void CacheReferences()
    {
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        if (!edgeCollider) edgeCollider = GetComponent<EdgeCollider2D>();

        if (lineRenderer) lineRenderer.useWorldSpace = true;
        landingPad = LandingPadPlacer.Instance ? LandingPadPlacer.Instance.transform : landingPad;
    }

    void Generate(bool firstTime, bool forceNewSeed)
    {
        CacheReferences();
        if (!lineRenderer) return;

        if (forceNewSeed)
            seed = Random.Range(1, 999999);
        else if (firstTime && seed == 0)
            seed = Random.Range(1, 999999);

        noiseOffset = seed * 0.001f;
        lineRenderer.positionCount = Mathf.Max(4, points);

        int pointCount = lineRenderer.positionCount;
        float xStart = -width * 0.5f;
        float step = width / (pointCount - 1);
        float levelFactor = GameController.Instance ? GameController.Instance.level / 50f : 0f;

        float amp = amplitude * (1f + levelFactor);
        float mountainChanceCurrent = mountainChance * (1f + levelFactor);
        float mountainHeightCurrent = mountainHeight * (1f + levelFactor);
        float mountainWidthCurrent = mountainWidth * (1f + levelFactor);

        float[] mountainAdd = BuildMountainOffsets(
            pointCount,
            step,
            mountainChanceCurrent,
            mountainHeightCurrent,
            mountainWidthCurrent);

        float padX = landingPad ? landingPad.position.x : float.NaN;
        float padY = landingPad ? landingPad.position.y : 0f;

        for (int i = 0; i < pointCount; i++)
        {
            float x = xStart + step * i;
            float noise = Mathf.PerlinNoise(noiseOffset + x * noiseScale, noiseOffset);
            float y = baseY + noise * amp + noise * mountainAdd[i];

            if (landingPad)
            {
                float distance = Mathf.Abs(x - padX);
                if (distance < padClearRadius)
                {
                    float blend = 1f - Mathf.Clamp01(distance / Mathf.Max(0.0001f, padClearRadius));
                    y = Mathf.Lerp(y, padY, blend * padFlatStrength);
                }
            }

            lineRenderer.SetPosition(i, new Vector3(x, y, 0f));
        }

        UpdateCollider();
    }

    float[] BuildMountainOffsets(int pointCount, float step, float chance, float height, float worldWidth)
    {
        float[] offsets = new float[pointCount];
        int halfWidth = Mathf.Max(1, Mathf.RoundToInt((worldWidth / Mathf.Max(0.0001f, step)) * 0.5f));
        halfWidth = Mathf.Clamp(halfWidth, 1, pointCount / 2);

        Random.State previousState = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < pointCount; i++)
        {
            if (Random.value > chance) continue;

            for (int k = -halfWidth; k <= halfWidth; k++)
            {
                int index = i + k;
                if (index < 0 || index >= pointCount) continue;

                float t = Mathf.InverseLerp(-halfWidth, halfWidth, k);
                float center = 1f - Mathf.Abs(2f * t - 1f);
                offsets[index] += height * mountainShape.Evaluate(center);
            }

            i += halfWidth;
        }

        Random.state = previousState;
        return offsets;
    }

    void UpdateCollider()
    {
        if (!edgeCollider || !lineRenderer) return;

        Vector2[] colliderPoints = new Vector2[lineRenderer.positionCount];
        for (int i = 0; i < colliderPoints.Length; i++)
        {
            Vector3 point = lineRenderer.GetPosition(i);
            colliderPoints[i] = point;
        }

        edgeCollider.points = colliderPoints;
    }
}
