using System;
using Oculus.Interaction.Samples;
using UnityEngine;

namespace Vsens.rendering
{
    [RequireComponent(typeof(MRPassthrough))]
    public class Passthrough : MonoBehaviour
    {
        public bool PassthroughEnabled = false;
        private MRPassthrough _mrPassthrough;

        private void Awake()
        {
            _mrPassthrough = GetComponent<MRPassthrough>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Update()
        {
            if (MRPassthrough.PassThrough.IsPassThroughOn != PassthroughEnabled)
            {
                _mrPassthrough.TogglePassThrough();
            }
        }
    }
}
