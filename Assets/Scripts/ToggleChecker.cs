using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ToggleChecker : MonoBehaviour
{
    [SerializeField] private Toggle _toggle;
    [SerializeField] private List<GameObject> _objectsToToggle;
    [SerializeField] private bool reverseToggle;
    
    // Start is called before the first frame update
    void Awake()
    {
        if (_toggle == null)
        {
            _toggle = GetComponent<Toggle>();
        }
        if (_toggle != null)
        {
            _toggle.onValueChanged.AddListener(isOn =>
            {
                _objectsToToggle?.ForEach(obj => obj.SetActive(reverseToggle ? !isOn : isOn));
            });
        }
    }
}
