using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DFT_Client
{
    class FileHelper
    {
        public string ClientName
        {
            get;
            private set;
        }
        public string ThePath
        {
            get;
            private set;
        }

        public FileHelper(string name)
        {
            ClientName = name;
            ThePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public string GetFilePathWithFileName(string filename)
        {
            string path = ThePath + @"\Shared\" + ClientName + @"\" + filename;
            return path;
        }

        public string GetFilePathOnly()
        {
            string path = ThePath + @"\Shared\" + ClientName;
            return path;
        }

        public string GetFilesFromFolder()
        {
            string path = ThePath + @"\Shared\" + ClientName;
            string[] files = Directory.GetFiles(path);
            string filensize = "";
            foreach (string s in files)
            {
                string filename = Path.GetFileName(s);
                FileInfo finfo = new FileInfo(s);
                long fsize = finfo.Length;
                filensize += (" " + filename + " " + fsize.ToString());
            }
            Console.WriteLine(filensize);
            return filensize;
        }
    }
}
