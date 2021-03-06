﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetChat2Server
{
    public class ChatServer
    {
        private readonly IList<ClientConnection> _clientConnections;
        private readonly Mutex _clientListMutex;
        private readonly TcpListener _serverSocket;

        public ChatServer(IPAddress addr, int port)
        {
            this._clientListMutex = new Mutex();
            this._clientConnections = new List<ClientConnection>();

            Console.WriteLine(">> Starting NetChat2 server on {0}:{1}", addr, port);

            this._serverSocket = new TcpListener(addr, port);
            this._serverSocket.Start();
            Console.WriteLine(">> Server started");

            //start thread(s)
            var clientConnectThread = new Thread(this.ConnectToClientsThread);
            clientConnectThread.Start();
        }

        public void HandleIncomingMessage(ClientConnection clientConnection, TcpMessage message)
        {
            Action<ClientConnection, TcpMessage> handleMessageThread = (connection, msg) =>
            {
                var printStr = string.Empty;

                if (!this._clientListMutex.WaitOne(250))
                {
                    Console.WriteLine("Couldn't obtain client list mutex");
                    return;
                }

                IList<ClientConnection> broadcastToList =
                    this._clientConnections.Where(c => c.ClientNum != connection.ClientNum && connection.IsConnected).ToList();

                foreach (var client in broadcastToList)
                {
                    client.SendMessage(msg);
                }

                if (msg.MessageType.HasFlag(TcpMessageType.ClientLeft) ||
                    msg.MessageType.HasFlag(TcpMessageType.ClientDropped))
                {
                    connection.IsConnected = false;
                    string leftType = msg.MessageType.HasFlag(TcpMessageType.ClientLeft) ? "left" : "dropped";
                    printStr = string.Format(">> [{0}] Client No: {1} {2}", DateTime.Now, connection.Alias ?? connection.ClientNum, leftType);
                    this._clientConnections.Remove(connection);
                }

                if (msg.MessageType.HasFlag(TcpMessageType.AliasChanged))
                {
                    printStr = string.Format(">> [{0}] ({1}) is now ({2})", DateTime.Now, msg.Contents[0],
                        msg.Contents[1]);
                }

                this._clientListMutex.ReleaseMutex();

                if (!string.IsNullOrWhiteSpace(printStr))
                {
                    Console.WriteLine(printStr);
                }
            };

            var thread = new Thread(() => handleMessageThread(clientConnection, message));
            thread.Start();
        }

        private void ConnectToClientsThread()
        {
            var counter = 0;
            while (true)
            {
                //AcceptTcpClient() blocks, no need to sleep AFAIK
                TcpClient clientSocket = this._serverSocket.AcceptTcpClient();
                counter++;
                Console.WriteLine(">> [{0}] Client No: {1} joined", DateTime.Now, counter);

                var clientConnection = new ClientConnection(this);
                if (!this._clientListMutex.WaitOne(250))
                {
                    continue;
                }

                clientConnection.StartClient(clientSocket, counter.ToString(CultureInfo.InvariantCulture));
                this._clientConnections.Add(clientConnection);

                IList<string> clientNames = this._clientConnections.Where(c => c.IsConnected).Select(c => c.Alias).ToList();

                this._clientListMutex.ReleaseMutex();

                var listMsg = new TcpMessage
                              {
                                  SentTime = DateTime.Now,
                                  Contents = clientNames,
                                  MessageType = TcpMessageType.SilentData | TcpMessageType.UserList
                              };
                clientConnection.SendMessage(listMsg);
            }
        }
    }
}