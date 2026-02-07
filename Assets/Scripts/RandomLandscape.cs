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
    public int seed = 0; // 0 = einmal zufällig (beim ersten Generate)
    private EdgeCollider2D edge;

    [Header("Landing Pad Freihalten (optional)")]
    private Transform landingPad;
    public float padClearRadius = 3f;
    [Range(0f, 1f)] public float padFlatStrength = 1f;

    [Header("Outlier Mountains (breite Berge)")]
    [Range(0f, 1f)] public float mountainChance = 0.10f;  // Chance pro Segment einen Berg zu starten
    public float mountainHeight = 2.5f;                   // Zusatz-Amplitude (additiv)
    public float mountainWidth = 8f;                      // Breite in Welt-Einheiten
    public AnimationCurve mountainShape = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Editor Preview")]
    public bool livePreviewInEditor = true;

    LineRenderer lr;
    float noiseOffset;

    void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        edge = GetComponent<EdgeCollider2D>();

        landingPad = LandingPadPlacer.Instance.transform;
        Generate(firstTime: true, forceNewSeed: false);
    }

    void OnValidate()
    {
        if (!livePreviewInEditor) return;
        if (Application.isPlaying) return;

        if (!lr) lr = GetComponent<LineRenderer>();
        if (!lr) return;

        if (!edge) edge = GetComponent<EdgeCollider2D>();
        if (!edge) return;

        // Im Editor NICHT dauernd neuen Seed ziehen, sonst flackert’s bei jedem Slider
        Generate(firstTime: true, forceNewSeed: false);
    }

    // Von außen nutzbar (Button/UI), erzeugt bewusst neues Level
    public void GenerateNewLevel()
    {
        Generate(firstTime: false, forceNewSeed: true);
    }

#if UNITY_EDITOR
#endif

    void Generate(bool firstTime, bool forceNewSeed)
    {
        if (!lr) lr = GetComponent<LineRenderer>();
        if (!lr) return;

        // Seed-Logik
        if (forceNewSeed) seed = Random.Range(1, 999999);
        else if (firstTime && seed == 0) seed = Random.Range(1, 999999);

        noiseOffset = seed * 0.001f;

        lr.positionCount = points;

        float xStart = -width * 0.5f;
        float step = width / (points - 1);

        float padX = landingPad ? landingPad.position.x : float.NaN;
        float padY = landingPad ? landingPad.position.y : 0f;

        // ===== Level Einfluss (level/100) =====
        float lvlFactor = FindFirstObjectByType<GameController>().level / 50f;

        float amp = amplitude * (1f + lvlFactor);
        float mChance = mountainChance * (1f + lvlFactor);
        float mHeight = mountainHeight * (1f + lvlFactor);
        float mWidth = mountainWidth * (1f + lvlFactor);

        // ===== 1) Berge "streuen" in add[] =====
        float[] add = new float[points];

        int half = Mathf.Max(1, Mathf.RoundToInt((mWidth / step) * 0.5f));
        half = Mathf.Clamp(half, 1, points / 2);

        var prevState = Random.state;
        Random.InitState(seed);

        for (int i = 0; i < points; i++)
        {
            if (Random.value > mChance) continue;

            for (int k = -half; k <= half; k++)
            {
                int idx = i + k;
                if (idx < 0 || idx >= points) continue;

                float t = Mathf.InverseLerp(-half, half, k);
                float center01 = 1f - Mathf.Abs(2f * t - 1f);
                float bell = mountainShape.Evaluate(center01);

                add[idx] += mHeight * bell;
            }

            i += half;
        }

        Random.state = prevState;

        // ===== 2) Hauptlinie setzen =====
        for (int i = 0; i < points; i++)
        {
            float x = xStart + step * i;

            float n = Mathf.PerlinNoise(noiseOffset + x * noiseScale, noiseOffset);

            float baseUp = n * amp;
            float mountainUp = n * add[i];
            float y = baseY + baseUp + mountainUp;

            if (landingPad)
            {
                float d = Mathf.Abs(x - padX);
                if (d < padClearRadius)
                {
                    float t = 1f - Mathf.Clamp01(d / padClearRadius);
                    y = Mathf.Lerp(y, padY, t * padFlatStrength);
                }
            }

            lr.SetPosition(i, new Vector3(x, y, 0f));
        }

        UpdateCollider();
    }


    void UpdateCollider()
    {
        if (!edge) return;

        Vector2[] pts = new Vector2[lr.positionCount];
        for (int i = 0; i < pts.Length; i++)
        {
            Vector3 p = lr.GetPosition(i);
            pts[i] = new Vector2(p.x, p.y);
        }

        edge.points = pts;
    }

}
