using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using MI_Tanks_Server;
using System.Drawing;
using System.Threading.Tasks;
using System.Timers;

namespace MI_Tanks_Server
{
    class Program
    {
        static readonly object _lock = new object();
        static readonly Dictionary<int, Player> clients = new Dictionary<int, Player>();
        private static int nextid = 1;
        private static int rotation = 15;
        private static double move = 1.0;
        private static double bulletmove = 5.0;
        private static Color[] colors = new Color[] { Color.Red, Color.Magenta, Color.Green, Color.Cyan, Color.Yellow, Color.White, Color.Blue };
        private static double startx = 556560.0;
        private static double starty = 6322636.0;
        private static Random random = new Random();
        private static List<Bullet> bullets = new List<Bullet>();
        private static System.Timers.Timer bullettimer = new System.Timers.Timer(200);
        private static object _bulletlock = new object();

        static void Main(string[] args)
        {
            int count = 1;

            TcpListener ServerSocket = new TcpListener(IPAddress.Any, 8066);
            ServerSocket.Start();

            bullettimer.Elapsed += OnTimedEvent;
            bullettimer.AutoReset = true;
            bullettimer.Enabled = true;

            while (true)
            {
                TcpClient client = ServerSocket.AcceptTcpClient();
                lock (_lock)
                    clients.Add(count, new Player(client));
                Console.WriteLine("Someone connected!!");

                Thread t = new Thread(handle_clients);
                t.Start(count);
                count++;
            }
        }

        private static void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            lock (_bulletlock)
            {
                List<Bullet> remove = new List<Bullet>();
                int bulletcount = bullets.Count;
                for (int i = 0; i < bulletcount; i++)
                {
                    Bullet b = bullets[i];
                    broadcast("/mb SELECT * FROM bullet WHERE BulletId = " + b.Id + " INTO BulletTemp NoSelect Hide", null);
                    broadcast("/mb Update BulletTemp set obj = CartesianOffset(OBJ, " + (b.Angle + 90) + ", " + bulletmove.ToString(CultureInfo.InvariantCulture) + ", \"m\")", null);
                    broadcast("close BulletTemp", null);
                    b.Counter--;
                    if (b.Counter < 0)
                        remove.Add(b);
                }
                foreach (Bullet b in remove)
                {
                    bullets.Remove(b);
                    broadcast("/mb SELECT * FROM bullet WHERE BulletId = " + b.Id + " INTO BulletTemp NoSelect Hide", null);
                    broadcast("/mb Delete from BulletTemp", null);
                }
            }
        }

            private static double RandomPos(double d)
        {
            double shift = random.NextDouble() * 200.0 - 100;
            return d + shift;
        }

