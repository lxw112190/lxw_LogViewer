using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;

namespace lxw_LogViewer
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        long Offset = 0;//初始化偏移
        String fileName = "";
        private static ConcurrentQueue<String> logQueue = new ConcurrentQueue<String>();

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = false;//该值确定是否可以选择多个文件
            dialog.Title = "请选择文件";
            dialog.Filter = "文本文件(*.log;*.txt)|*.log;*.txt;";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && dialog.FileName != "")
            {
                rtxtShow.Text = "";
                Offset = 0;
                fileName = dialog.FileName;
                txtFile.Text = fileName;

                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = fileName.Substring(0, fileName.LastIndexOf("\\") + 1);
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
                watcher.Filter = fileName.Substring(fileName.LastIndexOf("\\") + 1);
                watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                watcher.Created += new FileSystemEventHandler(watcher_Created);
                watcher.Deleted += new FileSystemEventHandler(watcher_Deleted);
                watcher.Renamed += new RenamedEventHandler(watcher_Renamed);
                watcher.EnableRaisingEvents = true;

                Thread t = new Thread(new ThreadStart(ShowLog));
                t.Start();

                LogToQueue();
            }

        }

        void ShowLog()
        {
            while (true)
            {
                Thread.Sleep(500);
                while (logQueue.Count > 0)
                {
                    String logInfo = "";
                    logQueue.TryDequeue(out logInfo);
                    rtxtShow.Invoke(new Action(() =>
                       {
                           if (logInfo != null && logInfo.Contains("ERROR") || logInfo.Contains("error"))
                           {
                               //高亮显示
                               rtxtShow.SelectionStart = rtxtShow.Text.Length;
                               rtxtShow.SelectionLength = logInfo.Length;
                               rtxtShow.SelectionColor = Color.FromName("Red");
                           }
                           rtxtShow.AppendText(logInfo + "\r\n");
                       }));
                }
            }
        }

        void watcher_Renamed(object sender, RenamedEventArgs e)
        {
            rtxtShow.Invoke(new Action(() =>
            {
                rtxtShow.AppendText("文件被重命名");
            }));

        }

        void watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            rtxtShow.Invoke(new Action(() =>
            {
                rtxtShow.AppendText("文件被删除");
            }));

        }

        void watcher_Created(object sender, FileSystemEventArgs e)
        {
            rtxtShow.Invoke(new Action(() =>
            {
                rtxtShow.AppendText("文件被创建");
            }));
        }

        void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            LogToQueue();
        }

        private void LogToQueue()
        {
            Mutex mutex = new Mutex(false, "mutex");
            mutex.WaitOne();
            if (File.Exists(fileName))
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.CanSeek)
                    {
                        if (Offset > fs.Length)//防止期间文件删除后创建导致偏移变化
                        {
                            Offset = fs.Length - 1;
                        }
                        fs.Seek(Offset, SeekOrigin.Begin);

                        byte[] b = new byte[fs.Length - Offset + 1];
                        int cnt, m = 0;
                        cnt = fs.ReadByte();
                        while (cnt != -1)
                        {
                            b[m++] = Convert.ToByte(cnt);
                            cnt = fs.ReadByte();
                        }

                        List<string> ltInfo = Encoding.UTF8.GetString(b).Split(new string[] { "\r\n" }
                            , StringSplitOptions.None).ToList();

                        foreach (String item in ltInfo)
                        {
                            logQueue.Enqueue(item);
                        }
                        Offset = fs.Length; //更新偏移位置
                    }
                    else
                    {
                        rtxtShow.Invoke(new Action(() =>
                        {
                            rtxtShow.AppendText("当前流不支持查找");
                        }));
                    }
                }
            }
            mutex.ReleaseMutex();
        }

    }
}
