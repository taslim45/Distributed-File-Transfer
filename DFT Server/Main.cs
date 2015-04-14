using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;

namespace DFT_Server
{
    public partial class Main : Form
    {
        Listener listener;
        public int totalclientsrequestedforfile = 0;
        public static int seedssendingrequestedparts = 0;
        public int sentback = 0;
        public string ClientName
        {
            get;
            private set;
        }
        private static Dictionary<Socket, string> OnlineSocID;
        private static Dictionary<string, string> OnlineIDToClient;
        private static Dictionary<string, List<string>> FileToClientMapper = new Dictionary<string, List<string>>();
        private static Dictionary<string, List<string>> ClientToFileMapper = new Dictionary<string, List<string>>();
        private static Dictionary<string, string> FileToSizeMapper = new Dictionary<string, string>();
        public Main()
        {
            InitializeComponent();
            listener = new Listener(8);
            listener.SocketAccepted += listener_SocketAccepted;
            Load += Main_Load;
        }

        void Main_Load(object sender, EventArgs e)
        {
            listener.Start();
            OnlineSocID = new Dictionary<Socket, string>();
            OnlineIDToClient = new Dictionary<string, string>();
        }

        void listener_SocketAccepted(System.Net.Sockets.Socket e)
        {
            if (!OnlineSocID.ContainsKey(e))
            {
                OnlineSocID.Add(e, "");
            }
            ClientInfo client = new ClientInfo(e);
            client.Received +=client_Received;
            client.Disconnected +=client_Disconnected;
            client.SendMessage +=client_SendMessage;
            client.SharedFile += client_SharedFile;
            client.Downloader += client_Downloader;
            client.PartFileSender += client_PartFileSender;
            client.UpdateList += client_UpdateList;
            Invoke((MethodInvoker)delegate
            {
                ListViewItem i = new ListViewItem();
                i.Text = client.EndPoint.ToString();  //Endpoint
                i.SubItems.Add(client.ID);      //ID    
                i.SubItems.Add("----");           //username
                i.SubItems.Add("XXX");           //last message
                i.SubItems.Add("XXX");          //last message time
                i.Tag = client;
                lstClient.Items.Add(i);
                
            });
        }

        void client_UpdateList(ClientInfo sender, string file, string source)
        {
            List<string> oldclient;
            List<string> oldfile;
            if (FileToClientMapper.ContainsKey(file))
            {
                oldclient = FileToClientMapper[file];
                oldclient.Add(source);
            }
            if (ClientToFileMapper.ContainsKey(source))
            {
                oldfile = ClientToFileMapper[source];
                oldfile.Add(file);
            }
            //Console.WriteLine("Update Complete");
        }

        void client_PartFileSender(Socket s, ClientInfo sender, string data, string downloader)
        {
            // forwarding the part file sent from a seeding client
            string final = data + " &EOF& ";
            if (OnlineIDToClient.ContainsValue(downloader))
            {
                string ID = OnlineIDToClient.FirstOrDefault(x => x.Value == downloader).Key;
                if (OnlineSocID.ContainsValue(ID))
                {
                    Socket sock = OnlineSocID.FirstOrDefault(x => x.Value == ID).Key;
                    int q = sock.Send(Encoding.Default.GetBytes(final));
                    if (q > 0)
                    {
                        //Console.WriteLine("Part data sent to {0}", downloader);
                        seedssendingrequestedparts++;
                    }
                    #region All Chunks Completed
                    if (seedssendingrequestedparts == totalclientsrequestedforfile)
                    {
                        string datatosend = "AllPartsComplete " + totalclientsrequestedforfile.ToString() + " Complete &EOF& ";
                        int u = sock.Send(Encoding.Default.GetBytes(datatosend));
                        if (u > 0)
                        {
                            //Console.WriteLine("All Parts complete for {0}\nTotal Seeds {1}\nTotal Requested {2}", downloader,seedssendingrequestedparts,totalclientsrequestedforfile);
                            seedssendingrequestedparts = 0;
                            totalclientsrequestedforfile = 0;
                            
                        }
                    }
                    #endregion
                }
            }
            
        }

