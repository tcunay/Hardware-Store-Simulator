using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Infrastructure.View;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public interface IStoreSceneData : IAudioService, INotificationService, IHudService
    {
        bool IsRegistered { get; }

        Pose GetSpawnPoint(SpawnPointId id);
        EntityBehaviour GetSceneView(SceneViewId id);
        void Register(SpawnPointMarker[] spawnPoints, SceneViewMarker[] sceneViews,
            PrototypeHudView hudView, PrototypeAudioView audioView);
        void Unregister();
    }
}
