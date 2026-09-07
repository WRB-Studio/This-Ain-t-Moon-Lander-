using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class StarField : MonoBehaviour
{
    public static StarField Instance;

    public int starCount = 200;
    public float radius = 20f;
    public float parallax = 0.05f;

    Transform target;
    ParticleSystem particleSystemRef;
    ParticleSystem.Particle[] stars;

    void Awake() => Instance = this;

    public void Init()
    {
        particleSystemRef = GetComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particleSystemRef.main;
        main.loop = false;
        main.startSpeed = 0f;
        main.startLifetime = Mathf.Infinity;
        main.maxParticles = starCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        particleSystemRef.Clear();
        particleSystemRef.Emit(starCount);

        stars = new ParticleSystem.Particle[starCount];
        particleSystemRef.GetParticles(stars);

        for (int i = 0; i < stars.Length; i++)
            RespawnStar(i);

        particleSystemRef.SetParticles(stars, stars.Length);
    }

    void LateUpdate()
    {
        if (!target || stars == null) return;

        transform.position = Vector3.Lerp(transform.position, target.position, parallax);
        RecycleStars();
    }

    public void SetTarget(Transform newTarget, bool instant = false)
    {
        target = newTarget;
        if (instant && target)
            transform.position = target.position;
    }

    void RecycleStars()
    {
        Vector2 center = transform.position;
        float maxDistanceSqr = radius * radius;
        bool changed = false;

        for (int i = 0; i < stars.Length; i++)
        {
            Vector2 delta = (Vector2)stars[i].position - center;
            if (delta.sqrMagnitude <= maxDistanceSqr) continue;

            RespawnStar(i);
            changed = true;
        }

        if (changed)
            particleSystemRef.SetParticles(stars, stars.Length);
    }

    void RespawnStar(int index)
    {
        Vector2 position = Random.insideUnitCircle * radius;
        stars[index].position = (Vector3)position + transform.position;
        stars[index].startSize = Random.Range(0.02f, 0.06f);
    }
}