        void client_Downloader(Socket s, ClientInfo sender, string optional, string downloader)
        {
            string[] optSeparator = { "" };
            if (optional.Trim().Length > 0)
            {
                char[] splitters = { ' ' };
                optSeparator = optional.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            }
            else optSeparator[0] = "";

            if (optSeparator[0].Trim().Equals("GetFileAll"))
            {
                // get from all clients
                #region Get File From All Available Clients
                int start, end, interval, nclients, size;
                int serial = 0;
                string file = optSeparator[1].Trim();
                List<string> avClients;
                if (FileToSizeMapper.ContainsKey(file))   // check the file existence
                {
                    string fsize = FileToSizeMapper[file];
                    size = int.Parse(fsize);
                    if (FileToClientMapper.ContainsKey(file))  // check file existence
                    {
                        avClients = FileToClientMapper[file];    // get list of all clients with that file
                        nclients = avClients.Count;
                        start = 0;
                        end = interval = size / nclients;
                        for (int k = 0; k < nclients; k++)
                        {
                            string clnts = avClients[k];        // get a particular client
                            if (OnlineIDToClient.ContainsValue(clnts))      // check if he is online
                            {
                                string ID = OnlineIDToClient.FirstOrDefault(x => x.Value == clnts).Key;
                                if (OnlineSocID.ContainsValue(ID))
                                {
                                    Socket sock = OnlineSocID.FirstOrDefault(x => x.Value == ID).Key;
                                    if (k == nclients - 1)
                                    {
                                        end = size;
                                    }
                                    string toSend = "SendParts " + file + " " + start.ToString() + " " + end.ToString() + " " + serial.ToString() + " " + downloader;
                                    toSend += " &EOF& ";
                                    int q = sock.Send(Encoding.Default.GetBytes(toSend));
                                    if (q > 0)
                                    {
                                        //Console.WriteLine("Request sent to {0} for {1} to {2} bytes", clnts, start, end);
                                        
                                    }
                                    serial++;
                                    start = end + 1;
                                    end += interval;
                                }
                            }
                        }
                    }
                }
                totalclientsrequestedforfile = serial;
                return;
                #endregion
            }
            else if (optSeparator[0].Trim().Equals("GetFile"))
            {
                //get from selected clients
                #region Get File From Selected Clients
                int start, end, interval, nclients, size;
                int serial=0;
                string file = optSeparator[1].Trim();
                List<string> avClients;
                List<string> prefClients = new List<string>();
                for (int x = 2; x < optSeparator.Length; x++)   // save selected clients in a separate list
                {
                    if (optSeparator[x].Trim().Length>0) prefClients.Add(optSeparator[x].Trim());   
                }
                if (FileToSizeMapper.ContainsKey(file))   // check the file existence
                {
                    string fsize = FileToSizeMapper[file];
                    size = int.Parse(fsize);
                    if (FileToClientMapper.ContainsKey(file))  // check file existence
                    {
                        avClients = FileToClientMapper[file];    // get list of all clients with that file
                        nclients = avClients.Count;
                        start = 0;
                        end = interval = size / nclients;
                        for (int k = 0; k < nclients; k++)
                        {
                            string clnts = avClients[k];        // get a particular client
                            int cindex = prefClients.IndexOf(clnts);   // search if this client is also in preferred client list
                            if (OnlineIDToClient.ContainsValue(clnts) && cindex >= 0)      // check if he is online
                            {
                                string ID = OnlineIDToClient.FirstOrDefault(x => x.Value == clnts).Key;
                                if (OnlineSocID.ContainsValue(ID))
                                {
                                    Socket sock = OnlineSocID.FirstOrDefault(x => x.Value == ID).Key;
                                    if (k == nclients - 1)
                                    {
                                        end = size;
                                    }
                                    string toSend = "SendParts " + file + " " + start.ToString() + " " + end.ToString() + " " + serial.ToString() + " " + downloader;
                                    toSend += " &EOF& ";
                                    int q = sock.Send(Encoding.Default.GetBytes(toSend));
                                    if (q > 0)
                                    {
                                        //Console.WriteLine("Request sent to {0} for {1} to {2} bytes", clnts, start, end);
                                        
                                    }
                                    serial++;
                                    start = end + 1;
                                    end += interval;
                                }
                            }
                        }
                    }
                }
                totalclientsrequestedforfile = serial;
                return;

                #endregion
            }
            
        }

