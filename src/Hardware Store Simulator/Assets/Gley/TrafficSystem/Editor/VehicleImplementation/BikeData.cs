using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public struct BikeData 
    {
        public string PrefabPath;
        public ColliderProperties[] Colliders;

        public BikeData(string prefabPath, ColliderProperties[] colliders)
        {
            PrefabPath = prefabPath;
            Colliders = colliders;
        }
    }
}
