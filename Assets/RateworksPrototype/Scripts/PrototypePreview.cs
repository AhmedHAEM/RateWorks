using UnityEngine;
namespace RateworksPrototype
{
    // Lets the authored production line be inspected without entering Play mode.
    public sealed class PrototypePreview : MonoBehaviour { void Awake() { gameObject.SetActive(false); } }
}
