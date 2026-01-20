using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Graphs
{
    internal class graphs_model
    {
        public int Id { get; set; }

        public string GraphName { get; set; } = string.Empty;

        public string GraphEquation { get; set; } = string.Empty;

        public int GraphType { get; set; } = 0;

        public double U1 { get; set; } = 0.0;
        public double U2 { get; set; } = 0.0;

        public double V1 { get; set; } = 0.0;
        public double V2 { get; set; } = 0.0;

        public double MeshStep { get; set; } = 0.1;
    }
}
