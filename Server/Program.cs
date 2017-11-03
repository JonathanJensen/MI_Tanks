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
        static readonly object _playerlock = new object();
        static readonly Dictionary<int, Player> clients = new Dictionary<int, Player>();
        private static int rotation = 15;
        private static double move = 1.0;
        private static double bulletmove = 5.0;
        private static Color[] colors = new Color[] { Color.Red, Color.Magenta, Color.Green, Color.Cyan, Color.Yellow, Color.White, Color.Blue };
        private static ConsoleColor[] ccolors = new ConsoleColor[] { ConsoleColor.Red, ConsoleColor.Magenta, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.White, ConsoleColor.Blue };
        private static double startx = 556560.0;
        private static double starty = 6322636.0;
        private static Random random = new Random();
        private static List<Bullet> bullets = new List<Bullet>();
        private static System.Timers.Timer bullettimer = new System.Timers.Timer(250);
        private static object _bulletlock = new object();
        private static object _playerjoinlock = new object();

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
                lock (_playerlock)
                {
                    clients.Add(count, new Player(client));
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("Someone connected!!");

                    Thread t = new Thread(handle_clients);
                    t.Start(count);
                    count++;
                }
            }
        }

        private static Player FindPlayer(int id)
        {
            lock (_playerlock)
            {
                foreach (Player p in clients.Values)
                    if (p.Id == id)
                        return p;
            }
            return null;
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
                    double rad = (b.Angle + 90) * Math.PI / 180.0;
                    b.X += Math.Cos(rad) * bulletmove;
                    b.Y += Math.Sin(rad) * bulletmove;

                    broadcast("/mb Update BulletTemp_" + b.Id + " set obj = CartesianOffset(OBJ, " + (b.Angle + 90) + ", " + bulletmove.ToString(CultureInfo.InvariantCulture) + ", \"m\")", null);
                    lock (_playerlock)
                    {
                        foreach (Player p in clients.Values)
                        {
                            if (p.Id != b.PlayerId) // You cannot kill your self
                            {
                                double dx = p.X - b.X;
                                double dy = p.Y - b.Y;
                                double distance = Math.Sqrt(dx * dx + dy * dy);
                                if (distance < 3)
                                {
                                    Player killer = FindPlayer(b.PlayerId);
                                    string killername = killer.Name;
                                    SendMessage(p, "/h "+killername); // Player was hit, remove from map
                                    Console.ForegroundColor = ccolors[killer.Id % 7];
                                    Console.WriteLine(killer.Name + " hit "+p.Name);
                                    Console.ForegroundColor = ccolors[p.Id % 7];
                                    Console.WriteLine(p.Name + " died");
                                    killer.Points++;
                                    broadcast("/mb print \"" + p.Name + " was hit by " + killer.Name + "\"", null);
                                    broadcast("/mb print \"" + p.Name + " now has " + killer.Points + " points\"", null);
                                    broadcast("/mb Delete from " + p.Name, null);
                                    broadcast("/mb close table " + p.Name, null);
                                }
                            }
                        }
                    }
                    b.Counter--;
                    if (b.Counter < 0)
                        remove.Add(b);
                }
                foreach (Bullet b in remove)
                {
                    bullets.Remove(b);
                    broadcast("/mb Delete from BulletTemp_" + b.Id, null);
                    broadcast("/mb close table BulletTemp_" + b.Id, null);
                }
            }
        }

        private static double RandomPos(double d)
        {
            double shift = random.NextDouble() * 200.0 - 100;
            return d + shift;
        }

        public static bool PlayerHasBullet(int playerid)
        {
            lock (_bulletlock)
            {
                foreach (Bullet b in bullets)
                    if (b.PlayerId == playerid)
                        return true;
            }
            return false;
        }

        private static void SendOtherPlayers(Player joiningPlayer)
        {
            foreach (Player player in clients.Values)
            {
                if (player.Id != joiningPlayer.Id)
                {
                    SendMessage(joiningPlayer, "/OpnTnkTbl"); // Open tab file with dummy tank object
                    SendMessage(joiningPlayer, "/mb Select * from tank into temp_tank"); // Make copy of dummy tank object
                    SendMessage(joiningPlayer, "/mb update temp_tank set id = " + player.Id); // Set player id
                    SendMessage(joiningPlayer, "/mb update temp_tank set playername=\"" + player.Name + "\""); // Set player name
                    SendMessage(joiningPlayer, $"/mb update temp_tank set obj=CartesianOffsetXY(obj, "+player.X.ToString(CultureInfo.InvariantCulture)+", "+player.Y.ToString(CultureInfo.InvariantCulture)+", \"m\")"); // move from 0,0 into map area
                    SendMessage(joiningPlayer, "/mb update temp_tank set obj = Rotate(obj, " + player.Angle + ")");
                    SendMessage(joiningPlayer, "/mb insert into Players select * from temp_tank"); // Copy tank into list of players
                    SendMessage(joiningPlayer, "/mb Close Table tank");
                    SendMessage(joiningPlayer, "/mb Close Table temp_tank");
                    SendMessage(joiningPlayer, "/mb select * from Players into " + player.Name + " where ID = " + player.Id + " noselect"); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                    SendMessage(joiningPlayer, "/cp " + player.Name + "," + (colors[player.Id % 7].ToArgb() & 0xFFFFFF)); // Set players tank color
                    Thread.Sleep(100);
                }
            }
        }

        public static void handle_clients(object o)
        {
            int id = (int)o;
            Player player;

            lock (_playerlock)
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
                            if (ln.StartsWith("/n")) // n = Name
                            {
                                player.Name = ln.Substring(3);
                                player.Id = id;
                                player.X = RandomPos(startx);
                                player.Y = RandomPos(starty);
                                player.Angle = (int)(random.NextDouble() * 360.0);

                                Console.ForegroundColor = ccolors[player.Id % 7];
                                Console.WriteLine(player.Name + " joined the game");
                                broadcast("/mb print \"" + player.Name + " joined the game\"", null);

                                // TODO: This should be broadcast to all connected clients, as they all need to add the new player.


                                // Then the current player broadcasts themselves to everyone (including themselves)
                                lock (_playerjoinlock)
                                {
                                    // First create player at it's own location and broadcast to all players
                                    broadcast("/OpnTnkTbl", null); // Open tab file with dummy tank object
                                    SendMessage(player, "/a "+player.Angle); // Send initial direction to client
                                    SendMessage(player, "/OpnPlrTbl"); // Open table with players (only do this once for each player)
                                    SendMessage(player, "/mb Add Map Auto Layer Players Animate"); // Add players table to map as animated layer (only do this once for each player)
                                    broadcast("/mb Select * from tank into temp_tank", null); // Make copy of dummy tank object
                                    broadcast("/mb update temp_tank set playerid = " + player.Id, null); // Set player id
                                    broadcast("/mb update temp_tank set obj=CartesianOffsetXY(obj, " + player.X.ToString(CultureInfo.InvariantCulture) + ", " + player.Y.ToString(CultureInfo.InvariantCulture) + ", \"m\")", null); // move from 0,0 into map area
                                    broadcast("/mb update temp_tank set obj = Rotate(obj, " + player.Angle + ")", null);
                                    broadcast("/mb insert into Players select * from temp_tank", null); // Copy tank into list of players
                                    broadcast("/mb Close Table tank", null);
                                    broadcast("/mb Close Table temp_tank", null);
                                    broadcast("/mb select * from Players into " + player.Name + " where PlayerId = " + player.Id + " noselect", null); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                                    broadcast("/cp " + player.Name + "," + (colors[player.Id % 7].ToArgb() & 0xFFFFFF), null);

                                    // The joining player will be brought up to date with all the other players locations
                                    SendOtherPlayers(player);
                                }

                                //SendMessage(player.Connection, "");
                                // Ready to play
                            }
                            else if (ln.StartsWith("/l")) // l = Left
                            {
                                player.Angle += rotation;
                                broadcast("/mb Update " + player.Name + " set obj = Rotate(obj, " + rotation + ")", player);
                            }
                            else if (ln.StartsWith("/r")) // r = Right
                            {
                                player.Angle -= rotation;
                                broadcast("/mb Update " + player.Name + " set obj = Rotate(obj, -" + rotation + ")", player);
                            }
                            else if (ln.StartsWith("/u")) // u = Up (forward)
                            {
                                // Calculate new player x,y
                                broadcast("/mb Update " + player.Name + " set obj = CartesianOffset(OBJ, " + (player.Angle + 90) + ", " + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")", player);
                                double rad = (player.Angle + 90) * Math.PI / 180.0;
                                player.X += Math.Cos(rad) * move;
                                player.Y += Math.Sin(rad) * move;
                            }
                            else if (ln.StartsWith("/d")) // d = Down (backwards)
                            {
                                broadcast("/mb Update " + player.Name + " set obj = CartesianOffset(OBJ, " + (player.Angle + 90) + ", -" + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")", player);
                                double rad = (player.Angle + 90) * Math.PI / 180.0;
                                player.X -= Math.Cos(rad) * move;
                                player.Y -= Math.Sin(rad) * move;
                            }
                            else if (ln.StartsWith("/f")) // f = Fire
                            {
                                if (!PlayerHasBullet(player.Id))
                                {
                                    Console.ForegroundColor = ccolors[player.Id % 7];
                                    Console.WriteLine(player.Name + " fires!");
                                    double rad = (player.Angle + 90) * Math.PI / 180.0;
                                    double bx = player.X + Math.Cos(rad) * 5.0; // Position bullet in front of tank
                                    double by = player.Y + Math.Sin(rad) * 5.0;
                                    Bullet bullet = new Bullet(player.Id, bx, by, player.Angle);
                                    lock (_bulletlock)
                                    {

                                        bullets.Add(bullet);
                                    }
                                    broadcast("/mb Insert Into Players (BulletId, OBJ) Values(" + bullet.Id + ", CreatePoint(" + bullet.X.ToString(CultureInfo.InvariantCulture) + ", " + bullet.Y.ToString(CultureInfo.InvariantCulture) + "))", null);
                                    broadcast("/mb SELECT * FROM Players WHERE BulletId = " + bullet.Id + " INTO BulletTemp_" + bullet.Id + " NoSelect Hide", null);
                                    // This we cannot get to work
                                    //broadcast("/cb BulletTemp_" + bullet.Id + "," + (colors[player.Id % 7].ToArgb() & 0xFFFFFF), null);
                                }
                            }
                            else if (ln.StartsWith("/s")) // s = Spawn (respawn after player died)
                            {
                                player.X = RandomPos(startx);
                                player.Y = RandomPos(starty);
                                broadcast("/OpnTnkTbl", null); // Open tab file with dummy tank object
                                broadcast("/mb Select * from tank into temp_tank", null); // Make copy of dummy tank object
                                broadcast("/mb update temp_tank set playerid = " + player.Id, null); // Set player id
                                broadcast("/mb update temp_tank set obj=CartesianOffsetXY(obj, " + player.X.ToString(CultureInfo.InvariantCulture) + ", " + player.Y.ToString(CultureInfo.InvariantCulture) + ", \"m\")", null); // move from 0,0 into map area
                                broadcast("/mb insert into Players select * from temp_tank", null); // Copy tank into list of players
                                broadcast("/mb Close Table tank", null);
                                broadcast("/mb Close Table temp_tank", null);
                                broadcast("/mb select * from Players into " + player.Name + " where PlayerId = " + player.Id + " noselect", null); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                                broadcast("/cp " + player.Name + "," + (colors[player.Id % 7].ToArgb() & 0xFFFFFF), null);
                            }
                            else if (ln.StartsWith("/q")) // q = Quit
                            {
                                string message = player.Name + " left the game!";
                                broadcast("/mb close table " + player.Name, player);
                                broadcast("/mb print \"" + message + "\"", player);
                                Console.ForegroundColor = ccolors[player.Id % 7];
                                Console.WriteLine(message);
                                player.Connection.Close();
                                clients.Remove(id);
                            }
                        }
                        else
                        {
                            if (ln.Length > 0)
                            {
                                broadcast(line.Trim(), null);
                                //Console.WriteLine(data);
                            }
                        }
                    }
                }
                catch
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine("Client disconnected");
                    clients.Remove(id);
                    break;
                }
            }

            lock (_playerlock) clients.Remove(id);
            try
            {
                player.Connection.Client.Shutdown(SocketShutdown.Both);
                player.Connection.Client.Close();
            }
            catch { }
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
            //            Console.WriteLine(player.Id + " " + player.Name.PadRight(20) + " " + message);
            byte[] buffer = Encoding.ASCII.GetBytes(message + Environment.NewLine);
            NetworkStream stream = player.Connection.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        public static void broadcast(string message, Player excludeplayer)
        {
            lock (_playerlock)
            {
                foreach (Player player in clients.Values)
                {
                    if (excludeplayer == null || excludeplayer.Id != player.Id)
                        SendMessage(player, message);
                }
            }
        }
    }//end Main class



}
