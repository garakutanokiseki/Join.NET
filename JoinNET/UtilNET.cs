using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace JoinNET
{
    public class UtilNET
    {
        static public string[] GetIPAddress()
        {
            //string ipaddress = "";
            IPHostEntry ipentry = Dns.GetHostEntry(Dns.GetHostName());

            string[] ipaddress = new string[ipentry.AddressList.Count()];

            int count = 0;
            foreach (IPAddress ip in ipentry.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    ipaddress[count] = ip.ToString();
                    ++count;
                }
            }
            return ipaddress;
        }
    }
}
