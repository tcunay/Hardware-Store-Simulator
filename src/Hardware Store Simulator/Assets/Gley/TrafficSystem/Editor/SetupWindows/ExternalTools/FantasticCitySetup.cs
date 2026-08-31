using Gley.UrbanSystem.Editor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

#if GLEY_TRAFFIC_SYSTEM
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;
#else
using VehicleTypes = Gley.TrafficSystem.VehicleTypes;
#endif

namespace Gley.TrafficSystem.Editor
{
    public class FantasticCitySetup : SetupWindowBase
    {
#if GLEY_FANTASTICCITY_TRAFFIC
        private float _greenLightTime = 10;
        private float _yellowLightTime = 3;
        private int _maxSpeed = 50;
#endif

        protected override void TopPart()
        {
            base.TopPart();
#if GLEY_FANTASTICCITY_TRAFFIC
            if (GUILayout.Button("Disable Fantastic City"))
            {
                Gley.Common.Editor.PreprocessorDirective.AddToCurrent(TrafficSystemConstants.GLEY_FANTASTICCITY_TRAFFIC, true);
            }
#else
            if (GUILayout.Button("Enable Fantastic City Support"))
            {
                Gley.Common.Editor.PreprocessorDirective.AddToCurrent(TrafficSystemConstants.GLEY_FANTASTICCITY_TRAFFIC, false);
            }
#endif
            EditorGUILayout.Space();
            if (GUILayout.Button("Download Fantastic City"))
            {
                Application.OpenURL("https://assetstore.unity.com/packages/3d/environments/urban/fantastic-city-generator-157625?aid=1011l8QY4");
            }
        }

        protected override void ScrollPart(float width, float height)
        {
            base.ScrollPart(width, height);
#if GLEY_FANTASTICCITY_TRAFFIC
            _greenLightTime = EditorGUILayout.FloatField("Green Light Time", _greenLightTime);
            _yellowLightTime = EditorGUILayout.FloatField("Yellow Light Time", _yellowLightTime);
            _maxSpeed = EditorGUILayout.IntField("Max Speed", _maxSpeed);

            EditorGUILayout.Space();
            if (GUILayout.Button("Extract Waypoints"))
            {
                List<int> vehicleTypes = System.Enum.GetValues(typeof(VehicleTypes)).Cast<int>().ToList();
                FantasticCityMethods.ExtractWaypoints(_maxSpeed, _greenLightTime, _yellowLightTime);
            }
#endif
        }
    }
}
