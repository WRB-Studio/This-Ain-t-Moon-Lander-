using System;
using UnityEngine;

public enum StoryDecorationKind { Label, Line }
[Serializable] public class StoryBoardDecoration
{
    public string id, chapter;
    public bool storyView;
    public StoryDecorationKind kind;
    public string text = "Titel";
    public Vector2 position, end, size = new(300, 70);
    public int fontSize = 24;
    public Color color = Color.white;
    public float lineWidth = 2;
    public bool dashed;
}
