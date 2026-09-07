using UnityEngine;

public class ScoringController : MonoBehaviour
{
    public static ScoringController Instance;

    [Header("Base")]
    [Tooltip("Basis-Punkte pro erfolgreichem Landing")]
    public int baseLandingScore = 100;

    [Header("Bonuses")]
    public int timeBonusMax = 40;
    public float timeBonusLossPerSecond = 2f;
    public int moonLandingBonus = 250;

    [Header("Weights (Pad/Landscape)")]
    public int speedWeight = 150;
    public int angleWeight = 120;
    public int centerWeight = 160;
    public int fuelWeight = 80;

    [Header("Global Multiplier")]
    public float scoreMultiplier = 1f;

    public int CollectedScore { get; private set; }
    public int LastScore { get; private set; }
    public int BestScore { get; private set; }

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
    LanderController lander => LanderController.Active;

    void Awake() => Instance = this;

    public void Init()
    {
        SaveGame data = SaveLoadManager.Instance.Data;
        BestScore = data.BestScore;
        CollectedScore = data.CollectedScore;
    }

    public void BeginRun() => runStartTime = Time.time;

    public void CalculateScore(Collision2D collision)
    {
        if (!lander) return;

        bool landedMoon = lander.landerState == LanderController.eLanderState.LandedMoon;
        float impactSpeed = collision.relativeVelocity.magnitude;
        float impactAngle = landedMoon ? GetGravityRelativeAngle() : Mathf.Abs(Mathf.DeltaAngle(0f, lander.rb.rotation));
        float timeSec = Mathf.Max(0f, Time.time - runStartTime);
        float centerAccuracy = landedMoon ? 0f : CalcCenterAccuracy(collision.collider);

        LastTimeSec = timeSec;
        LastWasMoon = landedMoon;
        LastCenterPct = landedMoon ? 0 : Mathf.RoundToInt(centerAccuracy * 100f);

        if (!landedMoon && LastCenterPct >= 90)
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxPerfectLanding);

        LastScore = CalculateScore(
            impactSpeed,
            impactAngle,
            centerAccuracy,
            lander.Fuel01,
            timeSec,
            landedMoon);

        CommitScore(LastScore);
    }

    int CalculateScore(float impactSpeed, float impactAngle, float centerAcc01, float fuelPct01, float timeSec, bool landedMoon)
    {
        ResetBreakdown();

        LastTimeScore = Boost(Mathf.RoundToInt(
            Mathf.Clamp(timeBonusMax - timeSec * timeBonusLossPerSecond, 0f, timeBonusMax)));

        LastFuelScore = Boost(Mathf.RoundToInt(Mathf.Clamp01(fuelPct01) * fuelWeight));

        float safeSpeed = Mathf.Max(0.0001f, lander.safeSpeed);
        float speedQuality = Mathf.Clamp01((safeSpeed - impactSpeed) / safeSpeed);
        LastSpeedScore = Boost(Mathf.RoundToInt(speedQuality * speedWeight));

        int score = Boost(baseLandingScore) + LastTimeScore + LastFuelScore + LastSpeedScore;

        if (landedMoon)
        {
            LastMoonScore = Boost(moonLandingBonus);
            score += LastMoonScore;
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxPerfectLanding);
            return score;
        }

        float safeAngle = Mathf.Max(0.0001f, lander.safeAngleDeg);
        float angleQuality = Mathf.Clamp01((safeAngle - impactAngle) / safeAngle);
        float centerQuality = Mathf.Clamp01(centerAcc01);

        LastAngleScore = Boost(Mathf.RoundToInt(angleQuality * angleWeight));
        LastCenterScore = Boost(Mathf.RoundToInt(centerQuality * centerWeight));

        return score + LastAngleScore + LastCenterScore;
    }

    int Boost(int value) => Mathf.RoundToInt(value * scoreMultiplier);

    void ResetBreakdown()
    {
        LastSpeedScore = 0;
        LastAngleScore = 0;
        LastCenterScore = 0;
        LastFuelScore = 0;
        LastTimeScore = 0;
        LastMoonScore = 0;
    }

    void CommitScore(int amount)
    {
        if (amount > 0)
        {
            CollectedScore += amount;
            SaveLoadManager.Instance.Data.CollectedScore = CollectedScore;
        }

        if (LastScore > BestScore)
        {
            BestScore = LastScore;
            SaveLoadManager.Instance.Data.BestScore = BestScore;
        }

        SaveLoadManager.Instance.Save();
    }

    float GetGravityRelativeAngle()
    {
        Vector2 gravity = lander.CurrentGravity;
        if (gravity.sqrMagnitude < 0.0001f) return 0f;

        Vector2 up = -gravity.normalized;
        float desired = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
        return Mathf.Abs(Mathf.DeltaAngle(desired, lander.rb.rotation));
    }

    float CalcCenterAccuracy(Collider2D padCollider)
    {
        if (!padCollider) return 0f;

        float halfWidth = Mathf.Max(0.0001f, padCollider.bounds.extents.x);
        float distance = Mathf.Abs(lander.transform.position.x - padCollider.bounds.center.x);
        return 1f - Mathf.Clamp01(distance / halfWidth);
    }
}
