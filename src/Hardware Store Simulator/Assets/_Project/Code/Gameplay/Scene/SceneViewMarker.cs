using HardwareStore.Gameplay.Components;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EntityBehaviour))]
    public sealed class SceneViewMarker : MonoBehaviour
    {
        [SerializeField] private SceneViewId _id;

        public SceneViewId Id => _id;
        public EntityBehaviour View => ResolveView();

        public void Configure(SceneViewId id)
        {
            _id = id;
            ResolveView();
        }

        private EntityBehaviour ResolveView() =>
            GetComponent<EntityBehaviour>() ??
            throw new MissingComponentException(
                $"{name} requires {nameof(EntityBehaviour)} on the same GameObject.");
    }
}
