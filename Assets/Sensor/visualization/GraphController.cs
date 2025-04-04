using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sensor.visualization
{
    
    public abstract class GraphController<T> : MonoBehaviour
    {
        [Tooltip("maximum cache data in seconds")]
        public int cacheTime = 4;
        [SerializeField]
        private Transform target;
        [SerializeField]
        private LineRenderer lineRenderer;
        [SerializeField]
        private List<Transform> avaliablePositions;
        
        // runtime
        protected readonly List<Tuple<float, T>> cacheData = new();
        
        protected virtual void Start()
        {
            gameObject.transform.parent = null;
        }

        public virtual void UploadData(float time, T data)
        {
            cacheData.Add(new Tuple<float, T>(time, data));
            while (cacheData.Count > 1 && cacheData[0].Item1 < time - cacheTime)
            {
                cacheData.RemoveAt(0);
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            var distance = Vector3.Distance(avaliablePositions[0].position, target.position);
            var bestPosition = avaliablePositions[0].position;
            foreach (var position in avaliablePositions)
            {
                var newDistance = Vector3.Distance(position.position, target.position);
                if (newDistance < distance)
                {
                    distance = newDistance;
                    bestPosition = position.position;
                }
            }

            lineRenderer.transform.position = Vector3.zero;
            lineRenderer.transform.rotation = Quaternion.identity;
            lineRenderer.SetPosition(0, target.position);
            lineRenderer.SetPosition(1, bestPosition);
        }
    }
}