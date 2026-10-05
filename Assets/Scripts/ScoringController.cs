using UnityEngine;

public class ScoringController : MonoBehaviour
{
    public static ScoringController Instance;
    [Header("Base")]
    public int baseLandingScore = 100;
    [Header("Bonuses")]
    public int timeBonusMax = 40;
    public float timeBonusLossPerSecond = 2f;
    public int moonLandingBonus = 250;
    [Header("Weights")]
    public int speedWeight = 150;
    public int angleWeight = 120;
    public int centerWeight = 160;
    public int fuelWeight = 80;
    [Header("Global Multiplier")]
    public float scoreMultiplier = 1f;

    public int CollectedScore { get; private set; }
    public int LastScore { get; private set; }
    public int BestScore { get; private set; }
    public int LastBaseScore { get; private set; }
    public int LastSpeedScore { get; private set; }
    public int LastAngleScore { get; private set; }
    public int LastCenterScore { get; private set; }
    public int LastFuelScore { get; private set; }
    public int LastTimeScore { get; private set; }
    public int LastMoonScore { get; private set; }
    public int LastCenterPct { get; private set; }
    public float LastTimeSec { get; private set; }
    public bool LastWasMoon { get; private set; }

    float runStartTime;
    bool moonAwarded;
    bool padAwarded;

    void Awake() => Instance = this;

    public void Init()
    {
        var data = SaveLoadManager.Instance.Data;
        BestScore = data.BestScore;
        CollectedScore = data.CollectedScore;
    }

    public void BeginRun()
    {
        runStartTime = Time.time;
        moonAwarded = padAwarded = false;
    }

    int Boost(int points) => Mathf.Max(0, Mathf.RoundToInt(points * scoreMultiplier));

    public bool CalculateScore(Collision2D collision)
    {
        var lander = LanderController.Instance;
        bool moon = lander.landerState == LanderController.eLanderState.LandedMoon;
        if (moon ? moonAwarded : padAwarded) return false;
        if (moon) moonAwarded = true;
        else padAwarded = true;

        LastWasMoon = moon;
        LastTimeSec = Mathf.Max(0f, Time.time - runStartTime);
        float halfWidth = Mathf.Max(0.0001f, collision.collider.bounds.extents.x);
        float distance = Mathf.Abs(lander.transform.position.x - collision.collider.bounds.center.x);
        float center = moon ? 0f : 1f - Mathf.Clamp01(distance / halfWidth);
        LastCenterPct = Mathf.RoundToInt(center * 100f);
        float speedQuality = Mathf.Clamp01(1f - collision.relativeVelocity.magnitude / Mathf.Max(0.0001f, lander.safeSpeed));
        float angle = Mathf.Abs(Mathf.DeltaAngle(0f, lander.rb.rotation));
        float angleQuality = Mathf.Clamp01(1f - angle / Mathf.Max(0.0001f, lander.safeAngleDeg));

        LastBaseScore = Boost(baseLandingScore);
        LastSpeedScore = Boost(Mathf.RoundToInt(speedQuality * speedWeight));
        LastAngleScore = moon ? 0 : Boost(Mathf.RoundToInt(angleQuality * angleWeight));
        LastCenterScore = moon ? 0 : Boost(Mathf.RoundToInt(center * centerWeight));
        LastFuelScore = Boost(Mathf.RoundToInt(lander.FuelFraction * fuelWeight));
        LastTimeScore = Boost(Mathf.RoundToInt(Mathf.Clamp(timeBonusMax - LastTimeSec * timeBonusLossPerSecond, 0f, timeBonusMax)));
        LastMoonScore = moon ? Boost(moonLandingBonus) : 0;
        LastScore = LastBaseScore + LastSpeedScore + LastAngleScore + LastCenterScore
            + LastFuelScore + LastTimeScore + LastMoonScore;
        if (moon || LastCenterPct >= 90)
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxPerfectLanding);

        CollectedScore = (int)System.Math.Min(int.MaxValue, (long)CollectedScore + LastScore);
        BestScore = Mathf.Max(BestScore, LastScore);
        var data = SaveLoadManager.Instance.Data;
        data.CollectedScore = CollectedScore;
        data.BestScore = BestScore;
        SaveLoadManager.Instance.Save();
        return true;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
