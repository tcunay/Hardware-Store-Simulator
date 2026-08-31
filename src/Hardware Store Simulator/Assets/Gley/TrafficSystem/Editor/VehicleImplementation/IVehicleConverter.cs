using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public interface IVehicleConverter
    {
        void Convert(string sourceFolder, string destinationFolder);
    }
}
