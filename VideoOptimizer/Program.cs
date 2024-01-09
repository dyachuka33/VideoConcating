using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoOptimizer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string inputFolder = "D:\\testVideo\\";
            string outputFolder = "D:\\output\\";
            DirectoryInfo place = new DirectoryInfo(@inputFolder);
            FileInfo[] Files = place.GetFiles();
            foreach (FileInfo i in Files)
            {
                string inputFilePath = inputFolder + i.Name;
            }
        }
    }
}
