using System;
using UnityEngine;

[Serializable]
public class RadioMessage
{
    public string id;
    public string sender;
    public string title;
    [TextArea(3, 12)] public string body;
    public StoryDialog.Expression portrait;
    public string signal;
    public Vector3 coordinates;
    public bool read;
    public RadioReply[] replies;
    public int chosenReply = -1;
    public string storyNodeId;
    public int storyOriginalReply = -1;
}

[Serializable]
public class RadioReply
{
    public string text;
    [TextArea(2, 6)] public string reaction;
}
