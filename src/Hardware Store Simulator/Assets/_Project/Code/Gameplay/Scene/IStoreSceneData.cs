using System.Collections.Generic;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.Presentation;
using HardwareStore.Gameplay.Views;
using UnityEngine;

namespace HardwareStore.Gameplay.Scene
{
    public interface IStoreSceneData : IAudioService, INotificationService, IHudService
    {
        bool IsRegistered { get; }
        InteractionView OrderCounterView { get; }
        LoadingZoneView LoadingZoneView { get; }
        IReadOnlyList<ProductView> ProductViews { get; }

        Pose GetSpawnPoint(SpawnPointId id);
        void Register(SpawnPointMarker[] spawnPoints, InteractionView orderCounterView,
            LoadingZoneView loadingZoneView, ProductView[] productViews,
            PrototypeHudView hudView, PrototypeAudioView audioView);
        void Unregister();
    }
}
