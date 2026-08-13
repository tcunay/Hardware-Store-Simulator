using System;

namespace HardwareStore.Gameplay.Presentation
{
    public readonly struct CustomerFlowSnapshot
    {
        public CustomerFlowSnapshot(
            int totalActiveCount,
            int arrivingCount,
            int queuedCount,
            int consultingCount,
            int loadingPipelineCount,
            int leavingCount)
        {
            if (totalActiveCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalActiveCount));
            if (arrivingCount < 0)
                throw new ArgumentOutOfRangeException(nameof(arrivingCount));
            if (queuedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(queuedCount));
            if (consultingCount < 0)
                throw new ArgumentOutOfRangeException(nameof(consultingCount));
            if (loadingPipelineCount < 0)
                throw new ArgumentOutOfRangeException(nameof(loadingPipelineCount));
            if (leavingCount < 0)
                throw new ArgumentOutOfRangeException(nameof(leavingCount));

            int categorizedCount = checked(
                arrivingCount + queuedCount + consultingCount +
                loadingPipelineCount + leavingCount);
            if (categorizedCount != totalActiveCount)
            {
                throw new ArgumentException(
                    "Customer flow categories must account for every active visit.");
            }

            TotalActiveCount = totalActiveCount;
            ArrivingCount = arrivingCount;
            QueuedCount = queuedCount;
            ConsultingCount = consultingCount;
            LoadingPipelineCount = loadingPipelineCount;
            LeavingCount = leavingCount;
        }

        public int TotalActiveCount { get; }
        public int ArrivingCount { get; }
        public int QueuedCount { get; }
        public int ConsultingCount { get; }
        public int LoadingPipelineCount { get; }
        public int LeavingCount { get; }
    }
}