        void client_SharedFile(Socket s, ClientInfo sender, List<string> flist)
        {
            #region When Client Shares File
            Invoke((MethodInvoker)delegate
            {
                for (int i = 0; i < lstClient.Items.Count; i++)
                {
                    ClientInfo client = lstClient.Items[i].Tag as ClientInfo;
                    if (client.ID == sender.ID)
                    {
                        if (OnlineIDToClient.ContainsKey(client.ID))
                        {
                            string clientname = OnlineIDToClient[client.ID];
                            List<string> temps = new List<string>();
                            if (!ClientToFileMapper.ContainsKey(clientname))
                            {
                                for (int j = 1; j < flist.Count; j+=2)
                                {
                                    string filename = flist[j - 1];
                                    string filesize = flist[j];
                                    temps.Add(filename);
                                    if (!FileToSizeMapper.ContainsKey(filename))
                                    {
                                        FileToSizeMapper.Add(filename, filesize);
                                    }

                                    if (!FileToClientMapper.ContainsKey(filename))
                                    {
                                        List<string> client_ = new List<string>();
                                        client_.Add(clientname);
                                        FileToClientMapper.Add(filename, client_);
                                    }
                                    else
                                    {
                                        List<string> updateClient = new List<string>();
                                        updateClient = FileToClientMapper[filename];
                                        updateClient.Add(clientname);
                                    }
                                }
                                ClientToFileMapper.Add(clientname, temps);
                            }
                        }
                    }
                }
            });
            #endregion
        }

        private void client_SendMessage(Socket s,ClientInfo sender,string optmsg)
        {
            string[] optSeparator = { "" };
            if (optmsg.Trim().Length > 0)
            {
                char[] splitters = { ' ' };
                optSeparator = optmsg.Split(splitters, StringSplitOptions.RemoveEmptyEntries);
            }
            else optSeparator[0] = "";
            if (optmsg.Trim().Equals("getlist"))
            {
                string toSend = "List";
                foreach (KeyValuePair<string, List<string>> pair in FileToClientMapper)
                {
                    toSend += " " + pair.Key + " " + FileToSizeMapper[pair.Key] + " ";
                    foreach (string item in pair.Value)
                    {
                        toSend += item + ",";
                    }
                    toSend += "\n";
                }
                toSend += " &EOF& ";
                int q = s.Send(Encoding.Default.GetBytes(toSend));
                if (q > 0)
                {
                    return;
                }
            }
            
            Invoke((MethodInvoker)delegate
            {
                for (int i = 0; i < lstClient.Items.Count; i++)
                {
                    ClientInfo client = lstClient.Items[i].Tag as ClientInfo;
                    if (client.ID == sender.ID)
                    {
                        if (optmsg.Trim().Length > 0)
                        {
                            if (optmsg.Trim().Equals("unsuccessful"))
                            {
                                //no client name because login unsuccessful
                            }
                            else
                            {
                                if (OnlineSocID.ContainsKey(s))
                                {
                                    OnlineSocID[s] = client.ID;
                                }
                                if (!OnlineIDToClient.ContainsKey(client.ID))
                                {
                                    OnlineIDToClient[client.ID] = optmsg.Trim();
                                    ClientName = optmsg.Trim();
                                }
                                lstClient.Items[i].SubItems[2].Text = optmsg.Trim();  //updates client name
                            }
                        }
                    }
                }
            });
        }

