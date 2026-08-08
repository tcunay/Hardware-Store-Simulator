using HardwareStore.Gameplay.Components;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public sealed class SpawnPointMarker : MonoBehaviour
    {
        [SerializeField] private SpawnPointId _id;

        public SpawnPointId Id => _id;
        public Pose Pose => new(transform.position, transform.rotation);

        public void Configure(SpawnPointId id) =>
            _id = id;
    }
}
