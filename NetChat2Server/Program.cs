using System;
using System.Configuration;

namespace NetChat2Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var version = ConfigurationManager.AppSettings["VersionNumber"];
            Console.WriteLine("NetChat2 server V{0}", version);

            var server = new ChatServer();
        }
    }
}