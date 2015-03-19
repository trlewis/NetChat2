using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetChat2Server
{
    public class TcpServer
    {
        private readonly Mutex _clientListMutex;
        private readonly TcpListener _serverSocket;
        private IList<Clientconnection> _clientConnections;

        public TcpServer()
        {
            this._clientListMutex = new Mutex();
            this._clientConnections = new List<Clientconnection>();
            this._serverSocket = new TcpListener(GetLocalIp(), 5311);
            this._serverSocket.Start();
            Console.WriteLine(">> Server Started");

            //start thread(s)
            var clientConnectThread = new Thread(this.ConnectToClientsThread);
            clientConnectThread.Start();

            var cullConnectionsThread = new Thread(CullConnectionsThread);
            cullConnectionsThread.Start();
        }

        public void ConnectionDropped(Clientconnection connection, bool laggedOut)
        {
            connection.IsConnected = false;
            Console.WriteLine(">> [{0}] ({1}) {2}", DateTime.Now, connection.NickName ?? connection.ClientNum, laggedOut ? "dropped" : "left");
            if (!this._clientListMutex.WaitOne(500))
            {
                return;
            }

            if (this._clientConnections.Contains(connection))
            {
                this._clientConnections.Remove(connection);
            }

            this._clientListMutex.ReleaseMutex();
        }

        public void IncomingMessage(Clientconnection connection, TcpMessage msg)
        {
            var thread = new Thread(() => this.HandleIncomingMessage(connection, msg));
            thread.Start();
        }

        private static IPAddress GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && ip.ToString().StartsWith("172"))
                {
                    return ip;
                }
            }

            return new IPAddress(new byte[] { 127, 0, 0, 1 });
        }

        private void ConnectToClientsThread()
        {
            var counter = 0;
            while (true)
            {
                //AcceptTcpClient() blocks, no need to sleep AFAIK
                var clientSocket = this._serverSocket.AcceptTcpClient();
                counter++;
                Console.WriteLine("Client No:{0} started", counter);

                var clientConnection = new Clientconnection(this);
                if (!this._clientListMutex.WaitOne(250))
                {
                    continue;
                }

                clientConnection.StartClient(clientSocket, counter.ToString(CultureInfo.InvariantCulture));
                this._clientConnections.Add(clientConnection);

                var clientNames = this._clientConnections.Where(c => c.IsConnected).Select(c => c.NickName).ToList();

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

        private void CullConnectionsThread()
        {
            while (true)
            {
                Thread.Sleep(5);
                if (!this._clientListMutex.WaitOne(250))
                {
                    continue;
                }

                this._clientConnections = this._clientConnections.Where(c => c.IsConnected).ToList();

                this._clientListMutex.ReleaseMutex();
            }
        }

        private void HandleIncomingMessage(Clientconnection connection, TcpMessage msg)
        {
            if (!this._clientListMutex.WaitOne(250))
            {
                return;
            }

            var broadcastToList =
                this._clientConnections.Where(c => c.ClientNum != connection.ClientNum && connection.IsConnected).ToList();

            foreach (var client in broadcastToList)
            {
                client.SendMessage(msg);
            }

            this._clientListMutex.ReleaseMutex();

            if (msg.MessageType.HasFlag(TcpMessageType.ClientDropped) || msg.MessageType.HasFlag(TcpMessageType.ClientLeft))
            {
                this.ConnectionDropped(connection, false);
            }
        }
    }
}