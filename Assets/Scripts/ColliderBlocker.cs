using UnityEngine;

public class ColliderBlocker : MonoBehaviour
{
    [SerializeField, Tooltip("If true, all colliders of children will be enabled. If false, all colliders will be disabled.")]
    private bool isBlocking;
    public bool IsBlocking
    {
        get => isBlocking;
        set
        {
            isBlocking = value;
            foreach (var collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = !isBlocking;
            }
        }
    }
    
    void Start()
    {
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = !isBlocking;
        }
    }

}
