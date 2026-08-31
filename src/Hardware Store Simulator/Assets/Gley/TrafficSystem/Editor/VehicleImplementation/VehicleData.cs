using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public struct VehicleData
    {
        public string PrefabPath;
        public ColliderProperties[] Colliders;

        public VehicleData(string prefabPath, ColliderProperties[] colliders)
        {
            PrefabPath = prefabPath;
            Colliders = colliders;
        }

        public VehicleData(string prefabPath)
        {
            PrefabPath = prefabPath;
            Colliders = new ColliderProperties[] { new(Vector3.zero, Vector3.zero, "Body") };
        }
    }

    public struct ColliderProperties
    {
        public Vector3 Center;
        public Vector3 Size;
        public string Name;
        public ColliderProperties(Vector3 center, Vector3 size, string name)
        {
            Center = center;
            Size = size;
            Name = name;
        }
    }
}
