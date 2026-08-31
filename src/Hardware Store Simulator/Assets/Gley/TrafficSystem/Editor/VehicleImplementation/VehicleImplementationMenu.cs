
using Gley.Common.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class VehicleImplementationMenu : EditorWindow
    {
        private const string WINDOW_NAME = "Vehicle Implementation - v.";
        private const int MIN_WIDTH = 400;
        private const int MIN_HEIGHT = 500;

        static VehicleImplementationMenu _implementationWindow;

        private ExternalVehiclePackages _selectedPackage = ExternalVehiclePackages.MobileTrafficTruckByGley;
        private string _sourceFolder = "";
        private string _destinationFolder = "";

        private static readonly Dictionary<ExternalVehiclePackages, (Func<PackageIconReferences, Texture2D> getIcon, string url, string label)> _packageInfo =
            new Dictionary<ExternalVehiclePackages, (Func<PackageIconReferences, Texture2D>, string, string)>()
            {
                { ExternalVehiclePackages.GridlockVehiclePackByPolyNinja,   (refs => refs.GridlockIcon,             "http://assetstore.unity.com/packages/3d/vehicles/gridlock-city-traffic-vehicle-pack-196085?aid=1011l8QY4", "Get Gridlock Vehicle Pack") },
                { ExternalVehiclePackages.MobileTrafficTruckByGley,         (refs => refs.MobileTrafficTruckIcon,   "https://assetstore.unity.com/packages/slug/273684?aid=1011l8QY4",                                          "Get Mobile Traffic Truck") },
                { ExternalVehiclePackages.CompleteVehiclePackBySICS,        (refs => refs.CompleteVehiclePackIcon,  "https://assetstore.unity.com/packages/3d/vehicles/land/complete-vehicle-pack-51460?aid=1011l8QY4",         "Get Complete Vehicle Pack") },
                { ExternalVehiclePackages.CompleteVehiclePackV2BySICS,      (refs => refs.CompleteVehiclePackIconV2,"https://assetstore.unity.com/packages/3d/vehicles/land/complete-vehicle-pack-v2-54560?aid=1011l8QY4",      "Get Complete Vehicle Pack V2") },
                { ExternalVehiclePackages.ToonVehiclesBySICS,               (refs => refs.ToonVehicles,             "https://assetstore.unity.com/packages/3d/vehicles/land/toon-vehicles-85104?aid=1011l8QY4",                 "Get Toon Vehicles") },
                { ExternalVehiclePackages.ToonRacingBySICS,                 (refs => refs.ToonRacing,               "https://assetstore.unity.com/packages/3d/vehicles/land/toon-racing-102727?aid=1011l8QY4",                  "Get Toon Racing") },
                { ExternalVehiclePackages.ToonCityBySICS,                   (refs => refs.ToonCity,                 "https://assetstore.unity.com/packages/3d/environments/urban/toon-city-88379?aid=1011l8QY4",                "Get Toon City") },
                { ExternalVehiclePackages.ToonIndustriesBySICS,             (refs => refs.ToonIndustries,           "https://assetstore.unity.com/packages/3d/environments/industrial/toon-industries-115109?aid=1011l8QY4",    "Get Toon Industries") },
                { ExternalVehiclePackages.ToonSuburbanPackBySICS,           (refs => refs.ToonSuburbanPack,         "https://assetstore.unity.com/packages/3d/environments/urban/toon-suburban-pack-198146?aid=1011l8QY4",      "Get Toon Suburban Pack") },
                { ExternalVehiclePackages.ToonPlanesPackBySICS,             (refs => refs.ToonPlanes,               "https://assetstore.unity.com/packages/3d/environments/industrial/toon-planes-pack-86945?aid=1011l8QY4",    "Get Toon Planes Pack") },
                { ExternalVehiclePackages.BigCityTrafficCarsByPackNekoNest, (refs => refs.BigCityTrafficCarsPack,   "https://assetstore.unity.com/packages/3d/vehicles/big-city-traffic-cars-pack-mobile-ready-modern-land-vehicles-312174?aid=1011l8QY4",    "Get Big City Traffic Cars Pack") },
                { ExternalVehiclePackages.FantasticCityByMasterPixel3D,     (refs => refs.FantasticCity,            "https://assetstore.unity.com/packages/3d/environments/urban/fantastic-city-generator-157625?aid=1011l8QY4","Get Fantastic City Generator") },

            };

        [MenuItem("Tools/Gley/Vehicle Implementation", false)]
        public static void Initialize()
        {
            _implementationWindow = LoadWindow<VehicleImplementationMenu>(WINDOW_NAME, MIN_WIDTH, MIN_HEIGHT, new Version());
        }

        private static T LoadWindow<T>(string WINDOW_NAME, int MIN_WIDTH, int MIN_HEIGHT, IVersion version) where T : EditorWindow
        {
            T window = (T)GetWindow(typeof(T));
            window.titleContent = new GUIContent(WINDOW_NAME + version.LongVersion);
            window.minSize = new Vector2(MIN_WIDTH, MIN_HEIGHT);
            window.Show();
            return (T)Convert.ChangeType(window, typeof(T));
        }

        private void OnEnable()
        {
            _implementationWindow = this;
        }


        private void OnGUI()
        {
            if (_implementationWindow == null)
            {
                Initialize();
                return;
            }
            // Label + dropdown for the enum
            EditorGUILayout.LabelField("Select Vehicle Package", EditorStyles.boldLabel);
            _selectedPackage = (ExternalVehiclePackages)EditorGUILayout.EnumPopup("Vehicle Package:", _selectedPackage);

            // Optional: react to the selection
            EditorGUILayout.Space();
            DrawPackageInfoBox(_selectedPackage);

            EditorGUILayout.Space(20);

            // Source folder
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            DrawFolderField("Package Root Folder:", ref _sourceFolder, "Select the root folder of the external package");

            EditorGUILayout.Space(10);

            // Destination folder
            EditorGUILayout.LabelField("Destination", EditorStyles.boldLabel);
            DrawFolderField("Output Folder:", ref _destinationFolder, "Select the folder where vehicle prefabs will be generated");

            EditorGUILayout.Space(20);

            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_sourceFolder) || string.IsNullOrEmpty(_destinationFolder));
            if (GUILayout.Button("Convert Vehicles", GUILayout.Height(35)))
            {
                ConvertVehicles();
            }
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPackageInfoBox(ExternalVehiclePackages package)
        {
            if (!_packageInfo.TryGetValue(package, out var info))
                return;

            PackageIconReferences iconRefs = GetIconReferences();
            Texture2D icon = iconRefs != null ? info.getIcon(iconRefs) : null;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            if (icon != null)
            {
                GUILayout.Label(icon, GUILayout.Width(50), GUILayout.Height(50));
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"Selected: {package}", EditorStyles.boldLabel);
            if (GUILayout.Button(info.label))
            {
                Application.OpenURL(info.url);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private IVehicleConverter GetConverter() => _selectedPackage switch
        {
            ExternalVehiclePackages.GridlockVehiclePackByPolyNinja => new GridlockConverter(),
            ExternalVehiclePackages.MobileTrafficTruckByGley => new MobileTrafficTruckConverter(),
            ExternalVehiclePackages.CompleteVehiclePackBySICS => new CompleteVehiclePackConverter(),
            ExternalVehiclePackages.CompleteVehiclePackV2BySICS => new CompleteVehiclePackV2Converter(),
            ExternalVehiclePackages.ToonVehiclesBySICS => new ToonVehiclesConverter(),
            ExternalVehiclePackages.ToonRacingBySICS => new ToonRacingConverter(),
            ExternalVehiclePackages.ToonCityBySICS => new ToonCityConverter(),
            ExternalVehiclePackages.ToonIndustriesBySICS => new ToonIndustriesConverter(),
            ExternalVehiclePackages.ToonSuburbanPackBySICS => new ToonSuburbanPackConverter(),
            ExternalVehiclePackages.ToonPlanesPackBySICS => new ToonPlanesPackConverter(),
            ExternalVehiclePackages.BigCityTrafficCarsByPackNekoNest => new BigCityCarsConverter(),
            ExternalVehiclePackages.FantasticCityByMasterPixel3D => new FantasticCityConverter(),
            _ => throw new NotImplementedException($"No converter for {_selectedPackage}")
        };

        private void ConvertVehicles()
        {
            Debug.Log($"Converting {_selectedPackage} vehicles from '{_sourceFolder}' to '{_destinationFolder}'");

            GetConverter().Convert(_sourceFolder, _destinationFolder);
        }

        private void DrawFolderField(string label, ref string path, string panelTitle)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(label, GUILayout.Width(120));

            // Read-only text field showing the path
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(path);
            EditorGUI.EndDisabledGroup();

            // Browse button
            if (GUILayout.Button("Browse", GUILayout.Width(65)))
            {
                string selected = EditorUtility.OpenFolderPanel(panelTitle, path, "");
                if (!string.IsNullOrEmpty(selected))
                {
                    // Convert absolute path to project-relative (Assets/...) if inside the project
                    if (selected.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + selected.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        path = selected;
                    }
                    GUI.FocusControl(null);
                    Repaint();
                }
            }

            EditorGUILayout.EndHorizontal();

            // Show the full path below as a hint if it's long
            if (!string.IsNullOrEmpty(path))
            {
                EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
            }
        }

        private PackageIconReferences _iconReferences;

        private PackageIconReferences GetIconReferences()
        {
            if (_iconReferences != null)
                return _iconReferences;

            string[] guids = AssetDatabase.FindAssets("t:PackageIconReferences");
            if (guids.Length == 0)
            {
                Debug.LogWarning("PackageIconReferences asset not found in project.");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _iconReferences = AssetDatabase.LoadAssetAtPath<PackageIconReferences>(path);
            return _iconReferences;
        }
    }
}
