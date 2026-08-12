using HardwareStore.Infrastructure.View.Registrars;
using UnityEngine;
using UnityEngine.AI;

namespace HardwareStore.Gameplay.Registrars
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshAgentRegistrar : EntityComponentRegistrar
    {
        public override void RegisterComponents() =>
            Entity.AddNavigationAgent(GetComponent<NavMeshAgent>());

        public override void UnregisterComponents()
        {
            if (Entity.hasNavigationAgent)
                Entity.RemoveNavigationAgent();
        }
    }
}
