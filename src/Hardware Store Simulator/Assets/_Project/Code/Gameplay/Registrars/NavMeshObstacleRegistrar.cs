using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshObstacle))]
    public sealed class NavMeshObstacleRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents() =>
            Entity.AddNavMeshObstacle(GetComponent<NavMeshObstacle>());

        public override void UnregisterComponents()
        {
            if (Entity.hasNavMeshObstacle)
                Entity.RemoveNavMeshObstacle();
        }
    }
}
