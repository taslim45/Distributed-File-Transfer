using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;


namespace DFT_Client
{
    public partial class Main : Form
    {
        public string DownloadingClient
        {
            get;
            private set;
        }
        public int FileStart
        {
            get;
            private set;
        }
        public int FileEnd
        {
            get;
            private set;
        }
        public string RequestedFile
        {
            get;
            private set;
        }
        public int SeedSerial
        {
            get;
            private set;
        }
        Socket sck;
        string username;
        string password;
        byte[][] dataFromServer;
        static int numSources;
        public Main()
        {
            InitializeComponent();
            sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Received += Main_Received;
            btnRefresh.Visible = false;
        }

        void Main_Received(byte[] data)
        {
            Invoke((MethodInvoker)delegate
            {
                string message = Encoding.Default.GetString(data);
                string[] decoded = Regex.Split(message,"&EOF&");
                //textServerMsg.Text = message;
                foreach (string commands in decoded)
                {
                    if (commands.Trim().Length > 0)
                    {
                        string cmd = commands.Trim();
                        //Console.WriteLine(cmd);
                        if (cmd.IndexOf("List") == 0)   //if the message has List, process it for output.
                        {
                            listFile.Items.Clear();
                            string[] FileLists = Regex.Split(cmd, @"\n");   //incoming data has \n separated lines 
                            int escape = 0;
                            for (int i=0; i< FileLists.Length; i++)
                            {
                                if (FileLists[i].Length > 0)
                                {
                                    Console.WriteLine(i + " " + FileLists[i]);
                                    string nlseparated = FileLists[i].Trim();
                                    if (nlseparated.IndexOf(username) > -1)
                                    {
                                        escape++;
                                        continue;   //the current user has his name in the list, skip this item.
                                    }
                                    string[] contents = nlseparated.Split(' ');   //each list is <space> separated

                                    if (i == 0)   //because the first line has the word 'List', so treat it specially
                                    {
                                        ListViewItem li = new ListViewItem();
                                        li.Text = contents[1].Trim();   //file name
                                        li.SubItems.Add(contents[2].Trim());   //file size
                                        li.SubItems.Add(contents[3].Trim());    //clients    
                                        li.Tag = i.ToString();
                                        listFile.Items.Add(li);
                                    }
                                    else
                                    {
                                        ListViewItem li = new ListViewItem();
                                        li.Text = contents[0].Trim();   //file name
                                        li.SubItems.Add(contents[1].Trim());   //file size
                                        li.SubItems.Add(contents[2].Trim());    //clients    
                                        li.Tag = i.ToString();
                                        listFile.Items.Add(li);
                                    }
                                    
                                }
                            }
                            if (escape == FileLists.Length)    
                            {
                                ListViewItem li = new ListViewItem();
                                li.Text = "=====";   //file name
                                li.SubItems.Add("=====");   //file size
                                li.SubItems.Add("Sorry no file was shared other than yours!");    //clients    
                                li.Tag = "sorry";
                                listFile.Items.Add(li);
                            }

                        }
                        else if (cmd.IndexOf("PartContent") == 0)
                        { 
                            // parts of requested file
                            char[] cmdArray = cmd.ToCharArray();
                            int firstspace = 0, secondspace = 0, flag1 = 0, flag2 = 0;
                            int spaces = 0;
                            for (int x = 0; x < cmdArray.Length; x++)
                            {
                                if (cmdArray[x] == ' ')
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
                            string first = cmd.Substring(0, firstspace);
                            string second = cmd.Substring(firstspace + 1, secondspace - firstspace - 1);
                            string third = cmd.Substring(secondspace + 1, cmd.Length - secondspace - 1);
                            string[] numbers = Regex.Split(second, @"\D+");
                            
                            int index = 0;
                            foreach (string item in numbers)
                            {
                                if (item.Length > 0)
                                {
                                    index = int.Parse(item);
                                    
                                }
                            }
                            //Console.WriteLine("PartContent storing at {0}", index);
                            byte[] filedata = Encoding.Default.GetBytes(third.Trim());
                            int len = filedata.Length;
                            dataFromServer[index] = new byte[len];
                            for (int z = 0; z < len; z++)
                            {
                                dataFromServer[index][z] = filedata[z];
                            }
                            //Console.WriteLine("PartContent run successful");
                        }
                        else if (cmd.IndexOf("SendParts") == 0)
                        { 
                            // send back parts of the file
                            
                            textServerMsg.Text = commands.Trim();
                            string[] brokens = cmd.Split(' ');
                            RequestedFile = brokens[1];
                            FileStart = int.Parse(brokens[2]);
                            FileEnd = int.Parse(brokens[3]);
                            SeedSerial = int.Parse(brokens[4]);
                            DownloadingClient = brokens[5];
                            //Console.WriteLine("SendParts for {0} serial {1} file {2}", DownloadingClient, SeedSerial, RequestedFile);
                            FileHelper fhelper = new FileHelper(username);

                            DownloadHelper dhelper = new DownloadHelper(DownloadingClient, RequestedFile, fhelper.GetFilePathWithFileName(RequestedFile));
                            byte[] requestedData = dhelper.GetRequestedBytes(FileStart, FileEnd - FileStart, SeedSerial);
                            string partData = Encoding.Default.GetString(requestedData);
                            SendMessageToServer(partData);
                        }
                        else if (cmd.IndexOf("AllPartsComplete") == 0)
                        {
                            char[] arr = cmd.ToCharArray();
                            int firstspace = 0, secondspace = 0, flag1 = 0, flag2 = 0;
                            int spaces = 0;
                            for (int i = 0; i < arr.Length; i++)
                            {
                                if (arr[i] == ' ')
                                {
                                    spaces++;
                                }
                                if (spaces == 1 && flag1 == 0)
                                {
                                    firstspace = i;
                                    flag1 = 1;
                                }
                                if (spaces == 2 && flag2 == 0)
                                {
                                    secondspace = i;
                                    break;
                                }
                            }

                            string first = cmd.Substring(0, firstspace);
                            string second = cmd.Substring(firstspace + 1, secondspace - firstspace - 1);
                            string third = "";
                            third = cmd.Substring(secondspace + 1, cmd.Length - secondspace - 1);
                            //Console.WriteLine("Inside AllPartsComplete: 0:{0}< 1:{1}< 2:{2}<", first, second, third);
                            string[] numbers = Regex.Split(second, @"\D+");
                            //Console.WriteLine("{0}", numbers.Length);
                            int totalorders = 0;
                            foreach (string item in numbers)
                            {
                                if (item.Length > 0)
                                {
                                    totalorders = int.Parse(item);
                                    //Console.WriteLine("TotalOrders 0:{0}", totalorders);
                                }
                            }

                            byte[][] finalCopy = new byte[totalorders][];
                            CopyMyArray(ref dataFromServer, ref finalCopy, 0, 0, totalorders);
                            FileHelper fh = new FileHelper(username);
                            DownloadHelper dh = new DownloadHelper(RequestedFile, fh.GetFilePathOnly(), 0);
                            byte[] finalMerged = dh.Combine2D(finalCopy);
                            bool IsDownloadComplete = dh.CompleteFileWrite(finalMerged, RequestedFile);
                            if (IsDownloadComplete)
                            {
                                string dcompletemsg = "Downloaded " + RequestedFile + " " + username;
                                //Console.WriteLine(dcompletemsg);
                                SendMessageToServer(dcompletemsg);
                            }
                        }

                        if (cmd.Equals("Release"))
                        {
                            try
                            {
                                sck.Close();
                                sck.Dispose();
                                Close();
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }
                            
                        }
                        else
                        {
                            //MessageBox.Show("Connected");
                            textServerMsg.Text = commands.Trim();
                        }
                    }
                }
                
            });
        }

        void CopyMyArray(ref byte[][] source, ref byte[][] destination, int startRow, int startCol,int total)
        {
            for (int i = 0; i < total; ++i)
            {
                destination[i] = new byte[source[i].Length];
                for (int j = 0; j < source[i].Length; j++)
                {
                    destination[i][j] = source[i][j];
                }
            }
            //Console.WriteLine("Copied to final array");
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            username = textUsername.Text.Trim();
            password = textPassword.Text.Trim();
            if (username.Length > 0 && password.Length > 0)
            {
                sck.Connect("127.0.0.1", 8);
                string msg = "Login " + username + " " + password;
                sck.BeginReceive(new byte[] { 0 }, 0, 0, 0, callback, null);
                SendMessageToServer(msg);
            }
            else
            {
                MessageBox.Show("Username or password field empty");
            }
        }
        void callback(IAsyncResult ar)
        {
            try
            {
                sck.EndReceive(ar);

                byte[] buf = new byte[16384];    //8192

                int rec = sck.Receive(buf, buf.Length, 0);
                if (rec < buf.Length)
                {
                    Array.Resize<byte>(ref buf, rec);
                }
                if (Received != null)
                {
                    Received(buf);
                }

                sck.BeginReceive(new byte[] { 0 }, 0, 0, 0, callback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error {0}", ex.Message);

            }
        }

        void SendMessageToServer(string msg)
        {
            string toSend = msg + " &EOF& ";
            int s = sck.Send(Encoding.Default.GetBytes(toSend));

            if (s > 0)
            {
                //MessageBox.Show("Message Sent");
                Console.WriteLine("Sent {0}", msg);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SendMessageToServer("Logout");
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            FileHelper fh = new FileHelper(username);
            string files = "Share" + fh.GetFilesFromFolder();
            SendMessageToServer(files);
        }

        private void btnGetAll_Click(object sender, EventArgs e)
        {
            SendMessageToServer("GetList");
            btnRefresh.Visible = true;
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            SendMessageToServer("GetList");
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            #region Working Checked Debugged
            string downloadCommand = txtFileName.Text;      // get the command from the text box
            string[] breakDown = downloadCommand.Split(' ');
            RequestedFile = breakDown[0].Trim();
            if (breakDown.Length > 1)
            {
                // client specified the sources
                numSources = breakDown.Length - 1;
                dataFromServer = new byte[numSources][];
                //Console.WriteLine("GetFile " + username + " " + downloadCommand.Trim());
                SendMessageToServer("GetFile " + username + " " + downloadCommand.Trim());
            }
            else
            { 
                // did not specify the sources, so download from all available sources.
                dataFromServer = new byte[100][];
                //Console.WriteLine("GetFileAll " + username + " " + breakDown[0].Trim());
                SendMessageToServer("GetFileAll " + username + " " + breakDown[0].Trim());
            }
            #endregion
        }

        public delegate void MessageReceivedHandler(byte[] data);
        public event MessageReceivedHandler Received;
 
    }
}
