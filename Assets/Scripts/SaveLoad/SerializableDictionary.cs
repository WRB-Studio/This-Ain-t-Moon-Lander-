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
        keys.Clear();
        values.Clear();
        foreach (var kv in this)
        {
            keys.Add(kv.Key);
            values.Add(kv.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        int c = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < c; i++)
            this[keys[i]] = values[i];
    }
}
