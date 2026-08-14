using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class CustomerVisit : IComponent { }
    [Game] public class CustomerVehicle : IComponent { }
    [Game] public class Customer : IComponent { }
    [Game] public class RouteMover : IComponent { }
    [Game] public class CustomerVisitArriving : IComponent { }
    [Game] public class CustomerVisitQueued : IComponent { }
    [Game] public class CustomerVisitConsulting : IComponent { }
    [Game] public class CustomerVisitWaitingForLoadingBay : IComponent { }
    [Game] public class CustomerVisitMovingToLoadingBay : IComponent { }
    [Game] public class CustomerVisitLoading : IComponent { }
    [Game] public class CustomerVisitCompleted : IComponent { }
    [Game] public class CustomerVisitReturning : IComponent { }
    [Game] public class CustomerVisitDeparting : IComponent { }
    [Game] public class CustomerVisitAbandoning : IComponent { }
    [Game] public class CustomerVisitWaitingForAbandonDeparture : IComponent { }
    [Game] public class CustomerVisitAbandonDeparting : IComponent { }
    [Game] public class CustomerApproachingCounter : IComponent { }
    [Game] public class CustomerWaitingInQueue : IComponent { }
    [Game] public class CustomerWaitingAtCounter : IComponent { }
    [Game] public class CustomerReturningToVehicle : IComponent { }
    [Game] public class CustomerAbandonReturningToVehicle : IComponent { }
    [Game] public class CustomerPatienceWarningIssued : IComponent { }
    [Game] public class CustomerPatienceWarningEvent : IComponent { }
    [Game] public class CustomerAbandonedEvent : IComponent { }
    [Game] public class CustomerVisitStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class CustomerActorVisitEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class CustomerEventVisitEntityId : IComponent { public int Value; }
    [Game] public class CustomerProjectType : IComponent { public CustomerProjectTypeId Value; }
    [Game] public class CustomerCooldownRemaining : IComponent { public float Value; }
    [Game] public class CustomerPatienceRemaining : IComponent { public float Value; }
    [Game] public class CustomerDepartureDelayRemaining : IComponent { public float Value; }
    [Game] public class RouteComponent : IComponent { public Pose[] Value; }
    [Game] public class CustomerReturnRoute : IComponent { public Pose[] Value; }
    [Game] public class RouteWaypointIndex : IComponent { public int Value; }
    [Game] public class RotationSpeed : IComponent { public float Value; }
    [Game] public class WaypointTolerance : IComponent { public float Value; }
    [Game] public class RouteCompleted : IComponent { }
}
