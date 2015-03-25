using System;
using System.Configuration;
using System.Net;

namespace NetChat2Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var version = ConfigurationManager.AppSettings["VersionNumber"];
            Console.WriteLine("NetChat2 server V{0} \n", version);

            var server = new ChatServer(ChooseServerIp());
        }

        private static IPAddress ChooseServerIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            var i = 0;
            foreach(var ip in host.AddressList)
            {
                Console.WriteLine("({0}) - {1}", i, ip);
                i++;
            }

            
            var choice = -2;            
            while (choice < -1 || choice >= host.AddressList.Length)
            {
                Console.Write("\nChoose an IP address to start the server on, -1 to start on localhost: ");
                var input = Console.ReadLine();
                try
                {
                    choice = int.Parse(input);
                }
                catch { }

                if(choice < -1 || choice >= host.AddressList.Length)
                {
                    Console.WriteLine("Invalid choice, please try again");
                }
            }
            Console.WriteLine("");

            if(choice == -1)
            {
                return new IPAddress(new byte[] { 127, 0, 0, 1 });
            }
            return host.AddressList[choice];
        }
    }
}