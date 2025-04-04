using UnityEngine;

public class SkinCollider : MonoBehaviour
{
    [Tooltip("schedule an update of the collision mesh on the next frame")]
    public bool needUpdate = true;
    [Tooltip("update the collision mesh only once per frame")]
    public bool updateOncePerFrame = true;
   
    // private CWeightList[] nodeWeights; // one per node
   
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private MeshCollider meshCollider;
   
   
    /// <summary>
    ///  This basically translates the information about the skinned mesh into
    /// data that we can internally use to quickly update the collision mesh.
    /// </summary>
    void Start()
    {
        skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
       
        if (meshCollider != null && skinnedMeshRenderer != null)
        {
            UpdateCollisionMesh();
        }
        else
        {
            Debug.LogError("[SkinnedCollisionHelper] "+ gameObject.name +" is missing SkinnedMeshRenderer or MeshCollider!");
        }
   
    }
    
    /// <summary>
    /// Manually recalculates the collision mesh of the skinned mesh on this object.
    /// </summary>
    public void UpdateCollisionMesh()
    {
        if (meshCollider.sharedMesh == null)
        {
            meshCollider.sharedMesh = new Mesh();
        }
        skinnedMeshRenderer.BakeMesh(meshCollider.sharedMesh, true);
        // ask the mesh collider to recalculate its normals
        meshCollider.sharedMesh = meshCollider.sharedMesh;
    }
   
    /// <summary>
    /// If the 'forceUpdate' flag is set, updates the collision mesh for the skinned mesh on this object
    /// </summary>
    void Update()
    {
        if (needUpdate)
        {
            if (updateOncePerFrame) needUpdate = false;
            UpdateCollisionMesh();
        }
    }

    public void ScheduleColliderUpdating(bool imediately = false)
    {
        if (imediately)
        {
            UpdateCollisionMesh();
        }
        else
        {
            needUpdate = true;
        }
    }
}
