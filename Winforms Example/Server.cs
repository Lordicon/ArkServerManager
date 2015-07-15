using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ark_Server_Manager
{
    public class Server : Ark.Models.ConnectionInfo
    {
        public string Hostname { get; set; }


        public int Port { get; set; }


        public string Password { get; set; }

    }
}
