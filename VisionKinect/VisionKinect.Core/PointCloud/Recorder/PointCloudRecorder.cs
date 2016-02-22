using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VisionKinect.Core.IO.FileTypes;
using VisionKinect.Core.IO.Stream;
using VisionKinect.Core.PointCloud.IO;
using VisionKinect.Core.PointCloud.Stream;

namespace VisionKinect.Core.PointCloud.Recorder
{
    /// <summary>
    /// Point Cloud Recorder class.
    /// An instance is able to record point clouds based on temp clouds obtained throught any process.
    /// </summary>
    public class PointCloudRecorder
    {
        #region #    Properties    #
        /// <summary>
        /// The File Stream that will write the temporary file for post processing
        /// </summary>
        public PointCloudFileWriter Writer { get; set; }

        public PointCloudZipStream ZipWriter { get; set; }

        /// <summary>
        /// Recorder's current state. Could be IDLE, RECORDING OR PROCESSING_CLOUDS
        /// </summary>
        public PointCloudRecorderState RecorderState { get; set; }
        /// <summary>
        /// Temporary point cloud's stack
        /// </summary>
        public Stack<PointCloudTemp> TempClouds { get; set; }
        /// <summary>
        /// Cloud processing thread
        /// </summary>
        BackgroundWorker CloudProcessingThread { get; set; }
        /// <summary>
        /// 
        /// </summary>
        BackgroundWorker PostProcessingThread { get; set; }

        public bool RecordRGB { get; set; }
        #endregion

        #region #    Mutex    #
        public Mutex Locker { get; set; }
        public Mutex StackLocker { get; set; }
        public Mutex IdLocker { get; set; }
        #endregion

        #region #    Frames Identification    #
        public Dictionary<int, int> FrameIds { get; set; }

        public int Id;
        #endregion

        #region #    Events    #
        public event EventHandler<PointCloudRecorder> Stopped;

        protected virtual void OnStopped()
        {
            if (Stopped != null)
                Stopped(this, this);
        }

        public event EventHandler<PointCloudRecorderState> StateChanged;
        protected virtual void OnStateChanged()
        {
            if (StateChanged != null)
                StateChanged(this, this.RecorderState);
        }

        public event EventHandler<string> CloudProcessed;
        protected virtual void OnCloudProcessed(Tuple<int, int> args)
        {
            if (CloudProcessed != null)
            {
                if (this.RecorderState == PointCloudRecorderState.Recording)
                    CloudProcessed(this, String.Format("Acquired {0}", args.Item2));
                else
                {
                    decimal progress = Math.Round((decimal)100.0 - ((args.Item1 / (decimal)args.Item2) * 100), 1);
                    CloudProcessed(this, String.Format("{0}% (processed {1} of {2})", progress, args.Item2 - args.Item1, args.Item2));
                }
            }
        }
        #endregion

        public PointCloudRecorder(string dirPath, FileType recorderFileType)
        {
            this.Writer = new PointCloudFileWriter(dirPath, new PointCloudProcessor(), null);
            this.ZipWriter = new PointCloudZipStream(dirPath, null, recorderFileType);

            this.Locker = new Mutex();
            this.StackLocker = new Mutex();
            this.IdLocker = new Mutex();
            this.FrameIds = new Dictionary<int, int>();

            this.Id = 0;

            this.TempClouds = new Stack<PointCloudTemp>();
        }

        /// <summary>
        /// Appends a temporary point cloud for processing
        /// </summary>
        /// <param name="temp">temp point cloud object</param>
        public void AddCloud(PointCloudTemp temp)
        {
            this.TempClouds.Push(temp);
            this.FrameIds.Add(temp.Id, temp.PointCount);
        }

        /// <summary>
        /// Returns an unique identifier for a frame. 
        /// Applies only for a current recording session.
        /// </summary>
        /// <returns></returns>
        public int GetId()
        {
            int i;
            this.IdLocker.WaitOne();
            i = this.Id++;
            this.IdLocker.ReleaseMutex();
            return i;
        }

        /// <summary>
        /// Starts recording processing.
        /// </summary>
        public void Record()
        {
            //this.Writer.OpenFile();
            this.ZipWriter.Open();
            ChangeState(PointCloudRecorderState.Recording);

            //this.CloudProcessingThread = new Thread(new ThreadStart(ProcessStack));
            //this.CloudProcessingThread.Start();
            this.CloudProcessingThread = new BackgroundWorker();
            this.CloudProcessingThread.WorkerReportsProgress = true;
            this.CloudProcessingThread.DoWork += ProcessStack;
            this.CloudProcessingThread.ProgressChanged += CloudProcessingThread_ProgressChanged;

            this.CloudProcessingThread.RunWorkerAsync();
        }

