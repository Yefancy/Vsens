using UnityEngine;
using UnityEngine.EventSystems;

namespace SojaExiles
{
    public static class BpsPointerRaycast
    {
        public static bool IsPrimaryClickOn(MonoBehaviour owner)
        {
            return IsPrimaryClickOn(owner, null, maxDistance: -1f);
        }

        public static bool IsPrimaryClickOn(MonoBehaviour owner, Transform player, float maxDistance)
        {
            if (owner == null || !Input.GetMouseButtonDown(0))
            {
                return false;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                return false;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
                if (camera == null)
                {
                    return false;
                }
            }

            var ray = camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity))
            {
                return false;
            }

            var hitTransform = hit.collider != null ? hit.collider.transform : null;
            if (hitTransform == null)
            {
                return false;
            }

            var isSelfOrChild = hitTransform == owner.transform || hitTransform.IsChildOf(owner.transform);
            if (!isSelfOrChild)
            {
                return false;
            }

            if (maxDistance >= 0f)
            {
                if (player == null)
                {
                    return false;
                }

                var distance = Vector3.Distance(player.position, owner.transform.position);
                if (distance >= maxDistance)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
