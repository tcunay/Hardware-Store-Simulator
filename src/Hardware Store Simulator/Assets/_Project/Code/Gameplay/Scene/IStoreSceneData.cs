using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public interface IStoreSceneData : IAudioService, INotificationService, IHudService,
        IDayNightPresentationService
    {
        bool IsRegistered { get; }

        Pose GetSpawnPoint(SpawnPointId id);
        Pose[] GetRoute(SceneRouteId id);
        CustomerFlowSceneLayout GetCustomerFlowLayout();
        EntityBehaviour GetSceneView(SceneViewId id);
        void Register(SpawnPointMarker[] spawnPoints, SceneRouteMarker[] routes,
            CustomerFlowLayoutMarker customerFlowLayout,
            SceneViewMarker[] sceneViews, PrototypeHudView hudView,
            PrototypeAudioView audioView, PrototypeDayNightView dayNightView);
        void Unregister();
    }
}
