namespace HardwareStore.Gameplay.Common.Cursor
{
    public interface ICursorService
    {
        bool IsLocked { get; }

        void SetLocked(bool locked);
    }
}
