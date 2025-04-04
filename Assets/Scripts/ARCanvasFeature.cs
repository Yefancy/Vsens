using System;
using System.Collections;
using UnityEngine;

public class ARCanvasFeature : MonoBehaviour
{
    [SerializeField, Tooltip("The distance from the camera to the canvas.")] 
    private float distanceFromCamera = 0.45f;
    [SerializeField, Tooltip("On start / enable, place the canvas in front of the user")] 
    private bool MoveCanvasInFrontOfCamera = true;
    [SerializeField, Tooltip("The canvas group to fade in and out.")]
    private CanvasGroup CanvasGroup;
    [SerializeField, Tooltip("Face the canvas to the camera.")]
    private bool FaceToCamera = true;
    [SerializeField, Tooltip("Face the canvas to the camera.")]
    private bool HeadUp = true;
    [Tooltip("The duration of the fade in and out.")]
    public float FadeDuration = 0.25f;
    public Action OnCanvasClose;
    public Action OnCanvasOpen;
    
    public bool IsOpen => gameObject.activeSelf;
    
    public void SnapCanvasInFrontOfCamera()
    {
        var cameraRig = DevicesRef.Instance.CameraRigRef.CameraRig;
        transform.position = cameraRig.centerEyeAnchor.transform.position +
                             cameraRig.centerEyeAnchor.transform.forward * distanceFromCamera;
    }
    
    public IEnumerator SnapCanvasInFrontOfCameraCoroutine()
    {
        yield return 0; // wait one frame to make sure the camera is set up
        SnapCanvasInFrontOfCamera();
    }
    
    void Start()
    {
        if (MoveCanvasInFrontOfCamera)
        {
            StartCoroutine(SnapCanvasInFrontOfCameraCoroutine());
        }
    }

    void OnEnable()
    {
        if (MoveCanvasInFrontOfCamera)
        {
            StartCoroutine(SnapCanvasInFrontOfCameraCoroutine());
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (FaceToCamera)
        {
            var cameraRig = DevicesRef.Instance.CameraRigRef.CameraRig;
            transform.LookAt(cameraRig.centerEyeAnchor);
            // 180 degree rotation on y axis to face the camera
            transform.Rotate(0, 180, 0);
        } else if (HeadUp)
        {
            var forward = transform.forward;
            transform.LookAt(transform.position + forward);
        }
    }
    
    IEnumerator FadeCanvas(bool fadeIn, Action callback = null)
    {
        var start = fadeIn ? 0 : 1;
        var end = fadeIn ? 1 : 0;
        var duration = FadeDuration;
        var time = 0f;
        while (time < duration)
        {
            CanvasGroup.alpha = Mathf.Lerp(start, end, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        CanvasGroup.alpha = end;
        
        callback?.Invoke();
    }
    
    public void CloseCanvas()
    {
        if (!gameObject.activeSelf) return;
        StartCoroutine(FadeCanvas(false, () =>
        {
            gameObject.SetActive(false);
            OnCanvasClose?.Invoke();
        }));
    }
        
    public void OpenCanvas()
    {
        if (gameObject.activeSelf) return;
        gameObject.SetActive(true);
        OnCanvasOpen?.Invoke();
        SnapCanvasInFrontOfCamera();
        StartCoroutine(FadeCanvas(true));
    }
    
}