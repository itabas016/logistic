using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    /// <summary>
	/// Stores a list of files and directories from an FTP result
	/// </summary>
	/// <remarks></remarks>
	public class FtpDirectory : List<FtpFileInfo>
    {
        public FtpDirectory()
        {
            //creates a blank directory listing
        }

        /// <summary>
        /// Constructor: create list from a (detailed) directory string
        /// </summary>
        /// <param name="dir">directory listing string</param>
        /// <param name="path"></param>
        /// <remarks></remarks>
        public FtpDirectory(string dir, string path)
        {
            foreach (string line in dir.Replace("\n", "").Split(System.Convert.ToChar('\r')))
            {
                //parse
                if (line != "" && !line.EndsWith("."))
                {
                    this.Add(new FtpFileInfo(line, path, true));
                }
            }
        }

        /// <summary>
        /// Constructor: create list from an arraylist object
        /// </summary>
        /// <param name="filelist">directory listing arraylist object</param>
        /// <param name="path"></param>
        public FtpDirectory(ArrayList filelist, string path)
        {
            foreach (string filename in filelist)
            {
                if (filename != "." && filename != ".." && filename.IndexOf('.') > 0)
                    this.Add(new FtpFileInfo(filename, path, false));
            }
        }

        /// <summary>
        /// Filter out only files from directory listing
        /// </summary>
        /// <param name="ext">optional file extension filter</param>
        /// <returns>FTPdirectory listing</returns>
        public FtpDirectory GetFiles(string ext)
        {
            return this.GetFileOrDir(DirectoryEntryTypes.File, ext);
        }

        /// <summary>
        /// Returns a list of only subdirectories
        /// </summary>
        /// <returns>FTPDirectory list</returns>
        /// <remarks></remarks>
        public FtpDirectory GetDirectories()
        {
            return this.GetFileOrDir(DirectoryEntryTypes.Directory, "");
        }

        //internal: share use function for GetDirectories/Files
        private FtpDirectory GetFileOrDir(DirectoryEntryTypes type, string ext)
        {
            FtpDirectory result = new FtpDirectory();
            foreach (FtpFileInfo fi in this)
            {
                if (fi.FileType == type)
                {
                    if (ext == "")
                    {
                        result.Add(fi);
                    }
                    else if (ext == fi.Extension)
                    {
                        result.Add(fi);
                    }
                }
            }
            return result;

        }

        public bool FileExists(string filename)
        {
            foreach (FtpFileInfo ftpfile in this)
            {
                if (ftpfile.Filename == filename)
                {
                    return true;
                }
            }
            return false;
        }

        private const char slash = '/';

        public static string GetParentDirectory(string dir)
        {
            string tmp = dir.TrimEnd(slash);
            int i = tmp.LastIndexOf(slash);
            if (i > 0)
            {
                return tmp.Substring(0, i - 1);
            }
            else
            {
                throw (new ApplicationException("No parent for root"));
            }
        }
    }
}
