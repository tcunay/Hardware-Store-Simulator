using System.Collections.Generic;
using UnityEngine;

namespace Gley.UrbanSystem
{
    [System.Serializable]
    public class Waypoint
    {
        [SerializeField] private int[] _neighbors;
        [SerializeField] private int[] _prev;
        [SerializeField] private Vector3 _position;
        [SerializeField] private string _name;
        [SerializeField] private int _listIndex;
        [SerializeField] private bool _temporaryDisabled;

        public int[] Neighbors => _neighbors;
        public int[] Prevs => _prev;
       
        public string Name => _name;
        public int ListIndex => _listIndex;
        public bool TemporaryDisabled
        {
            get
            {
                return _temporaryDisabled;
            }
            set
            {
                _temporaryDisabled = value;
            }
        }

        public Vector3 Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
            }
        }



        /// <summary>
        /// Constructor used to convert from editor waypoint to runtime waypoint 
        /// </summary>
        public Waypoint(string name, int listIndex, Vector3 position, int[] neighbors, int[] prev)
        {
            _name = name;
            _listIndex = listIndex;
            _position = position;
            _neighbors = neighbors;
            _prev = prev;
            _temporaryDisabled = false;
        }

        public void AddNeighbor(int waypointIndex)
        {
            // check if already exists (no LINQ)
            for (int i = 0; i < _neighbors.Length; i++)
            {
                if (_neighbors[i] == waypointIndex)
                    return;
            }

            int[] newArray = new int[_neighbors.Length + 1];

            for (int i = 0; i < _neighbors.Length; i++)
            {
                newArray[i] = _neighbors[i];
            }

            newArray[newArray.Length - 1] = waypointIndex;

            _neighbors = newArray;
        }

        public void AddPrev(int waypointIndex)
        {
            for (int i = 0; i < _prev.Length; i++)
            {
                if (_prev[i] == waypointIndex)
                    return;
            }

            int[] newArray = new int[_prev.Length + 1];

            for (int i = 0; i < _prev.Length; i++)
            {
                newArray[i] = _prev[i];
            }

            newArray[newArray.Length - 1] = waypointIndex;

            _prev = newArray;
        }
    }
}
