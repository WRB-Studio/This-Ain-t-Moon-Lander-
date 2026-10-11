using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ZeroGAnimationAuthoring
{
    public static void Build()
    {
        AddToController(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Characters/Biped.controller"));
        AssetDatabase.SaveAssets();
        Debug.Log("Slow four-second zero-G struggle loop authored on the shared biped rig.");
    }
    public static void AddToController(AnimatorController controller)
    {
        const string path = "Assets/Animations/Characters/BipedFloat.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (!clip) { clip = new AnimationClip { name = "BipedFloat", frameRate = 30 }; AssetDatabase.CreateAsset(clip, path); }
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Characters/BipedIdle.anim");
        foreach (var binding in AnimationUtility.GetCurveBindings(idle))
        {
            var curve = AnimationUtility.GetEditorCurve(idle, binding);
            float factor = 4f / idle.length;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++) { keys[i].time *= factor; keys[i].inTangent /= factor; keys[i].outTangent /= factor; }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }
        Rotate("Hips", t => 3 * Mathf.Sin(t * Mathf.PI * 2));
        Rotate("Hips/Spine", t => -2 * Mathf.Sin(t * Mathf.PI * 2 + .4f));
        foreach (bool near in new[] { true, false })
        {
            float offset = near ? 0 : .5f;
            string arm = "Hips/Spine/" + (near ? "ArmNear" : "ArmFar");
            string leg = "Hips/" + (near ? "LegNear" : "LegFar");
            Rotate(arm, t => -15 + 16 * Mathf.Sin((t + offset) * Mathf.PI * 2));
            Rotate(arm + "/Forearm", t => 30 + 12 * Mathf.Sin((t + offset + .15f) * Mathf.PI * 2));
            Rotate(arm + "/Forearm/Hand", t => 5 * Mathf.Sin((t + offset) * Mathf.PI * 2));
            Rotate(leg, t => 7 + 13 * Mathf.Sin((t + offset + .2f) * Mathf.PI * 2));
            Rotate(leg + "/Shin", t => -22 + 10 * Mathf.Sin((t + offset + .35f) * Mathf.PI * 2));
            Rotate(leg + "/Shin/Foot", t => 8 + 5 * Mathf.Sin((t + offset + .4f) * Mathf.PI * 2));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; settings.startTime = 0; settings.stopTime = 4;
        AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        if (!controller.parameters.Any(p => p.name == "IsFloating")) controller.AddParameter("IsFloating", AnimatorControllerParameterType.Bool);
        var machine = controller.layers[0].stateMachine;
        var floating = machine.states.FirstOrDefault(s => s.state.name == "Floating").state ?? machine.AddState("Floating");
        floating.motion = clip;
        foreach (var child in machine.states)
        {
            var state = child.state; if (state == floating) continue;
            foreach (var transition in state.transitions)
                if (transition.destinationState != floating && !transition.conditions.Any(c => c.parameter == "IsFloating"))
                    transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFloating");
            if (!state.transitions.Any(t => t.destinationState == floating))
            {
                var enter = state.AddTransition(floating); enter.hasExitTime = false; enter.duration = .3f;
                enter.AddCondition(AnimatorConditionMode.If, 0, "IsFloating");
            }
        }
        if (floating.transitions.Length == 0)
        {
            var exit = floating.AddTransition(machine.defaultState); exit.hasExitTime = false; exit.duration = .3f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "IsFloating");
        }
        EditorUtility.SetDirty(controller);
        void Rotate(string bone, Func<float, float> value)
        {
            const int samples = 48; var keys = new Keyframe[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float slope = (value(t + .001f) - value(t - .001f)) / .008f;
                keys[i] = new Keyframe(t * 4, value(t), slope, slope);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(bone, typeof(Transform), "localEulerAnglesRaw.z"), new AnimationCurve(keys));
        }
    }
}
