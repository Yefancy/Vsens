using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEngine;

public class DevicesRef : MonoBehaviour
{
    private static DevicesRef INSTANCE;
    public static DevicesRef Instance => INSTANCE;

    [SerializeField, Interface(typeof(IOVRCameraRigRef))]
    private Object _cameraRigRef;
    public IOVRCameraRigRef CameraRigRef { get; private set; }
    [SerializeField, Interface(typeof(IHand))] 
    private Object _leftIHand;
    public IHand LeftHand { get; private set; }
    [SerializeField, Interface(typeof(IHand))] 
    private Object _rightIHand;
    public IHand RightHand { get; private set; }
    [SerializeField]
    private HandGrabInteractor _leftHandGrabInteractor;
    public HandGrabInteractor LeftHandGrabInteractor => _leftHandGrabInteractor;
    [SerializeField]
    private HandGrabInteractor _rightHandGrabInteractor;
    public HandGrabInteractor RightHandGrabInteractor => _rightHandGrabInteractor;
    

    public void Awake()
    {
        if (INSTANCE == null)
        {
            INSTANCE = this;
        }
        else
        {
            Destroy(this);
        }
        if (_leftIHand != null)
        {
            LeftHand = _leftIHand as IHand;
        }
        if (_rightIHand != null)
        {
            RightHand = _rightIHand as IHand;
        }
        if (_cameraRigRef != null)
        {
            CameraRigRef = _cameraRigRef as IOVRCameraRigRef;
        }
    }
    
}
