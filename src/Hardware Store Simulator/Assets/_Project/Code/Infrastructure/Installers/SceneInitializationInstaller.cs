using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace HardwareStore.Infrastructure.Installers
{
    public sealed class SceneInitializationInstaller : MonoInstaller
    {
        [SerializeField] private List<MonoBehaviour> _initializers = new();

        public IReadOnlyList<MonoBehaviour> Initializers => _initializers;

        public void Configure(params MonoBehaviour[] initializers) =>
            _initializers = new List<MonoBehaviour>(initializers);

        public override void InstallBindings()
        {
            foreach (MonoBehaviour initializer in _initializers)
                Container.BindInterfacesTo(initializer.GetType()).FromInstance(initializer).AsSingle();
        }
    }
}
