using System;
using System.Configuration;
using System.Net;

namespace NetChat2Server
{
    internal class Program
    {
        private static int ChoosePort()
        {
            var defaultPort = int.Parse(ConfigurationManager.AppSettings["ServerPort"]);

            var choice = -2;
            do
            {
                Console.Write("\nChoose a port number, blank for default ({0}): ", defaultPort);
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    choice = -1;
                    break;
                }

                try
                {
                    choice = int.Parse(input);
                }
                catch { }

                if (choice < -1 || choice > 65536)
                {
                    Console.WriteLine("Invalid choice, please try again");
                }
            } while (choice < -2 || choice > 65536);

            return choice == -1 ? defaultPort : choice;
        }

        private static IPAddress ChooseServerIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            var i = 0;
            foreach (var ip in host.AddressList)
            {
                Console.WriteLine("({0}) - {1}", i, ip);
                i++;
            }

            var choice = -2;
            while (choice < -1 || choice >= host.AddressList.Length)
            {
                Console.Write("\nChoose an IP address to start the server on, blank for localhost: ");
                var input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    choice = -1;
                    break;
                }

                try
                {
                    choice = int.Parse(input);
                }
                catch { }

                if (choice < -1 || choice >= host.AddressList.Length)
                {
                    Console.WriteLine("Invalid choice, please try again");
                }
            }
            Console.WriteLine("");

            if (choice == -1)
            {
                return new IPAddress(new byte[] { 127, 0, 0, 1 });
            }
            return host.AddressList[choice];
        }

        private static void Main(string[] args)
        {
            var version = ConfigurationManager.AppSettings["VersionNumber"];
            Console.WriteLine("NetChat2 server V{0} \n", version);

            var ip = ChooseServerIp();
            var port = ChoosePort();
            var server = new ChatServer(ip, port);
        }
    }
}