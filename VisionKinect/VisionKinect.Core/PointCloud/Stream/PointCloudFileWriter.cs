using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using VisionKinect.Core.IO.FileTypes;

namespace VisionKinect.Core.IO.Stream
{
    public class PointCloudFileWriter
    {
        public FileType FileType { get; set; }
        public string DirPath { get; set; }
        public string FileName { get; set; }
        public FileStream Stream { get; private set; }
        public StreamWriter Writer { get; private set; }

        public Mutex WriteLock { get; set; }

        public PointCloudFileWriter(string dirPath, FileType type, string fileName)
        {
            this.FileType = type;
            this.DirPath = dirPath;

            this.FileName = String.IsNullOrEmpty(this.FileName) ? DateTime.Now.ToFileTimeUtc().ToString() : fileName;
            this.FileName += "." + type.Extension();

            this.WriteLock = new Mutex();

            if (String.IsNullOrEmpty(this.DirPath))
                throw new ArgumentNullException("DirPath");

            if (!System.IO.Directory.Exists(this.DirPath))
                System.IO.File.Create(this.DirPath);
        }

        public void OpenFile()
        {
            this.Stream = File.Create(System.IO.Path.Combine(this.DirPath, this.FileName));
            this.Writer = new StreamWriter(this.Stream);
        }

        public void Write(string s)
        {
            if (this.IsOpened)
            {
                this.WriteLock.WaitOne();
                
                this.Writer.Flush();
                this.Writer.Write(s);
                
                this.WriteLock.ReleaseMutex();
            }
        }

        public void Close()
        {
            try
            {
                this.Stream.Close();
                this.Writer.Close();

                this.Stream.Dispose();
                this.Writer.Dispose();
            }
            catch { }
        }

        public bool IsOpened
        {
            get
            {
                return this.Writer != null && this.Stream.CanWrite;
            }
        }
    }
}
