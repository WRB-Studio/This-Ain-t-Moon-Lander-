using System.Collections;
using UnityEngine;

public class ImpactFX : MonoBehaviour
{
    public static ImpactFX Instance;

    Coroutine impactRoutine;

    void Awake() => Instance = this;

    public void Init()
    {
        if (CameraController.Instance)
            CameraController.Instance.shakeOffset = Vector3.zero;
    }

    public void PlayImpactEffect(LanderController.eLanderState state)
    {
        if (state == LanderController.eLanderState.LandedPad ||
            state == LanderController.eLanderState.LandedMoon)
            return;

        if (impactRoutine != null)
            StopCoroutine(impactRoutine);

        impactRoutine = StartCoroutine(ImpactRoutine(0f, 0.4f, 0.5f));
    }

    IEnumerator ImpactRoutine(float freezeTime, float shakeTime, float shakeStrength, float shakeHz = 35f)
    {
        float previousTimeScale = Time.timeScale;

        if (freezeTime > 0f)
        {
            Time.timeScale = 0f;
            for (float time = 0f; time < freezeTime; time += Time.unscaledDeltaTime)
                yield return null;
            Time.timeScale = previousTimeScale;
        }

        CameraController cameraController = CameraController.Instance;
        if (!cameraController)
        {
            impactRoutine = null;
            yield break;
        }

        for (float time = 0f; time < shakeTime; time += Time.unscaledDeltaTime)
        {
            float fade = 1f - Mathf.Clamp01(time / Mathf.Max(0.0001f, shakeTime));
            float strength = shakeStrength * fade;

            float x = (Mathf.PerlinNoise(Time.unscaledTime * shakeHz, 0f) - 0.5f) * 2f * strength;
            float y = (Mathf.PerlinNoise(0f, Time.unscaledTime * shakeHz) - 0.5f) * 2f * strength;

            cameraController.shakeOffset = new Vector3(x, y, 0f);
            yield return null;
        }

        cameraController.shakeOffset = Vector3.zero;
        impactRoutine = null;
    }
}
