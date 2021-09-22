using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulator
{
    public class TrailerTelemetry
    {
        public string id { get; set; }
        public double temperature { get; set; }
        public bool temperatureAlert { get; set; } = false;
    }
}
