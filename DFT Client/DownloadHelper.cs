using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DFT_Client
{
    class DownloadHelper
    {
        string requestedFile;
        int reqByteStart;
        int reqByteEnd;
        string filename;
        string TheFullPath;
        string pathToPutDownloadedFile;
        string downloadingClient;

        public DownloadHelper(string file,string path,int opt)  //to be used when this client is the downloader
        {
            filename = file;
            pathToPutDownloadedFile = path;
        }
        public DownloadHelper(string name,string file,string path)  //to be used when the client is a seeder
        {
            filename = file;
            TheFullPath = path;
            downloadingClient = name;
        }

        public bool CompleteFileWrite(byte[] merged,string filename)
        {
            //Console.WriteLine("Destination {0}", pathToPutDownloadedFile);
            try
            {
                File.WriteAllBytes(pathToPutDownloadedFile + @"\" + filename, merged);
                /*
                var thestream = new MemoryStream(merged);
                string c = "";
                using (var sr = new StreamReader(thestream, Encoding.ASCII))
                {
                    c = sr.ReadToEnd();
                }
                File.WriteAllText(pathToPutDownloadedFile+ @"\" + filename, c, Encoding.UTF8);
                 * */
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception {0}", e.Message);
                return false;
            }
            return true;
        }

        public byte[] GetRequestedBytes(int start, int end,int order)
        {
            byte[] orderByte = new byte[4];
            string orderConstruct = "$" + order.ToString() + "$ ";
            orderByte = Encoding.Default.GetBytes(orderConstruct);
            reqByteStart = start;
            reqByteEnd = end;
            byte[] addname = Encoding.Default.GetBytes( downloadingClient + " ");
            byte[] fcontents = File.ReadAllBytes(TheFullPath).Skip(reqByteStart).Take(reqByteEnd).ToArray();
            byte[] finalcontents = Combine(orderByte, addname, fcontents);
            return finalcontents;
        }

        public byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }

        public byte[] Combine2D(byte[][] arrays)
        {
            Console.WriteLine("Inside Combine2D");
            int size = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                size += arrays[i].Length;
            }
            byte[] rv = new byte[size];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}
