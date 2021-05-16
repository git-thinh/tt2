using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web;

class Program
{
    static void __initApp()
    {
        if (!Directory.Exists(__CONFIG.PATH_TT_RAW)) Directory.CreateDirectory(__CONFIG.PATH_TT_RAW);
        if (!Directory.Exists(__CONFIG.PATH_TT_ZIP)) Directory.CreateDirectory(__CONFIG.PATH_TT_ZIP);
    }

    static void __executeCommand(oRequestService rs)
    {
        switch (rs.command)
        {
            case COMMANDS.CURL_GET_HTML:
                //if (!string.IsNullOrEmpty(input))
                //{
                //    if (input.Contains("https")) __getUrlHttps(requestId, input);
                //    else __getUrlHttp(requestId, input);
                //}
                break;
            case COMMANDS.PDF_SPLIT_ALL_PNG:
                //PdfService.SplitAllPng(requestId, input);
                break;
            case COMMANDS.PDF_SPLIT_ALL_JPG:
                PdfService.SplitAllJpeg(rs);
                break;
            case COMMANDS.PDF_MMF_TT:
                //PdfService.PDF_MMF_TT(requestId, input);
                break;
        }
    }

    #region [ MAIN ]

    static Thread __threadQueue = null;
    static ConcurrentQueue<oRequestService> __requests = new ConcurrentQueue<oRequestService>() { };
    static AutoResetEvent __signal = new AutoResetEvent(false);
    static void __processQueueReceive()
    {
        __threadQueue = new Thread(new ParameterizedThreadStart((o) =>
        {
            var qs = o as ConcurrentQueue<oRequestService>;
            while (true)
            {
                if (qs.Count == 0)
                    __signal.WaitOne();
                oRequestService rs = null;
                qs.TryDequeue(out rs);
                if (rs != null)
                    __executeCommand(rs);
            }
        }));
        __threadQueue.Start(__requests);
    }

    static void __startApp()
    {
        __initApp();
        new KeySubscriber((requestService) =>
        {
            __requests.Enqueue(requestService);
            __signal.Set();
        }).Start();
    }

    static void __stopApp()
    {
        __signal.Set();
    }

    // FOR SETTING OF WINDOWS SERVICE

    static Thread __threadWS = null;
    static void Main(string[] args)
    {
        if (Environment.UserInteractive)
        {
            Console.Title = string.Format("{0} - {1}", __CONFIG.CHANNEL_NAME, __CONFIG.UDP_PORT);
            StartOnConsoleApp(args);
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
            Stop();
        }
        else using (var service = new MyService())
                ServiceBase.Run(service);
    }

    public static void StartOnConsoleApp(string[] args)
    {
        __processQueueReceive();
        __startApp();
    }
    public static void StartOnWindowService(string[] args)
    {
        __processQueueReceive();
        __threadWS = new Thread(new ThreadStart(() => __startApp()));
        __threadWS.IsBackground = true;
        __threadWS.Start();
    }

    public static void Stop()
    {
        __stopApp();
        if (__threadWS != null) __threadWS.Abort();
    }

    #endregion;
}

