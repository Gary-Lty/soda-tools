using System;
using UnityEngine;

namespace Utils
{
    public class TestDrawWireCube : MonoBehaviour
    {
        public Color color = Color.white;
        public Vector3 size = Vector3.one;
        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position,size);
        }
    }
}