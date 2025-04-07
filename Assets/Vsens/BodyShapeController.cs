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
        BetaSliders = GetComponentsInChildren<Slider>();
        // assert sliders count is 10
        if (BetaSliders.Length != 10)
        {
            Debug.LogError($"BodyShapeController: {gameObject.name} has {BetaSliders.Length} sliders, but expected 10.");
        }
        else
        {
            for (int i = 0; i < BetaSliders.Length; i++)
            {
                BetaSliders[i].onValueChanged.AddListener(value => UpdateBodyShape());
            }
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    private void UpdateBodyShape()
    {
        var bodyShape = new float[BetaSliders.Length];
        for (var i = 0; i < BetaSliders.Length; i++)
        {
            bodyShape[i] = BetaSliders[i].value;
        }
        VsensPlatform.SetBodyShape(bodyShape);
    }

    public void Update()
    {
        var bodyShape = VsensPlatform.GetBodyShape();
        if (bodyShape.Length >= BetaSliders.Length)
        {
            for (int i = 0; i < BetaSliders.Length; i++)
            {
                BetaSliders[i].value = bodyShape[i];
            }
        }
    }
}
