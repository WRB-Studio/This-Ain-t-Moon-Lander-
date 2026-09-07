using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
{
    [SerializeField] List<TKey> keys = new();
    [SerializeField] List<TValue> values = new();

    public void OnBeforeSerialize()
    {
        keys ??= new List<TKey>();
        values ??= new List<TValue>();

        keys.Clear();
        values.Clear();

        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            keys.Add(pair.Key);
            values.Add(pair.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        Clear();
        if (keys == null || values == null) return;

        int count = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
            this[keys[i]] = values[i];
    }
}
