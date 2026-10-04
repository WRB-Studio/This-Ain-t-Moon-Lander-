using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new();
    [SerializeField] private List<TValue> values = new();

    public void OnBeforeSerialize()
    {
        keys ??= new();
        values ??= new();
        keys.Clear();
        values.Clear();
        foreach (var entry in this)
        {
            keys.Add(entry.Key);
            values.Add(entry.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        if (keys == null || values == null) return;
        for (int i = 0; i < Math.Min(keys.Count, values.Count); i++)
            if (keys[i] != null) this[keys[i]] = values[i];
    }
}
