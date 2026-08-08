using System;

namespace HardwareStore.Common.Extensions
{
    public static class FeatureExtensions
    {
        public static void Clear(this Feature feature, Action destructEntities = null)
        {
            feature.DeactivateReactiveSystems();
            feature.ClearReactiveSystems();

            destructEntities?.Invoke();

            feature.Cleanup();
            feature.TearDown();
        }
    }
}
