using System;
using UnityEngine;


public class BodySensorPlacement : MonoBehaviour
{
    public static Action<GameObject> OnSensorPlaced;
    public static Action<GameObject> OnSensorSelectEntered;
    public static Action<GameObject> OnSensorSelectExited;
    
    // protected override bool CanStartManipulationForGesture(TapGesture gesture)
    // {
    //     if (EventSystem.current.IsPointerOverGameObject())
    //     {
    //         return false;
    //     }
    //     
    //     if (!PlacementMode.Instance.IsPlacementMode) return false;
    //     if (PlacementMode.Instance.Selected == null) return false;
    //     
    //     return gesture.targetObject == gameObject;
    // }
    //
    // protected override void OnEndManipulation(TapGesture gesture)
    // {
    //     base.OnEndManipulation(gesture);
    //     if (gesture.isCanceled) return;
    //     var ray = xrOrigin.Camera.ScreenPointToRay(gesture.startPosition);
    //     Debug.DrawLine(ray.origin, ray.origin + ray.direction * 100, Color.yellow, 10);
    //     if (Physics.Raycast(ray, out var hit, Mathf.Infinity)) {
    //         Debug.DrawLine(hit.point, hit.point + hit.normal * 100, Color.green, 10);
    //         // Debug.DrawLine(pose.position, pose.position + pose.rotation * Vector3.forward * 100, Color.red, 10);
    //         var skinnedMeshRenderer = hit.collider.GetComponentInChildren<SkinnedMeshRenderer>();
    //         if (skinnedMeshRenderer != null && hit.collider is MeshCollider collider)
    //         {
    //             // Cache used values rather than accessing straight from the mesh on the loop below
    //             var sharedMesh = skinnedMeshRenderer.sharedMesh;
    //             var triangles = sharedMesh.triangles;
    //             var verticesIndex = triangles[hit.triangleIndex * 3 + 0];
    //             
    //             var bw = sharedMesh.boneWeights[verticesIndex];
    //             var boneIndex = bw.boneIndex0;
    //             var boneWeight = bw.weight0;
    //             if (bw.weight1 > boneWeight)
    //             {
    //                 boneIndex = bw.boneIndex1;
    //                 boneWeight = bw.weight1;
    //             }
    //             if (bw.weight2 > boneWeight)
    //             {
    //                 boneIndex = bw.boneIndex2;
    //                 boneWeight = bw.weight2;
    //             }
    //             if (bw.weight3 > boneWeight)
    //             {
    //                 boneIndex = bw.boneIndex3;
    //                 boneWeight = bw.weight3;
    //             }
    //             if (boneWeight > 0)
    //             {
    //                 var pose = new Pose(
    //                     hit.collider.transform.TransformPoint(collider.sharedMesh.vertices[verticesIndex]),
    //                     Quaternion.LookRotation(hit.normal));
    //                 var sensor = PlaceObject(pose, skinnedMeshRenderer.bones[boneIndex].gameObject);
    //                 var sensorDataCollector = sensor.GetComponentInChildren<SensorDataCollector>();
    //                 if (sensorDataCollector != null)
    //                 {
    //                     sensorDataCollector.boneMeshAttachment =
    //                         new BoneMeshAttachment(sensor, hit.triangleIndex, boneIndex, skinnedMeshRenderer, collider);
    //                 }
    //             }
    //         }
    //         else
    //         {
    //             var pose = new Pose(hit.point, Quaternion.LookRotation(hit.normal)); 
    //             PlaceObject(pose, hit.collider.gameObject);
    //         }
    //     }
    // }
    //
    // private GameObject PlaceObject(Pose pose, GameObject body)
    // {
    //     var sensorPrefab = PlacementMode.Instance.Selected;
    //     if (sensorPrefab == null)
    //     {
    //         Debug.LogWarning("No placement object prefab specified for AR Placement Interactable.");
    //         return null;
    //     }
    //
    //     var placementObject = Instantiate(sensorPrefab, pose.position, pose.rotation);
    //     var selectionIntractable = placementObject.GetComponent<ARSelectionInteractable>();
    //     if (selectionIntractable != null) {
    //         selectionIntractable.selectEntered.AddListener(args => {
    //             PlacementMode.Instance.SelectedSensor = placementObject;
    //             OnSensorSelectEntered?.Invoke(placementObject);
    //         });
    //         selectionIntractable.selectExited.AddListener(args => {
    //             PlacementMode.Instance.SelectedSensor = null;
    //             OnSensorSelectExited?.Invoke(placementObject);
    //         });
    //     }
    //     placementObject.transform.parent = body.transform;
    //     OnSensorPlaced?.Invoke(placementObject);
    //     return placementObject;
    // }
    
}
