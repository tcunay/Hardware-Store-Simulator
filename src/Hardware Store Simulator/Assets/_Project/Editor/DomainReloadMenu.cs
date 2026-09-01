using UnityEditor;

namespace HardwareStore.Editor
{
    public static class DomainReloadMenu
    {
        private const string MenuPath = "Tools/Hardware Store/Reload Domain";

        [MenuItem(MenuPath, priority = 200)]
        private static void ReloadDomain() => EditorUtility.RequestScriptReload();
    }
}
