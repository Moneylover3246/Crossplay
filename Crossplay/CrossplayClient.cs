using System;
using System.Collections.Generic;
using System.Text;

namespace Crossplay
{
    public class CrossplayClient
    {
        public byte[] LeftoverBytes { get; }

        public int TotalData { get; set; }

        public int Version { get; set; }

        public CrossplayClient()
        {
            LeftoverBytes = new byte[65535];
        }
    }
}
