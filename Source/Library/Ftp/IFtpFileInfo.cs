using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PayMedia.Integration.IFComponents.BBCL.Logistics
{
    public interface IFtpFileInfo
    {
        string Extension { get; }
        DateTime FileDateTime { get; }
        string Filename { get; }
        DirectoryEntryTypes FileType { get; }
        string FullName { get; }
        string NameOnly { get; }
        string Path { get; }
        string Permission { get; }
        long Size { get; }
    }
}
