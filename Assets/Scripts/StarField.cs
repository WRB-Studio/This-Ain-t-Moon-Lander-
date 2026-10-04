using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class StarField : MonoBehaviour
{
    public static StarField Instance;

    public int starCount = 200;
    public float radius = 20f;
    public float parallax = 0.05f;

    Transform target;
    ParticleSystem ps;
    ParticleSystem.Particle[] stars;

    void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        starCount = Mathf.Max(1, starCount);
        ps = GetComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.startSpeed = 0;
        main.startLifetime = Mathf.Infinity;
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Emit(starCount);
        stars = new ParticleSystem.Particle[starCount];
        ps.GetParticles(stars);

        for (int i = 0; i < stars.Length; i++)
            RespawnStar(i);

        ps.SetParticles(stars, stars.Length);
    }

    void LateUpdate()
    {
        if (!target) return;

        // Starfeld folgt dem Lander (parallax)
        transform.position = Vector3.Lerp(
            transform.position,
            target.position,
            1f - Mathf.Pow(1f - Mathf.Clamp01(parallax), Time.deltaTime * 60f));

        RecycleStars();
    }

    public void SetTarget(Transform newTarget, bool instant = false)
    {
        target = newTarget;
        if (!instant || !target || stars == null) return;
        transform.position = target.position;
        for (int i = 0; i < stars.Length; i++) RespawnStar(i);
        ps.SetParticles(stars, stars.Length);
    }

    void RecycleStars()
    {
        Vector3 center = transform.position;

        for (int i = 0; i < stars.Length; i++)
        {
            if (((Vector2)(stars[i].position - center)).sqrMagnitude > radius * radius)
                RespawnStar(i);
        }

        ps.SetParticles(stars, stars.Length);
    }

    void RespawnStar(int i)
    {
        Vector2 p = Random.insideUnitCircle * radius;
        stars[i].position = (Vector3)p + transform.position;
        stars[i].startSize = Random.Range(0.02f, 0.06f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
