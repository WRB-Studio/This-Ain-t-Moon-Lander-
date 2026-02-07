using UnityEngine;

public class ScoringController : MonoBehaviour
{
    public static ScoringController Instance;

    private LanderController lander => LanderController.Instance;

    [Header("Base")]
    [Tooltip("Basis-Punkte pro erfolgreichem Landing")]
    public int baseLandingScore = 100;

    [Header("Bonuses")]
    [Tooltip("Maximaler Zeitbonus (klein halten)")]
    public int timeBonusMax = 40;
    [Tooltip("Wie viel Zeitbonus pro Sekunde abgezogen wird")]
    public float timeBonusLossPerSecond = 2f;

    [Tooltip("Moon Landing gibt extra Punkte (ohne Speed/Angle/Center)")]
    public int moonLandingBonus = 250;

    [Header("Weights (Pad/Landscape)")]
    public int speedWeight = 150;
    public int angleWeight = 120;
    public int centerWeight = 160;
    public int fuelWeight = 80;

    [Header("Global Multiplier")]
    public float scoreMultiplier = 1f;

    // --- totals ---
    public int CollectedScore { get; private set; }
    public int LastScore { get; private set; }
    public int BestScore { get; private set; }

    // --- UI: breakdown (konkrete Punkte, die addiert werden) ---
    public int LastSpeedScore { get; private set; }
    public int LastAngleScore { get; private set; }
    public int LastCenterScore { get; private set; }
    public int LastFuelScore { get; private set; }
    public int LastTimeScore { get; private set; }
    public int LastMoonScore { get; private set; } // 0 oder moonLandingBonus

    // --- UI: optionale Infos ---
    public int LastCenterPct { get; private set; } // für "perfect landing" check / debug
    public float LastTimeSec { get; private set; }
    public bool LastWasMoon { get; private set; }

    private float runStartTime;


    private void Awake() => Instance = this;

    public void Init()
    {
        BestScore = SaveLoadManager.Instance.Data.BestScore;
        CollectedScore = SaveLoadManager.Instance.Data.CollectedScore;
    }

    /// <summary>Call this whenever a new round/run starts (e.g. after countdown).</summary>
    public void BeginRun() => runStartTime = Time.time;

    public void CalculateScore(Collision2D col)
    {
        float impactSpeed = col.relativeVelocity.magnitude;
        float impactAngle = Mathf.Abs(Mathf.DeltaAngle(0f, lander.GetComponent<Rigidbody2D>().rotation));

        float timeSec = Mathf.Max(0f, Time.time - runStartTime);
        float fuelPct = lander.fuelMax <= 0f ? 0f : Mathf.Clamp01(lander.currentFuel / lander.fuelMax);

        bool landedMoon = LanderController.Instance.landerState == LanderController.eLanderState.LandedMoon;

        // Center / Speed / Angle only for Pad/Landscape landings
        float centerAcc = 0f;
        LastCenterPct = 0;
        if (!landedMoon)
        {
            centerAcc = CalcCenterAccuracy(col.collider);
            LastCenterPct = Mathf.RoundToInt(centerAcc * 100f);

            if (LastCenterPct >= 90)
                AudioManager.Instance.PlaySound(AudioManager.Instance.sfxPerfectLanding);
        }

        LastScore = CalculateScore(impactSpeed, impactAngle, centerAcc, fuelPct, timeSec, landedMoon);

        AddToCollectedScore(LastScore);
        SaveBestScoreIfNeeded();
    }

    int CalculateScore(float impactSpeed, float impactAngle, float centerAcc01, float fuelPct01, float timeSec, bool landedMoon)
    {
        int ApplyBoost(int v) => Mathf.RoundToInt(v * scoreMultiplier);

        // reset breakdown
        LastSpeedScore = 0;
        LastAngleScore = 0;
        LastCenterScore = 0;
        LastFuelScore = 0;
        LastTimeScore = 0;
        LastMoonScore = 0;

        // TIME (raw -> boosted)
        int timeRaw = Mathf.RoundToInt(
            Mathf.Clamp(timeBonusMax - (timeSec * timeBonusLossPerSecond), 0f, timeBonusMax)
        );
        LastTimeScore = ApplyBoost(timeRaw);

        // FUEL (raw -> boosted)
        int fuelRaw = Mathf.RoundToInt(Mathf.Clamp01(fuelPct01) * fuelWeight);
        LastFuelScore = ApplyBoost(fuelRaw);

        // BASE (raw -> boosted)
        int baseRaw = baseLandingScore;
        int baseScore = ApplyBoost(baseRaw);

        int score = baseScore + LastTimeScore + LastFuelScore;

        // SPEED -> IMMER (auch Moon)
        {
            float safeSpeed = Mathf.Max(0.0001f, lander.safeSpeed);
            float speedQ = Mathf.Clamp01((safeSpeed - impactSpeed) / safeSpeed);
            int speedRaw = Mathf.RoundToInt(speedQ * speedWeight);
            LastSpeedScore = ApplyBoost(speedRaw);
            score += LastSpeedScore;
        }

        if (!landedMoon)
        {
            // ANGLE + CENTER nur bei Landscape/Pad
            float safeAngle = Mathf.Max(0.0001f, lander.safeAngleDeg);

            float angleQ = Mathf.Clamp01((safeAngle - impactAngle) / safeAngle);
            float centerQ = Mathf.Clamp01(centerAcc01);

            int angleRaw = Mathf.RoundToInt(angleQ * angleWeight);
            int centerRaw = Mathf.RoundToInt(centerQ * centerWeight);

            LastAngleScore = ApplyBoost(angleRaw);
            LastCenterScore = ApplyBoost(centerRaw);

            score += LastAngleScore;
            score += LastCenterScore;
        }
        else
        {
            // MOON bonus zusätzlich
            LastMoonScore = ApplyBoost(moonLandingBonus);
            score += LastMoonScore;

            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxPerfectLanding);
        }

        return score;
    }


    void AddToCollectedScore(int amount)
    {
        if (amount <= 0) return;

        CollectedScore += amount;
        SaveLoadManager.Instance.Data.CollectedScore = CollectedScore;
        SaveLoadManager.Instance.Save();
    }

    float CalcCenterAccuracy(Collider2D padCol)
    {
        float padCenterX = padCol.bounds.center.x;
        float padHalfWidth = padCol.bounds.extents.x;

        float shipX = lander.transform.position.x;
        float dx = Mathf.Abs(shipX - padCenterX);

        return 1f - Mathf.Clamp01(dx / padHalfWidth);
    }

    void SaveBestScoreIfNeeded()
    {
        if (LastScore <= BestScore) return;

        BestScore = LastScore;
        SaveLoadManager.Instance.Data.BestScore = BestScore;
        SaveLoadManager.Instance.Save();
    }
}
