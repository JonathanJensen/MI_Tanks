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
        public int Angle;
        public int Counter;

        public Bullet(int playerid, double x, double y, int angle)
        {
            Id = netxbulletid;
            netxbulletid++;
            PlayerId = playerid;
            X = x;
            Y = y;
            Angle = angle;
            Counter = 20; // After 20 moves the bullet dies.
        }
    }
}
