namespace HardwareStore.Common.Entity
{
    public static class CreateEntity
    {
        public static GameEntity Empty() =>
            Contexts.sharedInstance.game.CreateEntity();

        public static GameEntity Empty(int entityId) =>
            Empty().AddEntityId(entityId);
    }
}
