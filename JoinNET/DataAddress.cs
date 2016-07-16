using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JoinNET
{
    class DataAddress
    {
        public string address { get; set; }
        public int port { get; set; }
    }

    class DataAddressSet
    {
        public enum TunnelMode
        {
            WebServer = 1,      //Internal Web Server
            OptionalFunction,   //Optional IP address and port
        }

        public DataAddress grobal { get; set; }
        public DataAddress [] local { get; set; }
        public TunnelMode tunnelmode { get; set; }
    }
}
