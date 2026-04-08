using UnityEngine;
using UnityEngine.UI;
using TransformHandles;

public class TransformGizmoUI : MonoBehaviour
{
    public ToggleGroup toggleGroup;
    
    public Toggle translateToggle;
    public Image translateBG;
    public Toggle rotateToggle;
    public Image rotateBG;

    [Header("Colors")]
    public Color activeColor = new Color(0.32f, 0.78f, 0.32f, 1f);
    public Color inactiveColor = Color.white;

    private bool _initialized;

    public void Initialize(System.Action<HandleType> onHandleTypeSelected)
    {
        if (_initialized)
        {
            translateToggle?.onValueChanged.RemoveAllListeners();
            rotateToggle?.onValueChanged.RemoveAllListeners();
        }

        if (translateToggle != null)
        {
            translateToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                {
                    onHandleTypeSelected?.Invoke(HandleType.Position);
                }
            });
        }

        if (rotateToggle != null)
        {
            rotateToggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                {
                    onHandleTypeSelected?.Invoke(HandleType.Rotation);
                }
            });
        }

        _initialized = true;
    }

    public void SetMode(HandleType handleType)
    {
        bool translateActive = handleType != HandleType.Rotation;
        bool rotateActive = handleType == HandleType.Rotation;

        if (translateToggle != null)
        {
            translateToggle.SetIsOnWithoutNotify(translateActive);
        }

        if (rotateToggle != null)
        {
            rotateToggle.SetIsOnWithoutNotify(rotateActive);
        }

        if (translateBG != null)
        {
            translateBG.color = translateActive ? activeColor : inactiveColor;
        }

        if (rotateBG != null)
        {
            rotateBG.color = rotateActive ? activeColor : inactiveColor;
        }
    }
}
