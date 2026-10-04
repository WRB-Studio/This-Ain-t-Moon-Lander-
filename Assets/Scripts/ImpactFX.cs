using System.Collections;
using UnityEngine;

public class ImpactFX : MonoBehaviour
{
    public static ImpactFX Instance;
    Coroutine shake;

    void Awake() => Instance = this;
    public void Init() => ResetEffect();

    public void PlayImpactEffect(LanderController.eLanderState state)
    {
        if (state == LanderController.eLanderState.LandedPad
            || state == LanderController.eLanderState.LandedMoon) return;
        ResetEffect();
        shake = StartCoroutine(Shake());
    }

    public void ResetEffect()
    {
        if (shake != null) StopCoroutine(shake);
        shake = null;
        if (CameraController.Instance) CameraController.Instance.shakeOffset = Vector3.zero;
    }

    IEnumerator Shake()
    {
        var camera = CameraController.Instance;
        if (!camera) yield break;
        const float duration = 0.4f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float strength = 0.5f * (1f - elapsed / duration);
            float x = (Mathf.PerlinNoise(Time.unscaledTime * 35f, 0f) - 0.5f) * 2f * strength;
            float y = (Mathf.PerlinNoise(0f, Time.unscaledTime * 35f) - 0.5f) * 2f * strength;
            camera.shakeOffset = new Vector3(x, y);
            yield return null;
        }
        camera.shakeOffset = Vector3.zero;
        shake = null;
    }

    void OnDisable() => ResetEffect();
    void OnDestroy() { if (Instance == this) Instance = null; }
}
