using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransparentPreviewModel : IPreviewModel
{
    [SerializeField, Tooltip("The renderer of the model.")]
    private Renderer renderer;
    
    public override void SetAsPreviewView()
    {
        throw new System.NotImplementedException();
    }
}
