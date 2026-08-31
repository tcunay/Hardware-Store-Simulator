using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class FantasticCityMethods : UnityEditor.Editor
    {
        public static void ExtractWaypoints(int maxSpeed, float greenLightTime, float yellowLightTime)
        {
            Type bridgeType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Gley.TrafficSystem.Editor.FantasticCityBridge"))
                .FirstOrDefault(type => type != null);

            if (bridgeType == null)
            {
                Debug.LogError("FantasticCityBridge was not found. Make sure the FCG-GleyBridge script is outside asmdef folders and compiles successfully.");
                return;
            }

            MethodInfo extractMethod = bridgeType.GetMethod(nameof(ExtractWaypoints), BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int), typeof(float), typeof(float) }, null);

            if (extractMethod == null)
            {
                Debug.LogError("FantasticCityBridge.ExtractWaypoints() was not found.");
                return;
            }

            extractMethod.Invoke(null, new object[] { maxSpeed, greenLightTime, yellowLightTime });
        }
    }
}
