using System;
using UnityEngine;
using UnityEngine.UI;
using Vsens;

public class BodyShapeController : MonoBehaviour
{
    public VsensPlatform VsensPlatform;
    private Slider[] BetaSliders;

    private void Awake()
    {
        var sliders = GetComponentsInChildren<Slider>();
        // assert sliders count is 10
        if (sliders.Length != 10)
        {
            Debug.LogError($"BodyShapeController: {gameObject.name} has {sliders.Length} sliders, but expected 10.");
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
