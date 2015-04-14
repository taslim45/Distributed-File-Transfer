using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace DFT_Server
{
    class ClientInfo
    {
        public string ID
        {
            get;
            private set;
        }
        public IPEndPoint EndPoint
        {
            get;
            private set;
        }
        Socket sck;
        string dataToClient;
        string dataFromClient;
        string messageTerminator = " &EOF& ";
        List<Clients> regClients;
        List<string> files;
        
        public ClientInfo(Socket accepted)
        {
            sck = accepted;
            ID = Guid.NewGuid().ToString();
            EndPoint = (IPEndPoint)sck.RemoteEndPoint;
            regClients = Listener.GetClientList();
            files = new List<string>();
            sck.BeginReceive(new byte[] { 0 }, 0, 0, 0, callback, null);
        }

        void callback(IAsyncResult ar)
        {
            try
            {
                sck.EndReceive(ar);

                byte[] buf = new byte[16384];  //8192

                int rec = sck.Receive(buf, buf.Length, 0);
                if (rec < buf.Length)
                {
                    Array.Resize<byte>(ref buf, rec);
                }
                if (Received != null)
                {
                    Received(this, buf);
                    if (SendMessage != null)
                    {
                        dataFromClient = Encoding.Default.GetString(buf);
                        string[] incomingMessages = Regex.Split(dataFromClient, "&EOF&");
                        
                        foreach (string msg in incomingMessages)
                        {
                            
                            string[] decoded = msg.Split(' ');
                           
                            string command = decoded[0];
                            Console.WriteLine("{0}\n{1}", command, msg);
                            string opt="";
                            if (command.Equals("Login"))
                            {
                                #region Client Login 
                                string username = decoded[1];
                                string password = decoded[2];
                                
                                bool result = CheckClientCredentials(username,password);
                                if (result)
                                {
                                    dataToClient = "Login Successful" + messageTerminator;
                                    opt = username;
                                    
                                }
                                else
                                {
                                    dataToClient = "Login Unsuccessful" + messageTerminator;
                                    opt = "unsuccessful";
                                }
                                SendMessageToClient(dataToClient,opt);
                                #endregion
                            }
                            else if (command.Equals("Logout"))
                            {
                                dataToClient = "Release" + messageTerminator;
                                opt = "Release";
                                SendMessageToClient(dataToClient,opt);
                            }
                            else if (command.Equals("Share"))
                            {
                                dataToClient = "Shared list received" + messageTerminator;
                                opt = "files";
                                
                                for (int i = 1; i < decoded.Length; i++)
                                {
                                    files.Add(decoded[i].Trim());
                                    Console.WriteLine(decoded[i].Trim());
                                }

                                SendMessageToClient(dataToClient, opt);
                            }
                            else if (command.Equals("GetList"))
                            {
                                opt = "getlist";
                                SendMessage(sck, this, opt);
                            }
                            else if (command.Equals("GetFileAll"))
                            {
                                // no particular source.
                                string reqFile = decoded[2].Trim();
                                string dloader = decoded[1].Trim();
                                Console.WriteLine("All sources ");
                                opt = "GetFileAll " + reqFile;
                                Downloader(sck, this, opt, dloader);
                            }
                            else if (command.Equals("GetFile"))
                            {
                                #region When client specifies which sources to download from
                                string reqFile = decoded[2].Trim();
                                string dloader = decoded[1].Trim();
                                if (decoded.Length - 3 > 0)
                                {
                                    // client specified the sourcs
                                    //Console.WriteLine("Specific sources ");
                                    opt = "GetFile " + reqFile + " ";
                                    for (int i = 3; i < decoded.Length; i++)
                                    {
                                        if(decoded[i].Length > 0)
                                            opt += (decoded[i] + " ");
                                        Console.WriteLine(decoded[i]);
                                    }
                                    Downloader(sck, this, opt, dloader);
                                }
                                else
                                {


                                }
                                #endregion
                            }
                            else if (command.IndexOf('$')==0)
                            {
                                #region Parts of a file from a seeder
                                string comm = msg.Trim();
                                char[] commandchar = comm.ToCharArray();
                                int firstspace = 0, secondspace = 0, flag1 = 0, flag2 = 0;
                                int spaces = 0;

                                for (int x = 0; x < commandchar.Length; x++)
                                {
                                    if (commandchar[x] == ' ')
                                    {
                                        spaces++;
                                    }
                                    if (spaces == 1 && flag1 == 0)
                                    {
                                        firstspace = x;
                                        flag1 = 1;
                                    }
                                    if (spaces == 2 && flag2 == 0)
                                    {
                                        secondspace = x;
                                        break;
                                    }
                                }
                                string first = comm.Substring(0, firstspace);
                                string second = comm.Substring(firstspace + 1, secondspace - firstspace - 1);
                                string third = comm.Substring(secondspace + 1, comm.Length - secondspace - 1);
                                string filepartToDownloadingClient = "PartContent " + first.Trim() + " " + third.Trim();
                                PartFileSender(sck, this, filepartToDownloadingClient, second.Trim());
                                #endregion
                            }
                            else if (command.Equals("Downloaded"))
                            {
                                string file = decoded[1];
                                string newsource = decoded[2];
                                UpdateList(this, file, newsource);
                            }
                        }
                    }
                }

                sck.BeginReceive(new byte[] { 0 }, 0, 0, 0, callback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Client.cs message: {0}", ex.Message);
                if (Disconnected != null)
                {
                    Disconnected(this);
                }
                Close();
            }
        }

        public void Close()
        {
            try
            {
                sck.Close();
                sck.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}", e.Message);
            }
        }

        private void SendMessageToClient(string msg,string opt)
        {
            int s = sck.Send(Encoding.Default.GetBytes(msg));
            if (s > 0)
            {
                if (opt.Equals("Release"))
                {
                    SendMessage(sck,this, "");
                    if (Disconnected != null)
                    {
                        Disconnected(this);
                    }
                    Close();
                }
                else if (opt.Equals("files"))
                {
                    SendMessage(sck, this, "");
                    if (SharedFile != null)
                    {
                        SharedFile(sck, this, files);
                    }
                }
                else
                    SendMessage(sck, this, opt);
                
            }
        }
        private bool CheckClientCredentials(string user,string pass)
        {
            
            foreach (Clients client in regClients)
            {
                if (client.clientname.Equals(user) && client.clientpass.Equals(pass))
                {
                    return true;
                }
            }
            return false;
        }

        #region Delegates
        public delegate void ClientReceivedHandler(ClientInfo sender, byte[] data);
        public delegate void ClientDisconnectedHandler(ClientInfo sender);
        public delegate void MessageToClientHandler(Socket s,ClientInfo sender,string optional);
        public delegate void SharedFileHandler(Socket s, ClientInfo sender, List<string> flist);
        public delegate void DownloadRequestHandler(Socket s, ClientInfo sender, string optional, string downloader);
        public delegate void PartFileSendHandler(Socket s, ClientInfo sender, string data, string downloader);
        public delegate void UpdateFileListHandler(ClientInfo sender, string file, string source);
        #endregion

        #region Events
        public event ClientReceivedHandler Received;
        public event ClientDisconnectedHandler Disconnected;
        public event MessageToClientHandler SendMessage;
        public event SharedFileHandler SharedFile;
        public event DownloadRequestHandler Downloader;
        public event PartFileSendHandler PartFileSender;
        public event UpdateFileListHandler UpdateList;
        #endregion
    }
}
