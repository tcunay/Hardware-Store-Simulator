using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface ICustomerVisitFactory
    {
        GameEntity Create(GameEntity store, Pose[] arrivalRoute, Pose[] departureRoute);
    }
}
