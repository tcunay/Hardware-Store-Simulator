using Zenject;
using UnityEngine.SceneManagement;

namespace HardwareStore.Infrastructure.View.Factory
{
    public sealed class EntityViewFactory : IEntityViewFactory
    {
        private readonly IInstantiator _instantiator;
        private readonly DiContainer _container;

        public EntityViewFactory(IInstantiator instantiator, DiContainer container)
        {
            _instantiator = instantiator;
            _container = container;
        }

        public EntityBehaviour CreateViewFromPrefab(GameEntity entity)
        {
            EntityBehaviour view = _instantiator.InstantiatePrefabForComponent<EntityBehaviour>(
                entity.ViewPrefab,
                entity.SpawnPosition,
                entity.SpawnRotation,
                parentTransform: null);

            view.transform.SetParent(null, worldPositionStays: true);
            SceneManager.MoveGameObjectToScene(view.gameObject, SceneManager.GetActiveScene());
            return BindExistingView(entity, view);
        }

        public EntityBehaviour BindExistingView(GameEntity entity, EntityBehaviour view)
        {
            view.SetEntity(entity);
            return view;
        }

        public EntityBehaviour InjectAndBindExistingView(
            GameEntity entity,
            EntityBehaviour view)
        {
            _container.InjectGameObject(view.gameObject);
            return BindExistingView(entity, view);
        }
    }
}
