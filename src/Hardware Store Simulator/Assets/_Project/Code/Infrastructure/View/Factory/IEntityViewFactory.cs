namespace HardwareStore.Infrastructure.View.Factory
{
    public interface IEntityViewFactory
    {
        EntityBehaviour CreateViewFromPrefab(GameEntity entity);
        EntityBehaviour BindExistingView(GameEntity entity, EntityBehaviour view);
    }
}
