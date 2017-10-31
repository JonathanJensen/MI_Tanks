using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MI_Tanks_Server
{
    class Bullet
    {
        private static int netxbulletid = 1;

        public int Id;
        public int PlayerId;
        public double X;
        public double Y;

        public Bullet(int playerid, double x, double y)
        {
            Id = netxbulletid;
            netxbulletid++;
            PlayerId = playerid;
            X = x;
            Y = y;
        }
    }
}
