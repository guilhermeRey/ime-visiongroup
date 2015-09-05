using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisionKinect.Core.IO.FileTypes;

namespace VisionKinect.Core.PointCloud.Stream
{
    public class PointCloudZipStream
    {
        public FileStream Zip { get; private set; }
        public ZipArchive Archive { get; private set; }

        public string FileName { get; private set; }

        public string DirPath { get; private set; }

        public FileType FileType { get; private set; }

        public PointCloudZipStream(string dir, string fileName, FileType type)
        {
            this.FileName = String.IsNullOrEmpty(this.FileName) ? DateTime.Now.ToFileTimeUtc().ToString() : fileName;
            this.FileName += "." + type.Extension();

            this.DirPath = dir;
            this.FileType = type;
        }

        public PointCloudZipStream Open()
        {
            this.Zip = new FileStream(Path.Combine(this.DirPath, this.FileName + ".zip"), FileMode.Create, FileAccess.ReadWrite);
            this.Archive = new ZipArchive(this.Zip, ZipArchiveMode.Create, true);

            return this;
        }

        public void Close()
        {
            this.Archive.Dispose();
            this.Zip.Dispose();
        }
    }
}
