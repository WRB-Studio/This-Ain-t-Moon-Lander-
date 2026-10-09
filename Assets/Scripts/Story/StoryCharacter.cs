using UnityEngine;

[CreateAssetMenu(menuName = "Story/Charakter")]
public class StoryCharacter : ScriptableObject
{
    public string id;
    public string nameKey;
    public GameObject prefab;
    public Sprite portrait;
}
