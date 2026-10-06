using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public static class StoryChecks
{
    static int passed;
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    static IEnumerator Until(Func<bool> condition)
    {
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 12;
        while (!condition())
        {
            if (UnityEditor.EditorApplication.timeSinceStartup > deadline) throw new Exception("Story transition timed out.");
            yield return null;
        }
    }

    public static IEnumerator Run()
    {
        passed = 0;
        var story = StoryTextController.Instance;
        var manager = SaveLoadManager.Instance;
        var savedFlags = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).flags;
        int originalAsideLevel = manager.Data.earthAsideLevel;
        string originalAside = manager.Data.lastEarthAside;
        var delay = typeof(StoryTextController).GetField("triggerDelayFromRunStart", BindingFlags.Instance | BindingFlags.NonPublic);
        float previousDelay = (float)delay.GetValue(story);
        delay.SetValue(story, 0f);
        story.Restart();
        var serial = new UnityEditor.SerializedObject(story);
        var panel = ((StoryDialog)serial.FindProperty("dialog").objectReferenceValue).gameObject;
        var panelVisibility = (CanvasGroup)serial.FindProperty("gameplayPanelVisibility").objectReferenceValue;
        var info = (TMPro.TMP_Text)serial.FindProperty("txtInfo").objectReferenceValue;
        float originalFixedDeltaTime = Time.fixedDeltaTime;
        Require(story.GetEarthMessages(1)[0] != story.GetEarthMessages(4)[0]
            && story.GetEarthMessages(4)[0] != story.GetEarthMessages(7)[0], "Earth briefings must progress through distinct level ranges.");
        Require(story.GetEarthMessages(3).Length == 0 && story.GetEarthMessages(6).Length == 0
            && story.GetEarthMessages(9).Length == 0 && story.GetEarthMessages(10).Length == 0,
            "Transmission levels and later levels must not use ordinary launch messages.");
        int originalLevel = GameController.Instance.level;
        GameController.Instance.level = 3;
        story.ShowEarthBriefing();
        var wait = Until(() => panel.activeInHierarchy && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(story.IsShowingTransmission && Time.timeScale == 0f && story.BlocksGameplayInput,
            "Level three must show one operator transmission with paused gameplay.");
        Require(panelVisibility.alpha == 0f, "A dialogue must hide the countdown and result panel behind it.");
        story.enabled = false; story.enabled = true;
        wait = Until(() => panel.activeInHierarchy && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(story.IsShowingTransmission, "An interrupted transmission must return until acknowledged.");
        story.ContinueDiscovery();
        Require(manager.Data.GetFlag("story.earth.3") && !panel.activeInHierarchy,
            "Continue must acknowledge the transmission and hide its text immediately.");
        wait = Until(() => !story.IsShowingDialogue && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(Time.timeScale == 1f && Mathf.Abs(Time.fixedDeltaTime - originalFixedDeltaTime) < 0.000001f && panelVisibility.alpha == 1f,
            "Continue must smoothly restore game speed, physics timing and the HUD. scale=" + Time.timeScale
            + ", fixed=" + Time.fixedDeltaTime + ", expected=" + originalFixedDeltaTime + ", alpha=" + panelVisibility.alpha);
        story.Restart(); story.ShowEarthBriefing();
        for (int i = 0; i < 4; i++) yield return null;
        Require(!story.IsShowingDialogue, "An acknowledged level transmission must not repeat on retry.");
        GameController.Instance.level = originalLevel;
        story.Restart();
        string previousAside = manager.Data.lastEarthAside;
        for (int i = 0; i < 40; i++)
        {
            string aside = story.PickEarthAside();
            Require(!string.IsNullOrEmpty(aside) && aside != previousAside, "Late asides must avoid immediate repeats.");
            previousAside = aside;
        }
        manager.Save(); manager.Load();
        Require(manager.Data.lastEarthAside == previousAside, "The last aside must survive save/load to prevent repeats.");
        var chance = typeof(StoryTextController).GetField("earthAsideChance", BindingFlags.Instance | BindingFlags.NonPublic);
        float originalChance = (float)chance.GetValue(story);
        chance.SetValue(story, 1f); GameController.Instance.level = 10;
        story.ShowEarthBriefing();
        Require(manager.Data.earthAsideLevel == 10 && manager.Data.lastEarthAside != previousAside,
            "A late level must roll one aside using its configured probability.");
        previousAside = manager.Data.lastEarthAside;
        story.Restart(); story.ShowEarthBriefing();
        Require(manager.Data.lastEarthAside == previousAside, "Retrying a late level must not reroll its aside.");
        chance.SetValue(story, 0f); GameController.Instance.level = 11;
        story.ShowEarthBriefing();
        Require(manager.Data.earthAsideLevel == 11 && manager.Data.lastEarthAside == previousAside,
            "A zero probability must keep the next level silent.");
        chance.SetValue(story, originalChance); GameController.Instance.level = originalLevel;
        story.Restart();
        story.Discover(StoryTextController.Discovery.EVA);
        story.Discover(StoryTextController.Discovery.EVA);
        Require(story.HasDiscovered(StoryTextController.Discovery.EVA), "Discoveries must be recorded idempotently.");
        for (int i = 0; i < 4; i++) yield return null;
        Require(!story.IsShowingDialogue && Time.timeScale == 1f && info.gameObject.activeInHierarchy
            && info.text == story.GetDiscoveryText(StoryTextController.Discovery.EVA),
            "EVA must show an ordinary flight overlay without changing game speed.");
        manager.Load();
        Require(manager.Data.GetFlag("story.ack.EVA"), "The EVA overlay must be acknowledged when displayed.");
        story.Restart();
        story.Discover(StoryTextController.Discovery.AbandonedShip);
        yield return null;
        Require(!panel.activeInHierarchy && Time.timeScale == 1f, "A new ship notice must wait before changing game speed.");
        wait = Until(() => story.IsTransitioning && Time.timeScale < 1f);
        while (wait.MoveNext()) yield return wait.Current;
        Require(!panel.activeInHierarchy && Time.timeScale > 0.05f,
            "Slow motion must ease in before the dialogue is displayed.");
        wait = Until(() => panel.activeInHierarchy && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(story.IsShowingDiscovery && Time.timeScale > 0f && Time.timeScale < 0.1f,
            "The delayed ship discovery must enter strong slow motion and then show its text.");
        Require(!LanderUI.Instance.TryGetGameplayPointer(out _), "Discovery dialogue must block gameplay pointer input.");
        manager.Load();
        Require(story.HasDiscovered(StoryTextController.Discovery.AbandonedShip)
            && !manager.Data.GetFlag("story.ack.AbandonedShip"), "Save/load must preserve discovery independently of its acknowledgement.");
        story.enabled = false;
        Require(Time.timeScale == 1f && !story.IsShowingDiscovery, "Disabling the story controller must restore the original time scale.");
        story.enabled = true;
        wait = Until(() => panel.activeInHierarchy && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(story.IsShowingDiscovery, "Interrupted discovery notices must be offered again.");
        story.ContinueDiscovery();
        Require(Time.timeScale < 1f && story.BlocksGameplayInput && !panel.activeInHierarchy,
            "Continue must guard the closing click while time eases back to normal.");
        wait = Until(() => !story.BlocksGameplayInput);
        while (wait.MoveNext()) yield return wait.Current;
        Require(!story.BlocksGameplayInput && !story.IsShowingDiscovery, "Input must resume after the pointer has been released.");
        manager.Load();
        Require(manager.Data.GetFlag("story.ack.AbandonedShip"), "Acknowledgements must survive a disk save and load.");
        story.Restart();
        for (int i = 0; i < 4; i++) yield return null;
        Require(!story.IsShowingDiscovery, "Acknowledged discoveries must not repeat on another run.");
        story.Show("Ordinary flight comment.");
        yield return null; yield return null;
        Require(Time.timeScale == 1f && !story.IsShowingDiscovery, "Ordinary comments must leave simulation time unchanged.");
        story.Restart();
        story.Discover(StoryTextController.Discovery.MoonLanding);
        for (int i = 0; i < 4; i++) yield return null;
        Require(!story.IsShowingDialogue && Time.timeScale == 1f,
            "Moon landing must not open a special discovery dialogue.");
        Require(story.TakeMoonLandingMessage() == story.GetDiscoveryText(StoryTextController.Discovery.MoonLanding)
            && story.TakeMoonLandingMessage() == null, "The first moon landing text must be supplied to the score panel once.");
        var ship = LanderController.Instance;
        var originalPosition = ship.rb.position;
        var gravity = GravityManager2D.Instance;
        var update = typeof(StoryTextController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        var canShow = typeof(StoryTextController).GetMethod("CanShowDiscovery", BindingFlags.Instance | BindingFlags.NonPublic);
        var originalVelocity = ship.rb.linearVelocity;
        ship.rb.position = gravity.transform.position + Vector3.right * (gravity.moonEnterRadius - 5f);
        ship.transform.position = ship.rb.position; Physics2D.SyncTransforms();
        ship.rb.linearVelocity = Vector2.left * 100f;
        Require(!(bool)canShow.Invoke(story, new object[] { StoryTextController.Discovery.Moon }),
            "A moon notice must not take control away during an imminent impact.");
        ship.rb.linearVelocity = Vector2.zero;
        Require((bool)canShow.Invoke(story, new object[] { StoryTextController.Discovery.Moon }),
            "A safe outer moon approach must permit its discovery notice.");
        ship.rb.linearVelocity = originalVelocity;
        ship.rb.position = gravity.transform.position + Vector3.up * (gravity.moonFullRadius * 0.5f);
        ship.transform.position = ship.rb.position;
        Physics2D.SyncTransforms(); update.Invoke(story, null);
        Require(story.HasDiscovered(StoryTextController.Discovery.Moon)
            && !story.HasDiscovered(StoryTextController.Discovery.ZeroG),
            "Moon gravity must discover the Moon without falsely claiming zero gravity. Moon="
            + story.HasDiscovered(StoryTextController.Discovery.Moon) + ", ZeroG="
            + story.HasDiscovered(StoryTextController.Discovery.ZeroG));
        ship.rb.position = new Vector2(gravity.transform.position.x + gravity.moonEnterRadius + 100,
            Mathf.Max(gravity.zeroGFullY + 10, gravity.transform.position.y + gravity.moonEnterRadius + 100));
        ship.transform.position = ship.rb.position;
        Physics2D.SyncTransforms(); update.Invoke(story, null);
        Require(story.HasDiscovered(StoryTextController.Discovery.ZeroG), "Entering actual zero gravity must record its discovery.");
        yield return null;
        Require(!panel.activeInHierarchy && Time.timeScale == 1f, "Zero gravity must not show or slow the game immediately.");
        wait = Until(() => panel.activeInHierarchy && !story.IsTransitioning);
        while (wait.MoveNext()) yield return wait.Current;
        Require(story.IsShowingDiscovery && Time.timeScale < 0.1f,
            "The first zero gravity notice must show after its delay and deceleration.");
        var restored = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data));
        Require(restored.GetFlag("discovery.Moon") && restored.GetFlag("discovery.ZeroG"),
            "Multiple discoveries must survive save serialization together.");
        ship.rb.position = originalPosition; ship.transform.position = originalPosition; Physics2D.SyncTransforms();
        story.Restart();
        manager.Data.flags = savedFlags;
        manager.Data.earthAsideLevel = originalAsideLevel;
        manager.Data.lastEarthAside = originalAside;
        delay.SetValue(story, previousDelay);
        manager.Save();
        Debug.Log("Story checks passed: " + passed);
    }
}
