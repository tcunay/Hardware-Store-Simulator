using UnityEngine;

namespace HardwareStore.Gameplay.Common.Cursor
{
    public sealed class UnityCursorService : ICursorService
    {
        public bool IsLocked => UnityEngine.Cursor.lockState == CursorLockMode.Locked;

        public void SetLocked(bool locked)
        {
            UnityEngine.Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            UnityEngine.Cursor.visible = !locked;
        }
    }
}