        void CloudProcessingThread_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            OnCloudProcessed((Tuple<int, int>)e.UserState);
        }

        /// <summary>
        /// Background thread method, process the temporary point cloud stack
        /// </summary>
        protected void ProcessStack(object sender, DoWorkEventArgs e)
        {
            while (this.RecorderState == PointCloudRecorderState.Recording || this.RecorderState == PointCloudRecorderState.Stopping)
            {
                if (this.TempClouds.Count > 0 && this.TempClouds.Peek() != null)
                {
                    PointCloudTemp tempCloud = this.TempClouds.Pop();
                    PointCloudRecorderOptions options = new PointCloudRecorderOptions()
                    {
                        RecordRGB = this.RecordRGB
                    };

                    string tempfile = Path.GetTempFileName();
                    using (var writer = new StreamWriter(tempfile))
                    {
                        foreach (PointCloudTemp temp in tempCloud.ProcessCloud())
                            writer.Write(this.ZipWriter.FileType.FormatLine(temp.Id, temp.XYZ, this.RecordRGB ? temp.RGB : null));
                    }

                    ZipArchiveEntry entry = this.ZipWriter.Archive.CreateEntry("frame-" + tempCloud.Id + "." + this.ZipWriter.FileType.Extension());
                    using (StreamWriter entryWriter = new StreamWriter(entry.Open()))
                    {
                        string fileHeader = this.ZipWriter.FileType.Header(tempCloud, options);
                        entryWriter.Write(fileHeader);

                        using (var reader = new StreamReader(tempfile))
                        {
                            while (!reader.EndOfStream)
                                entryWriter.WriteLine(reader.ReadLine());
                        }
                    }

                    this.FrameIds[tempCloud.Id] = tempCloud.PointCount;
                    this.CloudProcessingThread.ReportProgress(0, new Tuple<int, int>(this.TempClouds.Count, this.FrameIds.Count));
                }
                else if (this.RecorderState == PointCloudRecorderState.Stopping)
                {
                    ChangeState(PointCloudRecorderState.ProcessingClouds);
                    //this.PostProcessingThread = new Thread(new ThreadStart(PostProcess));
                    //this.PostProcessingThread.Start();

                    this.PostProcessingThread = new BackgroundWorker();
                    this.PostProcessingThread.WorkerReportsProgress = true;
                    this.PostProcessingThread.DoWork += PostProcessingThread_DoWork;
                    this.PostProcessingThread.ProgressChanged += PostProcessingThread_ProgressChanged;

                    this.PostProcessingThread.RunWorkerAsync();
                }
            }
        }

        /// <summary>
        /// Stops the recording session and process the file stream written.
        /// This method could take a while to terminate.
        /// </summary>
        public void Stop()
        {
            ChangeState(PointCloudRecorderState.Stopping);
        }

        void PostProcessingThread_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            OnStopped();
        }

        void PostProcessingThread_DoWork(object sender, DoWorkEventArgs e)
        {
            PostProcess();
        }

        public void PostProcess()
        {
            CreateLogFile();

            //this.Writer.Close();
            ChangeState(PointCloudRecorderState.WritingFile);

            this.ZipWriter.Close();

            ChangeState(PointCloudRecorderState.Finishing);

            this.TempClouds.Clear();
            //this.CloudProcessingThread.Clo;

            ChangeState(PointCloudRecorderState.Idle);
            this.PostProcessingThread.ReportProgress(100, this.RecorderState);
        }

        private void CreateLogFile()
        {
            StringBuilder header = new StringBuilder();
            header.AppendLine("# IME - Point Cloud Recorder");
            header.AppendLine("log file for " + this.Writer.FileName);
            header.AppendLine("frame ids count " + this.FrameIds.Count);
            foreach (int id in this.FrameIds.Keys)
                header.AppendLine("frame " + id.ToString() + " count " + this.FrameIds[id].ToString());
            header.AppendLine("---");

            using (FileStream sb = new FileStream(Path.Combine(this.Writer.DirPath, this.Writer.FileName + "_log.clog"), FileMode.OpenOrCreate))
            {
                using (StreamWriter headerWriter = new StreamWriter(sb))
                {
                    headerWriter.Write(header);
                }
            }
        }

        private void ChangeState(PointCloudRecorderState newState)
        {
            this.RecorderState = newState;
            OnStateChanged();
        }
    }
}
