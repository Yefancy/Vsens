using TransformHandles;
using UnityEngine;

namespace VsensAgent.RuntimeEditing
{
    [CreateAssetMenu(fileName = "RuntimeTransformHandleAssets", menuName = "VsensAgent/Runtime Transform Handle Assets")]
    public sealed class RuntimeTransformHandleAssetSet : ScriptableObject
    {
        [SerializeField] private GameObject transformHandlePrefab;
        [SerializeField] private GameObject ghostPrefab;

        public GameObject TransformHandlePrefab => transformHandlePrefab;
        public GameObject GhostPrefab => ghostPrefab;

        public bool IsValid(out string error)
        {
            if (transformHandlePrefab == null)
            {
                error = "Transform handle prefab is not assigned.";
                return false;
            }

            if (transformHandlePrefab.GetComponent<Handle>() == null)
            {
                error = $"Transform handle prefab '{transformHandlePrefab.name}' does not contain a Handle component.";
                return false;
            }

            if (ghostPrefab == null)
            {
                error = "Ghost prefab is not assigned.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
