using UnityEngine;

// Serialized legacy component: the shared space navigation replaces this signal.
public class StationSignalIndicator : MonoBehaviour
{
    void OnEnable() { gameObject.SetActive(false); }
}
