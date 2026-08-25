using UnityEngine;

namespace HardwareStore.Gameplay.Factories
{
    public interface IFreightFoundationFactory
    {
        void Create(Pose truckPose, Pose palletPose, int storeEntityId);
    }
}
