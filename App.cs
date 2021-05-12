using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web;

class Program
{
    static void __executeCommand(Tuple<string, COMMANDS, string> data)
    {
        string requestId = data.Item1, input = data.Item3;
        COMMANDS cmd = data.Item2;
        switch (cmd)
        {
            case COMMANDS.CURL_GET_HTML:
                //if (!string.IsNullOrEmpty(input))
                //{
                //    if (input.Contains("https")) __getUrlHttps(requestId, input);
                //    else __getUrlHttp(requestId, input);
                //}
                break;
            case COMMANDS.PDF_SPLIT_ALL_PNG:
                PdfService.SplitAllPng(requestId, input);
                break;
            case COMMANDS.PDF_MMF_TT:
                PdfService.PDF_MMF_TT(requestId, input);
                break;
        }
    }

    static void __initApp()
    {
        if (!Directory.Exists(__CONFIG.PATH_TT_RAW))
            Directory.CreateDirectory(__CONFIG.PATH_TT_RAW);
        if (!Directory.Exists(__CONFIG.PATH_TT_ZIP))
            Directory.CreateDirectory(__CONFIG.PATH_TT_ZIP);
    }

    #region [ MAIN ]

    static void __executeTaskTcp(Tuple<string, byte[]> data)
    {
        if (data == null || data.Item2 == null || data.Item2.Length < 39) return;
        var buf = data.Item2;
        string requestId = Encoding.ASCII.GetString(buf, 0, 36);
        var cmd = (COMMANDS)((int)buf[36]);
        string text = Encoding.UTF8.GetString(buf, 37, buf.Length - 37);
        __executeCommand(new Tuple<string, COMMANDS, string>(requestId, cmd, text));
    }

    static WebServer _http;
    static RedisBase m_subcriber;
    static bool __running = true;
    static void __startApp()
    {
        __initApp();
        string uri = string.Format("http://{0}:{1}/", __CONFIG.HTTP_HOST, __CONFIG.HTTP_PORT);
        _http = new WebServer(__executeCommand);
        _http.Start(uri);
        m_subcriber = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_SUBCRIBE,__CONFIG.REDIS_HOST,  __CONFIG.REDIS_PORT_READ));
        m_subcriber.PSUBSCRIBE(__CONFIG.CHANNEL_NAME);
        var bs = new List<byte>();
        while (__running)
        {
            if (!m_subcriber.m_stream.DataAvailable)
            {
                if (bs.Count > 0)
                {
                    var buf = m_subcriber.__getBodyPublish(bs.ToArray(), __CONFIG.CHANNEL_NAME);
                    bs.Clear();
                    if (buf != null)
                        new Thread(new ParameterizedThreadStart((o) =>
                        __executeTaskTcp((Tuple<string, byte[]>)o))).Start(buf);
                }
                Thread.Sleep(100);
                continue;
            }
            byte b = (byte)m_subcriber.m_stream.ReadByte();
            bs.Add(b);
        }
    }

    static void __stopApp()
    {
        __running = false;
        _http.Stop();
    }

    // FOR SETTING OF WINDOWS SERVICE

    static Thread __threadWS = null;
    static void Main(string[] args)
    {
        if (Environment.UserInteractive)
        {
            Console.Title = string.Format("{0} - {1}", __CONFIG.CHANNEL_NAME, __CONFIG.HTTP_PORT);
            StartOnConsoleApp(args);
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
            Stop();
        }
        else using (var service = new MyService())
                ServiceBase.Run(service);
    }

    public static void StartOnConsoleApp(string[] args) => __startApp();
    public static void StartOnWindowService(string[] args)
    {
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

