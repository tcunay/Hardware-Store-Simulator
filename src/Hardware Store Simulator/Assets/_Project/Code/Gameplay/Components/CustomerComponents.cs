using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class CustomerVisit : IComponent { }
    [Game] public class CustomerVehicle : IComponent { }
    [Game] public class CustomerVisitArriving : IComponent { }
    [Game] public class CustomerVisitWaiting : IComponent { }
    [Game] public class CustomerVisitLoading : IComponent { }
    [Game] public class CustomerVisitCompleted : IComponent { }
    [Game] public class CustomerVisitDeparting : IComponent { }
    [Game] public class CustomerVisitStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class CustomerVisitEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class CustomerCooldownRemaining : IComponent { public float Value; }
    [Game] public class CustomerDepartureDelayRemaining : IComponent { public float Value; }
    [Game] public class RouteComponent : IComponent { public Pose[] Value; }
    [Game] public class DepartureRoute : IComponent { public Pose[] Value; }
    [Game] public class RouteWaypointIndex : IComponent { public int Value; }
    [Game] public class RotationSpeed : IComponent { public float Value; }
    [Game] public class WaypointTolerance : IComponent { public float Value; }
    [Game] public class RouteCompleted : IComponent { }
}
