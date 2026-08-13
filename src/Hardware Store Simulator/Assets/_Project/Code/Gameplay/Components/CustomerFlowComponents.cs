using Entitas;
using Entitas.CodeGeneration.Attributes;
using UnityEngine;

namespace HardwareStore.Gameplay.Components
{
    [Game] public class CustomerParkingSpot : IComponent { }
    [Game] public class CustomerQueueSpot : IComponent { }
    [Game] public class CustomerLoadingBay : IComponent { }
    [Game] public class CustomerTrafficLane : IComponent { }

    [Game] public class CustomerParkingSpotStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class CustomerQueueSpotStoreEntityId : IComponent { [EntityIndex] public int Value; }
    [Game] public class CustomerLoadingBayStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class CustomerTrafficLaneStoreEntityId : IComponent { [PrimaryEntityIndex] public int Value; }

    [Game] public class ReservedCustomerParkingSpotEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ReservedCustomerQueueSpotEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ReservedCustomerLoadingBayEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ReservedCustomerTrafficLaneEntityId : IComponent { [PrimaryEntityIndex] public int Value; }
    [Game] public class ServingOrderCounterEntityId : IComponent { [PrimaryEntityIndex] public int Value; }

    [Game] public class ParkingSpotIndex : IComponent { public int Value; }
    [Game] public class QueueSpotIndex : IComponent { public int Value; }
    [Game] public class CustomerArrivalSequence : IComponent { public int Value; }
    [Game] public class NextCustomerArrivalSequence : IComponent { public int Value; }

    [Game] public class CustomerVehicleArrivalRoute : IComponent { public Pose[] Value; }
    [Game] public class CustomerVehicleToLoadingRoute : IComponent { public Pose[] Value; }
    [Game] public class CustomerApproachRoute : IComponent { public Pose[] Value; }
    [Game] public class CustomerLoadingDepartureRoute : IComponent { public Pose[] Value; }
}
