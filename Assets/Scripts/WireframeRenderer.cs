using UnityEngine;

public class WireframeRenderer : MonoBehaviour
{
    public Material wireframeMaterial;
    public MeshFilter meshFilter;
    
    private void OnRenderObject()
    {
        if (meshFilter == null || wireframeMaterial == null)
        {
            return;
        }
        
        wireframeMaterial.SetPass(0);
        Graphics.DrawMeshNow(meshFilter.sharedMesh, transform.localToWorldMatrix);
        
        GL.wireframe = true;
        GL.PushMatrix();
        GL.MultMatrix(transform.localToWorldMatrix);
        wireframeMaterial.SetPass(0);
        Graphics.DrawMeshNow(meshFilter.sharedMesh, Vector3.zero, Quaternion.identity);
        GL.PopMatrix();
        GL.wireframe = false;
    }

}
