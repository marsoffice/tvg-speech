using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarsOffice.Tvg.Speech.Entities
{
    public class FfProbeFormat
    {
        public double Duration { get; set; }
        public long Size { get; set; }
    }

    public class FfProbeResponse
    {
        public FfProbeFormat Format { get; set; }
    }
}
