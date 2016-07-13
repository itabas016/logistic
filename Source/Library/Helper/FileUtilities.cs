using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public static class FileUtilities
    {
        /// <summary>
        /// <paramref name="filepath"/> has daily folder elements added to it.
        /// <para/>Example: "c:\temp"          -> "c:\temp\2011\02\09"
        /// <para/>         "c:\temp\temp.txt" -> "c:\temp\2011\02\09\temp.txt"
        /// </summary>
        /// <param name="filepath">folder or folder with filename (filename must have an extension)</param>
        /// <returns>the <paramref name="filepath"/> with the daily folder elements added to the end.</returns>
        public static string CreateDailyFolder(string filepath)
        {
            string folder;
            string file;
            string dailyFolder = DateTime.Now.ToString("yyyy\\\\MM\\\\dd");
            string newDailyFolder;

            if (string.IsNullOrEmpty(filepath))
                throw new IntegrationException("Error - The parameter 'filepath' cannot be empty.\r\n");

            try
            {
                // the only real way for us to easily know if this has a filename is to look for an extension.
                if (Path.HasExtension(filepath))
                {
                    // If there is an extension then I will assume there is a filename and we will INSERT the daily folder value BEFORE the filename.
                    folder = Path.GetDirectoryName(filepath);
                    file = Path.GetFileName(filepath);
                }
                else
                {
                    // if the is NO extension then we will simply APPEND the daily folder to the filepath.
                    folder = filepath;
                    file = string.Empty;
                }

                // Add just our daily folder portion.
                newDailyFolder = Path.Combine(folder, dailyFolder);

                // now go create the folder. note: it faster to just attempt to create it everytime. If it already exists no har is done, no exception.
                Directory.CreateDirectory(newDailyFolder);

                // if we have a filename on the input then put it back on.
                newDailyFolder = Path.Combine(newDailyFolder, file);
            }
            catch (Exception ex)
            {
                throw new IntegrationException(string.Format("ERROR: Unexpected error occuered while Creating a Daily Folder.\r\nError in {0}.{1}():\r\n{2}", typeof(FileUtilities).Name, MethodBase.GetCurrentMethod().Name, ex.ToString()));
            }


            return newDailyFolder;
        }

    }
}
