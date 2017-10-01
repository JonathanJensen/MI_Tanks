using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MI_Tanks_Server
{
    class Player
    {
        public TcpClient Connection;
        public string Name;
        public int Id;
        public int Angle;
        public double X;
        public double Y;
        public Player(TcpClient connection)
        {
            Connection = connection;
        }
    }
}
