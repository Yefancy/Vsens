using UnityEngine;

public class IPreviewModel : MonoBehaviour
{
    [SerializeField]
    [Tooltip("offset from the surface of the object to the surface of the plane when placing the object")]
    private float normalOffset;
    
    public virtual void SetAsPreviewView()
    {
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
    }

    public virtual bool CanBePlacedAt(Ray ray, RaycastHit hit)
    {
        return true;
    }
    
    public virtual Pose GetPlacementPose(Ray ray, RaycastHit hit)
    {
        return new Pose(hit.point + hit.normal.normalized * normalOffset, Quaternion.FromToRotation(Vector3.up, hit.normal));
    }
    
    public virtual void OnPlaced()
    {
       
    }
}
