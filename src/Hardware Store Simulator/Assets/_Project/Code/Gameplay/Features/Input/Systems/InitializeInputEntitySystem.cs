using Entitas;
using HardwareStore.Common.Entity;

namespace HardwareStore.Gameplay.Features.Input.Systems
{
    public sealed class InitializeInputEntitySystem : IInitializeSystem
    {
        public void Initialize() => CreateInputEntity.Empty().isInputState = true;
    }
}
