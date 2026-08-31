using System.Collections.Generic;
using UnityEngine;

#if GLEY_TRAFFIC_SYSTEM
using VehicleTypes = Gley.TrafficSystem.User.VehicleTypes;
#else
using VehicleTypes = Gley.TrafficSystem.VehicleTypes;
#endif

namespace Gley.TrafficSystem
{
    /// <summary>
    /// Stores all idle vehicles.
    /// </summary>
    public class IdleVehiclesData
    {
        private readonly List<VehicleComponent> _idleVehicles;

        public List<VehicleComponent> IdleVehicles => _idleVehicles;


        public IdleVehiclesData (List<VehicleComponent> idleVehicles)
        {
            _idleVehicles = idleVehicles;
        }


        public void AddVehicle(VehicleComponent vehicle)
        {
            if (!_idleVehicles.Contains(vehicle))
            {
                if (vehicle.gameObject.activeSelf == false)
                {
                    if (!vehicle.Ignored)
                    {
                        _idleVehicles.Add(vehicle);
                    }
                }
            }
        }


        public void RemoveVehicle(VehicleComponent vehicle)
        {
            _idleVehicles.Remove(vehicle);
        }


        /// <summary>
        /// Get a random index of an idle vehicle
        /// </summary>
        /// <returns></returns>

        public VehicleComponent GetRandomVehicleOfType(VehicleTypes type)
        {
            var tempVehicles = new List<VehicleComponent>();
            for (int i = 0; i < _idleVehicles.Count; i++)
            {
                if (_idleVehicles[i].vehicleType == type)
                {
                    tempVehicles.Add(_idleVehicles[i]);
                }
            }

            if (tempVehicles.Count > 0)
            {
                return tempVehicles[Random.Range(0, tempVehicles.Count)];
            }

            return null;
        }


        public VehicleComponent GetRandomVehicle()
        {
            if (_idleVehicles.Count==0)
            {
                return null;
            }
            return _idleVehicles[Random.Range(0, _idleVehicles.Count)];
        }
    }
}