        void client_Disconnected(ClientInfo sender)
        {
            #region Handle Disconnection
            Invoke((MethodInvoker)delegate
            {
                for (int i = 0; i < lstClient.Items.Count; i++)
                {
                    ClientInfo client = lstClient.Items[i].Tag as ClientInfo;
                    if (client.ID == sender.ID)
                    {
                        lstClient.Items.RemoveAt(i);   //remove from UI list
                        if (OnlineIDToClient.ContainsKey(client.ID))
                        {
                            string clientname = OnlineIDToClient[client.ID];
                            if (ClientToFileMapper.ContainsKey(clientname))
                            {
                                List<string> todel = ClientToFileMapper[clientname];   //retrieve the list of files that he shared
                                bool del = ClientToFileMapper.Remove(clientname);  //remove from Client-> File list
                                foreach (string files in todel)
                                {
                                    if (FileToClientMapper.ContainsKey(files))
                                    {
                                        List<string> nclients = FileToClientMapper[files];
                                        if (nclients.Count > 1)
                                        {
                                            nclients.Remove(clientname);   //remove from client list
                                        }
                                        else
                                        { 
                                            //this client is the only holder of this file, so delete entire entry
                                            FileToClientMapper.Remove(files);  //remove from File->Client list
                                            FileToSizeMapper.Remove(files);   //remove from File->Size list
                                        }
                                    }
                                }
                            }
                        }
                        foreach (KeyValuePair<Socket,string> pair in OnlineSocID)
                        {
                            if (client.ID.Equals(pair.Value))
                            {

                                bool rem = OnlineSocID.Remove(pair.Key);  //remove from Socket->ID list
                                bool rem2 = OnlineIDToClient.Remove(pair.Value);   //remove from ID->Client list
                                if (rem && rem2) Console.WriteLine("Removed");
                                else Console.WriteLine("Failed to Remove");
                                break;
                            }
                        }
                        break;
                    }
                }
            });
            #endregion
        }

        void client_Received(ClientInfo sender, byte[] data)
        {
            Invoke((MethodInvoker)delegate
            {
                for (int i = 0; i < lstClient.Items.Count; i++)
                {
                    ClientInfo client = lstClient.Items[i].Tag as ClientInfo;
                    if (client.ID == sender.ID)
                    {
                        string incoming = Encoding.Default.GetString(data);
                        string[] messages = Regex.Split(incoming, "&EOF&");
                        foreach (string msg in messages)
                        {
                            if (msg.Trim().Length > 0)
                            {
                                lstClient.Items[i].SubItems[3].Text = msg.Trim();
                            }
                        }

                        lstClient.Items[i].SubItems[4].Text = DateTime.Now.ToString();
                        break;
                    }
                }
            });
        }
        #region Test Print
        private void btnPrint_Click(object sender, EventArgs e)
        {
            Invoke((MethodInvoker)delegate
            {
                Console.WriteLine("Users");
                foreach (KeyValuePair<Socket,string> pair in OnlineSocID)
                {
                    Console.WriteLine("{0} {1} {2}", pair.Key.ToString(),OnlineIDToClient[pair.Value], pair.Value);
                }
                Console.WriteLine("=========\nFiles and Sizes");
                foreach (KeyValuePair<string, string> pair in FileToSizeMapper)
                {
                    Console.WriteLine("{0} {1}", pair.Key,pair.Value);
                }
                Console.WriteLine("=========\nFiles and Clients");
                foreach (KeyValuePair<string, List<string>> pair in FileToClientMapper)
                {
                    Console.Write("{0}", pair.Key);
                    foreach (string item in pair.Value)
                    {
                        Console.Write(" {0}",item);
                    }
                    Console.WriteLine();
                }
                Console.WriteLine("\n=========\nClients and Files");
                foreach (KeyValuePair<string, List<string>> pair in ClientToFileMapper)
                {
                    Console.Write("{0}", pair.Key);
                    foreach (string item in pair.Value)
                    {
                        Console.Write(" {0}", item);
                    }
                    Console.WriteLine();
                }
            });
        }
        #endregion
    }
}