        public static void handle_clients(object o)
        {
            int id = (int)o;
            Player player;

            lock (_lock)
                player = clients[id];

            while (true)
            {
                try
                {
                    NetworkStream stream = player.Connection.GetStream();
                    byte[] buffer = new byte[1024];
                    int byte_count = stream.Read(buffer, 0, buffer.Length);

                    if (byte_count == 0)
                    {
                        break;
                    }

                    string data = Encoding.ASCII.GetString(buffer, 0, byte_count);
                    string[] lines = data.Split('\n');
                    foreach (string line in lines)
                    {
                        string ln = line.Trim();
                        if (ln.StartsWith("/")) // Command only for server
                        {
                            if (ln.StartsWith("/n"))
                            {
                                player.Name = ln.Substring(3);
                                player.Id = id;
                                player.X = RandomPos(startx);
                                player.Y = RandomPos(starty);

                                Console.WriteLine("Player joined: " + player.Name);
                                
                                // TODO: This should be broadcast to all connected clients, as they all need to add the new player.

                                // The joining player will be brought up to date with all the other players locations
                                JoinGame(player);

                                // Then the current player broadcasts themselves to everyone (including themselves)
                                broadcast("/mb set CoordSys Earth Projection 8, 115, \"m\", 9, 0, 0.9996, 500000, 0", null);
                                broadcast("/OpnTnkTbl", null); // Open tab file with dummy tank object
                                broadcast("/OpnPlrTbl", null); // Open table with players
                                broadcast("/mb Add Map Auto Layer Players Animate", null); // Add players table to map as animated layer
                                broadcast("/mb Select * from tank into temp_tank", null); // Make copy of dummy tank object
                                broadcast("/mb update temp_tank set id = "+player.Id, null); // Set player id
                                broadcast("/mb update temp_tank set playername=\""+player.Name+"\"", null); // Set player name
                                broadcast("/mb update temp_tank set obj=CartesianOffsetXY(obj, "+player.X.ToString(CultureInfo.InvariantCulture)+", "+player.Y.ToString(CultureInfo.InvariantCulture)+", \"m\")", null); // move from 0,0 into map area
                                broadcast("/mb insert into Players select * from temp_tank", null); // Copy tank into list of players
                                broadcast("/mb Close Table tank", null);
                                broadcast("/mb Close Table temp_tank", null);
                                broadcast("/mb select * from Players into "+player.Name+" where ID = "+player.Id+" noselect", null); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                                broadcast("/c " + player.Name + ","+(colors[player.Id % 7].ToArgb() & 0xFFFFFF), null);

                                //SendMessage(player.Connection, "");
                                // Ready to play
                            }
                            else if (ln.StartsWith("/l"))
                            {
                                player.Angle += rotation;
                                broadcast("/mb Update " + player.Name+" set obj = Rotate(obj, "+rotation+")", player);
                            }
                            else if (ln.StartsWith("/r"))
                            {
                                player.Angle -= rotation;
                                broadcast("/mb Update " + player.Name + " set obj = Rotate(obj, -"+rotation+")", player);
                            }
                            else if (ln.StartsWith("/u"))
                            {
                                // Calculate new player x,y
                                broadcast("/mb Update " + player.Name+" set obj = CartesianOffset(OBJ, "+(player.Angle+90)+", "+move.ToString(CultureInfo.InvariantCulture)+", \"m\")", player);
                                double rad = (player.Angle+90) * Math.PI / 180.0;
                                player.X += Math.Cos(rad) * move;
                                player.Y += Math.Sin(rad) * move;
                            }
                            else if (ln.StartsWith("/d"))
                            {
                                broadcast("/mb Update " + player.Name + " set obj = CartesianOffset(OBJ, " + (player.Angle + 90) + ", -" + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")", player);
                                double rad = (player.Angle+90) * Math.PI / 180.0;
                                player.X -= Math.Cos(rad) * move;
                                player.Y -= Math.Sin(rad) * move;
                            }
                            else if (ln.StartsWith("/f"))
                            {
                                double rad = (player.Angle+90) * Math.PI / 180.0;
                                double bx= player.X + Math.Cos(rad) * 5.0; // Position bullet in front of tank
                                double by =player.Y + Math.Sin(rad) * 5.0;
                                Bullet bullet = new Bullet(player.Id, bx, by, player.Angle);
                                lock (_bulletlock)
                                {

                                    bullets.Add(bullet);
                                }
                                broadcast("/mb Insert Into Bullet (BulletId, PlayerId, OBJ) Values("+bullet.Id+", "+player.Id+", CreatePoint("+bullet.X.ToString(CultureInfo.InvariantCulture)+", "+bullet.Y.ToString(CultureInfo.InvariantCulture)+"))", null);
                               // not implemented yet
                               // add bullet to map and start moving
                            }
                            else if (ln.StartsWith("/q"))
                            {
                                string message = player.Name + " left the game!";
                                broadcast("/mb close table "+player.Name, player);
                                broadcast("/mb print \""+message+"\"", player);
                                Console.WriteLine(message);
                            }
                        }
                        else
                        {
                            if (ln.Length > 0)
                            {
                                broadcast(line.Trim(), null);
                                Console.WriteLine(data);
                            }
                        }
                    }
                } catch
                {
                    Console.WriteLine("Client disconnected");
                    clients.Remove(id);
                    break;
                }
            }

            lock (_lock) clients.Remove(id);
            player.Connection.Client.Shutdown(SocketShutdown.Both);
            player.Connection.Client.Close();
        }

        private static void JoinGame(Player joiningPlayer)
        {
            foreach (Player player in clients.Values)
            {
                if (player != joiningPlayer)
                {
                    SendMessage(joiningPlayer, "/OpnTnkTbl"); // Open tab file with dummy tank object
                    SendMessage(joiningPlayer, "/OpnPlrTbl"); // Open table with players
                    SendMessage(joiningPlayer, "/mb Add Map Auto Layer Players Animate"); // Add players table to map as animated layer
                    SendMessage(joiningPlayer, "/mb Select * from tank into temp_tank"); // Make copy of dummy tank object
                    SendMessage(joiningPlayer, "/mb update temp_tank set id = " + player.Id); // Set player id
                    SendMessage(joiningPlayer, "/mb update temp_tank set playername=\"" + player.Name + "\""); // Set player name
                    SendMessage(joiningPlayer, $"/mb update temp_tank set obj=CartesianOffsetXY(obj, {556560.0 + player.X}, {6322636.0 + player.Y}, \"m\")"); // move from 0,0 into map area
                    SendMessage(joiningPlayer, "/mb update temp_tank set obj = Rotate(obj, " + player.Angle + ")");
                    SendMessage(joiningPlayer, "/mb insert into Players select * from temp_tank"); // Copy tank into list of players
                    SendMessage(joiningPlayer, "/mb Close Table tank");
                    SendMessage(joiningPlayer, "/mb Close Table temp_tank");
                    SendMessage(joiningPlayer, "/mb select * from Players into " + player.Name + " where ID = " + player.Id + " noselect"); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                    Thread.Sleep(100);
                }
            }
        }

        public static void SendMessage(Player player, string message)
        {
            Console.WriteLine(player.Id+" "+ player.Name.PadRight(20)+" "+message);
            byte[] buffer = Encoding.ASCII.GetBytes(message + Environment.NewLine);
            NetworkStream stream = player.Connection.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        public static void broadcast(string message, Player excludeplayer)
        {
            lock (_lock)
            {
                foreach (Player player in clients.Values)
                {
                    if (excludeplayer==null || excludeplayer.Id!=player.Id)
                      SendMessage(player, message);
                }
            }
        }
    }//end Main class


   
}
