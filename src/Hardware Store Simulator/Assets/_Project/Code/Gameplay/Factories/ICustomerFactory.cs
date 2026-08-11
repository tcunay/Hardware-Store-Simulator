using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface ICustomerFactory
    {
        GameEntity Create(GameEntity customerVisit, Pose[] routeToCounter,
            Pose[] routeToVehicle);
    }
}
