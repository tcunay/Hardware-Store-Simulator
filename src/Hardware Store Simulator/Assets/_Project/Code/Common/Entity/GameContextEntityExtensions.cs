using System;

namespace HardwareStore.Common.Entity
{
    public static class GameContextEntityExtensions
    {
        public static GameEntity GetRequiredEntity(
            this GameContext context,
            int entityId,
            string relation)
        {
            GameEntity entity = context.GetEntityWithEntityId(entityId);
            if (entity == null)
                throw new InvalidOperationException(
                    $"Missing entity {entityId} referenced as {relation}.");

            return entity;
        }
    }
}
