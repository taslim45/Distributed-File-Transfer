using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DFT_Server
{
    public struct Clients
    {
        public int serial;
        public string clientname;
        public string clientpass;
    }
    class Listener
    {
        Socket soc;
        private static List<Clients> clientList;
        public bool Listening
        {
            get;
            private set;
        }
        public int Port
        {
            get;
            private set;
        }
        public Listener(int port)
        {
            Port = port;
            soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            clientList = new List<Clients>();
            ReadLoginFile();
        }
        public void Start()
        {
            if (Listening)
                return;

            soc.Bind(new IPEndPoint(0, Port));
            soc.Listen(0);
            soc.BeginAccept(callback, null);
            Listening = true;
        }
        public void Stop()
        {
            if (!Listening)
                return;

            soc.Close();
            soc.Dispose();
            soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }
        void callback(IAsyncResult ar)
        {
            try
            {
                Socket s = this.soc.EndAccept(ar);
                if (SocketAccepted != null)
                {
                    SocketAccepted(s);
                }
                this.soc.BeginAccept(callback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Listener message: {0}", ex.Message);
            }

        }
        private void ReadLoginFile()
        {
            bool fileExists = false;
            string thePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string fullPath = thePath + @"\loginfo.txt";

            fileExists = File.Exists(fullPath);
            if (fileExists)
            {
                Clients clients = new Clients();
                //Console.WriteLine("{0} file found.", fullPath);
                string[] userinfo = File.ReadAllLines(fullPath);
                int nclient = 0;
                foreach (string user in userinfo)
                {
                    nclient++;
                    Console.WriteLine(user);
                    string[] identities = user.Split(' ');
                    clients.serial = nclient;
                    clients.clientname = identities[0];
                    clients.clientpass = identities[1];
                    clientList.Add(clients);
                }
            }
            else
            {
                Console.WriteLine("{0} file is missing.", fullPath);
            }
        }
        public static List<Clients> GetClientList()
        {
            return clientList;
        }
        
        
        public delegate void SocketAcceptedHandler(Socket e);
        public event SocketAcceptedHandler SocketAccepted;
    }
}
