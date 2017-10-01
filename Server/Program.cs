﻿using System;
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

namespace MI_Tanks_Server
{
    class Program
    {
        static readonly object _lock = new object();
        static readonly Dictionary<int, Player> clients = new Dictionary<int, Player>();
        private static int nextid = 1;
        private static int rotation = 15;
        private static double move = 1.0;
        static void Main(string[] args)
        {
            int count = 1;

            TcpListener ServerSocket = new TcpListener(IPAddress.Any, 8066);
            ServerSocket.Start();

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
                            if (ln.StartsWith("/name"))
                            {
                                player.Name = ln.Substring(6);
                                player.Id = nextid;
                                // TODO: This should be broadcast to all connected clients, as they all need to add the new player.
                                SendMessage(player, "Open Table \"C:\\source\\MI_Tanks\\MapInfo_Files\\tank.TAB\""); // Open tab file with dummy tank object
                                SendMessage(player, "Open Table \"C:\\source\\MI_Tanks\\MapInfo_Files\\Players.TAB\""); // Open table with players
                                SendMessage(player, "Add Map Auto Layer Players Animate"); // Add players table to map as animated layer
                                SendMessage(player, "Select * from tank into temp_tank"); // Make copy of dummy tank object
                                SendMessage(player, "update temp_tank set id = "+player.Id); // Set player id
                                SendMessage(player, "update temp_tank set playername=\""+player.Name+"\""); // Set player name
                                SendMessage(player, "update temp_tank set obj=CartesianOffsetXY(obj, 556560.0, 6322636.0, \"m\")"); // move from 0,0 into map area
                                SendMessage(player, "insert into Players select * from temp_tank"); // Copy tank into list of players
                                SendMessage(player, "Close Table tank");
                                SendMessage(player, "Close Table temp_tank");
                                SendMessage(player, "select * from Players into "+player.Name+" where ID = "+player.Id+" noselect"); // Create query for each player. This will both make a nice player list, and make it possible to move and rotate the geometry of each individual player
                                //SendMessage(player.Connection, "");
                                // Ready to play
                            } else if (ln.StartsWith("/left"))
                            {
                                player.Angle += rotation;
                                broadcast("Update "+player.Name+" set obj = Rotate(obj, "+rotation+")");
                            }
                            else if (ln.StartsWith("/right"))
                            {
                                player.Angle -= rotation;
                                broadcast("Update " + player.Name + " set obj = Rotate(obj, -"+rotation+")");
                            }
                            else if (ln.StartsWith("/up"))
                            {
                                // Calculate new player x,y
                                // TODO: Brug czartesianoffset funktionen i stedet for, så er vi sikre på at de flytter samme afstand som vi selv har beregnet
                                broadcast("Update "+player.Name+" set obj = CartesianOffset(OBJ, "+(player.Angle+90)+", "+move.ToString(CultureInfo.InvariantCulture)+", \"m\")");
                            }
                            else if (ln.StartsWith("/down"))
                            {
                                broadcast("Update " + player.Name + " set obj = CartesianOffset(OBJ, " + (player.Angle + 90) + ", " + move.ToString(CultureInfo.InvariantCulture) + ", \"m\")");
                            }
                            else if (ln.StartsWith("/fire"))
                            {
                               // not implemented yet
                            }
                        }
                        else
                        {
                            if (ln.Length > 0)
                            {
                                broadcast(line.Trim());
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

        public static void SendMessage(Player player, string message)
        {
            Console.WriteLine(player.Id+" "+ player.Name.PadRight(20)+" "+message);
            byte[] buffer = Encoding.ASCII.GetBytes(message + Environment.NewLine);
            NetworkStream stream = player.Connection.GetStream();
            stream.Write(buffer, 0, buffer.Length);
            stream.Flush();
        }

        public static void broadcast(string message)
        {
            lock (_lock)
            {
                foreach (Player player in clients.Values)
                {
                    SendMessage(player, message);
                }
            }
        }
    }//end Main class


   
    }
