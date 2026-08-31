#if GLEY_FANTASTICCITY_TRAFFIC
using FCG;
using Gley.TrafficSystem.User;
using Gley.UrbanSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Gley.TrafficSystem.Editor
{
    public class FantasticCityBridge : UnityEditor.Editor
    {
        private const string FantasticCityIntersectionsHolder = "TrafficSystem/EditorData/FantasticCityIntersections";
        private static readonly Dictionary<FCGWaypointsContainer, Dictionary<int, List<WaypointSettings>>> _created = new();
        private static readonly Dictionary<TFShiftHand2, TrafficLightsIntersectionSettings> _createdIntersections = new();

        public static void ExtractWaypoints(int maxSpeed, float greenLightTime, float yellowLightTime)
        {
            Debug.Log("ExtractWaypoints");
            List<int> vehicleTypes = System.Enum.GetValues(typeof(VehicleTypes)).Cast<int>().ToList();

            var ts = FindObjectOfType<FCG.TrafficSystem>();
            if (ts == null)
            {
                Debug.LogWarning("No Traffic System found for Fantastic City. Enable traffic before extracting waypoints");
                return;
            }
            ts.DeffineDirection(ts.trafficLightHand);
            ts.UpdateAllWayPoints();
            ts.enabled = false;

            string holderPath = "TrafficSystem/EditorData/FantasticCityEditorWaypoints";
            DestroyImmediate(GameObject.Find(holderPath));

            Transform holder = MonoBehaviourUtilities.GetOrCreateGameObject(holderPath, true).transform;

            var containers = MonoBehaviourUtilities.FindObjects<FCGWaypointsContainer>();

            _created.Clear();

            for (int i = 0; i < containers.Length; i++)
            {
                CreateGleyRoadFromFCG(containers[i], i, holder, vehicleTypes, maxSpeed);
            }

            LinkFCGRoadConnections();

            CreateIntersections(greenLightTime, yellowLightTime, vehicleTypes, maxSpeed);

            AddAdditionalWaypoints(vehicleTypes);
            AddAdditionalWaypoints(vehicleTypes);

            Debug.Log("Fantastic City waypoints extracted: " + containers.Length);
        }

        private static void AddAdditionalWaypoints(List<int> vehicleTypes)
        {
            WaypointSettings[] allWaypoints = MonoBehaviourUtilities.FindObjects<WaypointSettings>();

            for (int i = 0; i < allWaypoints.Length; i++)
            {
                allWaypoints[i].distance = new List<int>();
                for (int j = 0; j < allWaypoints[i].neighbors.Count; j++)
                {
                    allWaypoints[i].distance.Add((int)Vector3.Distance(allWaypoints[i].transform.position, allWaypoints[i].neighbors[j].transform.position));
                }
            }


            foreach (var wp in allWaypoints)
            {
                if (wp.distance.Count == 0)
                {
                    continue;
                }
                if (wp.distance.Count > 1)
                {
                    continue;
                }
                if (wp.distance[0] < 10)
                {
                    continue;
                }

               
                AddMiddleWaypoint(wp, vehicleTypes);
            }
        }

        private static void AddMiddleWaypoint(WaypointSettings wp, List<int> vehicleTypes)
        {
            WaypointSettings next = wp.neighbors[0] as WaypointSettings;
            if (next == null)
                return;

            Vector3 middlePosition = (wp.transform.position + next.transform.position) / 2f;

            var waypointCreator = new TrafficWaypointCreator();
            Transform newWp = waypointCreator.CreateWaypoint(
                wp.transform.parent,
                middlePosition,
                wp.name + "_Mid",
                vehicleTypes,
                wp.maxSpeed,
                wp.laneWidth);

            WaypointSettings newWaypoint = newWp.GetComponent<WaypointSettings>();

            // disconnect wp from next
            wp.neighbors.Remove(next);
            next.prev.Remove(wp);

            // connect wp -> newWaypoint -> next
            wp.neighbors.Add(newWaypoint);
            newWaypoint.prev.Add(wp);
            newWaypoint.neighbors.Add(next);
            next.prev.Add(newWaypoint);
        }

        private static void CreateIntersections(float greenLightTime, float yellowLightTime, List<int> vehicleTypes, int maxSpeed)
        {
            _createdIntersections.Clear();
            ClearPreviousIntersections();

            Transform holder = MonoBehaviourUtilities.GetOrCreateGameObject($"{TrafficSystemConstants.PACKAGE_NAME}/{UrbanSystemConstants.EDITOR_HOLDER}/FantasticCityIntersections", true).transform;

            var shiftedLights = MonoBehaviourUtilities.FindObjects<TFShiftHand2>();

            for (int i = 0; i < shiftedLights.Length; i++)
            {
                TFShiftHand2 shifted = shiftedLights[i];
                TrafficLights2 fcgLights = GetActiveTrafficLights(shifted);

                if (fcgLights == null)
                {
                    Debug.LogWarning($"No active TrafficLights2 found for {shifted.name}", shifted);
                    continue;
                }

                GameObject intersectionObject = MonoBehaviourUtilities.CreateGameObject(
                    $"FCG_TrafficLightsIntersection_{i}",
                    holder,
                    fcgLights.transform.position,
                    true);

                var intersection = intersectionObject.AddComponent<TrafficLightsIntersectionSettings>();
                intersection.Initialize();

                intersection.position = intersectionObject.transform.position;
                intersection.greenLightTime = greenLightTime;
                intersection.yellowLightTime = yellowLightTime;
                intersection.stopWaypoints = new List<IntersectionStopWaypointsSettings>();

                IntersectionStopWaypointsSettings stopSettings = new IntersectionStopWaypointsSettings();
                fcgLights.enabled = false;
                if (fcgLights.trafficLight_N.gameObject.activeSelf)
                {
                    stopSettings = new IntersectionStopWaypointsSettings();
                    AddLightObjects(stopSettings, fcgLights.trafficLight_N);
                    AssignStopWaypoint(stopSettings, fcgLights.trafficLight_N, intersection.transform.position, vehicleTypes);
                    intersection.stopWaypoints.Add(stopSettings);
                }

                if (fcgLights.trafficLight_S.gameObject.activeSelf)
                {
                    stopSettings = new IntersectionStopWaypointsSettings();
                    AddLightObjects(stopSettings, fcgLights.trafficLight_S);
                    AssignStopWaypoint(stopSettings, fcgLights.trafficLight_S, intersection.transform.position, vehicleTypes);
                    intersection.stopWaypoints.Add(stopSettings);
                }

                if (fcgLights.trafficLight_E.gameObject.activeSelf)
                {
                    stopSettings = new IntersectionStopWaypointsSettings();
                    AddLightObjects(stopSettings, fcgLights.trafficLight_E);
                    AssignStopWaypoint(stopSettings, fcgLights.trafficLight_E, intersection.transform.position, vehicleTypes);
                    intersection.stopWaypoints.Add(stopSettings);
                }

                if (fcgLights.trafficLight_W.gameObject.activeSelf)
                {
                    stopSettings = new IntersectionStopWaypointsSettings();
                    AddLightObjects(stopSettings, fcgLights.trafficLight_W);
                    AssignStopWaypoint(stopSettings, fcgLights.trafficLight_W, intersection.transform.position, vehicleTypes);
                    intersection.stopWaypoints.Add(stopSettings);
                }

                _createdIntersections.Add(shifted, intersection);

                EditorUtility.SetDirty(intersection);
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"Created {_createdIntersections.Count} FCG traffic light intersections");
        }

        private static void AssignStopWaypoint(IntersectionStopWaypointsSettings stopSettings, FCG.TrafficLight fcgLight, Vector3 intersectionPosition, List<int> vehicleTypes)
        {
            WaypointSettings stopWaypoint = CreateStopWaypoint(fcgLight, intersectionPosition, vehicleTypes);

            if (stopWaypoint == null)
            {
                Debug.LogWarning($"No stop waypoint created for {fcgLight.name}", fcgLight);
                return;
            }

            if (!stopSettings.roadWaypoints.Contains(stopWaypoint))
            {
                stopSettings.roadWaypoints.Add(stopWaypoint);
            }
        }

        private static WaypointSettings CreateStopWaypoint(FCG.TrafficLight fcgLight, Vector3 intersectionPosition, List<int> vehicleTypes)
        {
            Vector3 stopPosition = fcgLight.StopCollider != null
                ? fcgLight.StopCollider.transform.position
                : fcgLight.transform.position;

            // find the waypoint closest to the stop position that is moving toward the intersection
            WaypointSettings[] allWaypoints = MonoBehaviourUtilities.FindObjects<WaypointSettings>();
            WaypointSettings insertAfter = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < allWaypoints.Length; i++)
            {
                WaypointSettings waypoint = allWaypoints[i];
                if (waypoint == null)
                    continue;

                if (waypoint.neighbors == null || waypoint.neighbors.Count == 0)
                    continue;

                Vector3 forward = GetWaypointForward(waypoint);
                Vector3 toIntersection = (intersectionPosition - waypoint.transform.position).normalized;

                if (Vector3.Dot(forward, toIntersection) < 0.35f)
                    continue;

                float score = Vector3.Distance(waypoint.transform.position, stopPosition);
                if (score < bestScore)
                {
                    bestScore = score;
                    insertAfter = waypoint;
                }
            }

            if (insertAfter == null)
            {
                Debug.LogWarning($"No suitable waypoint found to insert stop waypoint for {fcgLight.name}", fcgLight);
                return null;
            }

            // create the new waypoint at the stop collider position
            var waypointCreator = new TrafficWaypointCreator();
            string name = "StopWaypoint_" + fcgLight.name;
            Transform newWp = waypointCreator.CreateWaypoint(
                insertAfter.transform.parent,
                stopPosition,
                name,
                vehicleTypes,
                insertAfter.maxSpeed,
                insertAfter.laneWidth);

            WaypointSettings newWaypoint = newWp.GetComponent<WaypointSettings>();

            if (insertAfter.prev == null || insertAfter.prev.Count == 0)
            {
                Debug.LogWarning($"insertAfter waypoint '{insertAfter.name}' has no prev. Cannot insert stop waypoint.");
                return null;
            }
            // insert: insertAfter -> newWaypoint -> insertAfter's neighbor
            WaypointSettingsBase prev = insertAfter.prev[0];

            prev.neighbors.Add(newWaypoint);
            newWaypoint.prev.Add(prev);
            newWaypoint.neighbors.Add(insertAfter);
            insertAfter.prev.Add(newWaypoint);
            prev.neighbors.Remove(insertAfter);
            insertAfter.prev.Remove(prev);

            newWp.position = MoveNewWaypointPoz(prev, insertAfter, stopPosition);
            insertAfter.name = insertAfter.name.Replace(UrbanSystemConstants.WaypointNamePrefix, UrbanSystemConstants.ConnectionWaypointName);

            return newWaypoint;
        }

        private static Vector3 MoveNewWaypointPoz(WaypointSettingsBase prev, WaypointSettings insertAfter, Vector3 currentPosition)
        {
            Vector3 a = prev.transform.position;
            Vector3 b = insertAfter.transform.position;

            // project currentPosition onto the line defined by prev and insertAfter
            Vector3 ab = b - a;
            Vector3 ap = currentPosition - a;

            float t = Vector3.Dot(ap, ab) / Vector3.Dot(ab, ab);

            return a + ab * t;
        }

        private static Vector3 GetWaypointForward(WaypointSettings waypoint)
        {
            Vector3 forward = Vector3.zero;

            foreach (var neighbor in waypoint.neighbors)
            {
                if (neighbor != null)
                {
                    forward += neighbor.transform.position - waypoint.transform.position;
                }
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                return waypoint.transform.forward;
            }

            return forward.normalized;
        }

        private static void ClearPreviousIntersections()
        {
            GameObject oldHolder = GameObject.Find(FantasticCityIntersectionsHolder);

            if (oldHolder != null)
            {
                DestroyImmediate(oldHolder);
            }
        }

        private static TrafficLights2 GetActiveTrafficLights(TFShiftHand2 shifted)
        {
            var trafficSystem = FindObjectOfType<FCG.TrafficSystem>();
            int hand = trafficSystem != null ? trafficSystem.trafficLightHand : 0;

            if (hand == 0)
            {
                return shifted.rightHandObjects;
            }

            if (hand == 1)
            {
                return shifted.leftHandObjects;
            }

            if (shifted.leftHandObjectsJapan != null)
            {
                return shifted.leftHandObjectsJapan;
            }

            return shifted.leftHandObjects;
        }

        private static void AddLightObjects(IntersectionStopWaypointsSettings group, FCG.TrafficLight light)
        {
            if (light == null)
            {
                return;
            }
            light.Pedestrians.SetActive(false);

            if (light.Red != null)
            {
                group.redLightObjects.Add(light.Red);
            }

            if (light.Yellow != null)
            {
                group.yellowLightObjects.Add(light.Yellow);
            }

            if (light.Green != null)
            {
                group.greenLightObjects.Add(light.Green);
            }
        }

        private static IEnumerable<int> GetValidSides(FCGWaypointsContainer container)
        {
            if (!container.oneway)
            {
                yield return 0;
                yield return 1;
                yield break;
            }

            if (container.doubleLine)
            {
                yield return 0;
                yield return 1;
                yield break;
            }

            // FCG one-way single-lane uses side based on right/left-hand traffic.
            yield return container.rightHand == 0 ? 1 : 0;
        }

        private static void CreateGleyRoadFromFCG(FCGWaypointsContainer fcgRoad, int roadIndex, Transform parent, List<int> vehicleTypes, int maxSpeed)
        {
            if (fcgRoad.waypoints == null || fcgRoad.waypoints.Count < 2)
                return;

            var waypointCreator = new TrafficWaypointCreator();

            GameObject roadObject = MonoBehaviourUtilities.CreateGameObject(
                "Road_" + roadIndex,
                parent,
                fcgRoad.transform.position,
                true);

            Transform lanesHolder = MonoBehaviourUtilities.CreateGameObject(
                UrbanSystemConstants.LanesHolderName,
                roadObject.transform,
                roadObject.transform.position,
                true).transform;

            _created[fcgRoad] = new Dictionary<int, List<WaypointSettings>>();

            int laneIndex = 0;

            foreach (int side in GetValidSides(fcgRoad))
            {
                Transform laneHolder = MonoBehaviourUtilities.CreateGameObject(UrbanSystemConstants.LaneNamePrefix + laneIndex, lanesHolder, roadObject.transform.position, true).transform;

                var laneWaypoints = new List<WaypointSettings>();

                for (int i = 0; i < fcgRoad.waypoints.Count; i++)
                {
                    string waypointName = roadObject.name + "-" + UrbanSystemConstants.LaneNamePrefix + laneIndex + "-" +
                        UrbanSystemConstants.WaypointNamePrefix + i;

                    Transform wp = waypointCreator.CreateWaypoint(
                        laneHolder,
                        fcgRoad.Node(side, i),
                        waypointName,
                        vehicleTypes,
                        maxSpeed,
                        fcgRoad.width * 2);

                    laneWaypoints.Add(wp.GetComponent<WaypointSettings>());
                }

                LinkSequential(laneWaypoints);
                _created[fcgRoad][side] = laneWaypoints;

                laneIndex++;
            }
        }
        private static void LinkSequential(List<WaypointSettings> waypoints)
        {
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                waypoints[i].neighbors.Add(waypoints[i + 1]);
                waypoints[i + 1].prev.Add(waypoints[i]);
            }
        }

        private static void LinkFCGRoadConnections()
        {
            foreach (var pair in _created)
            {
                FCGWaypointsContainer source = pair.Key;
                LinkSideConnections(source, 0, source.nextWay0, source.nextWaySide0);
                LinkSideConnections(source, 1, source.nextWay1, source.nextWaySide1);
            }
        }

        private static void LinkSideConnections(FCGWaypointsContainer source, int sourceSide, FCGWaypointsContainer[] nextWays, int[] nextSides)
        {
            if (!_created.ContainsKey(source))
                return;

            if (!_created[source].ContainsKey(sourceSide))
                return;

            if (nextWays == null || nextSides == null)
                return;

            List<WaypointSettings> sourceWaypoints = _created[source][sourceSide];
            if (sourceWaypoints.Count == 0)
                return;

            WaypointSettings last = sourceWaypoints[sourceWaypoints.Count - 1];

            for (int i = 0; i < nextWays.Length; i++)
            {
                FCGWaypointsContainer target = nextWays[i];
                int targetSide = nextSides[i];

                if (target == null)
                    continue;

                if (!_created.ContainsKey(target))
                    continue;

                if (!_created[target].ContainsKey(targetSide))
                    continue;

                List<WaypointSettings> targetWaypoints = _created[target][targetSide];
                if (targetWaypoints.Count == 0)
                    continue;

                WaypointSettings first = targetWaypoints[0];

                if (!last.neighbors.Contains(first))
                    last.neighbors.Add(first);

                if (!first.prev.Contains(last))
                    first.prev.Add(last);
            }
        }
    }
}
#endif