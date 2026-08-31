using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    public enum TrafficControlPolicyId
    {
        Uncontrolled,
        BrakeOnly,
        NavMesh,
        Coupled,
    }

    public enum TrafficPriorityId
    {
        CustomerVehicle = 40,
        CustomerPedestrian = 50,
        WarehouseWorker = 60,
        PlayerVehicle = 90,
        Player = 100,
    }

    [Game] public class TrafficParticipant : IComponent { }
    [Game] public class TrafficYielding : IComponent { }
    [Game] public class TrafficControlPolicy : IComponent { public TrafficControlPolicyId Value; }
    [Game] public class TrafficPriority : IComponent { public int Value; }
    [Game] public class TrafficDesiredVelocity : IComponent { public Vector3 Value; }
    [Game] public class TrafficIntentDistance : IComponent { public float Value; }
    [Game] public class TrafficAngularIntent : IComponent { public float Value; }
    [Game] public class TrafficPreviousPosition : IComponent { public Vector3 Value; }
    [Game] public class TrafficCurrentSpeed : IComponent { public float Value; }
    [Game] public class TrafficConflictEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class TrafficConflictColliderComponent : IComponent { public Collider Value; }
}
