using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Threading;

class App
{
    static void __initApp()
    {
        if (!Directory.Exists(__CONFIG.PATH_TT_RAW)) Directory.CreateDirectory(__CONFIG.PATH_TT_RAW);
        if (!Directory.Exists(__CONFIG.PATH_TT_ZIP)) Directory.CreateDirectory(__CONFIG.PATH_TT_ZIP);
    }

    static void __executeCommand(string requestId, COMMANDS cmd, string input, Dictionary<string, object> data)
    {
        if (data == null) data = new Dictionary<string, object>();
        switch (cmd)
        {
            case COMMANDS.PDF_SPLIT_ALL_JPG:
                PdfService.SplitAllJpeg(requestId, cmd, input, data);
                break;
        }
    }

    #region [ MAIN ]

    static NetServer __server;
    static Thread __threadUdp = null;
    static List<IPEndPoint> __subcribes = new List<IPEndPoint>();

    public static void Reply(COMMANDS cmd, string requestId, string input, Dictionary<string, object> data)
    {
        var packet = new NetPacket(cmd, requestId, input, data);
        for (int i = 0; i < __subcribes.Count; i++)
        {
            try
            {
                __server.Send(__subcribes[i], packet);
            }
            catch {
                __subcribes.RemoveAt(i);
                i--;
            }
        }
    }

    static void __serverOnRecieve(IPEndPoint client, NetPacket packet)
    {
        try
        {
            var reader = new NetPacketReader(packet);
            var cmd = reader.Read<COMMANDS>();
            if (cmd == COMMANDS.NODE_SUBCRIBER)
                __subcribes.Add(client);
            else
            {
                var requestId = reader.ReadRequestId();
                var input = reader.Read<string>();
                var data = reader.Read<Dictionary<string, object>>();
                __executeCommand(requestId, cmd, input, data);
            }
        }
        catch
        {
        }
    }

    static void __startApp()
    {
        __initApp();

        __server = new NetServer(__CONFIG.UDP_PORT);
        __server.OnRecieve += __serverOnRecieve;
        __threadUdp = new Thread(__server.Listen);
        __threadUdp.IsBackground = true;
        __threadUdp.Start();
    }

    static void __stopApp()
    {
        if (__threadUdp != null)
            __threadUdp.Abort();
    }

    // FOR SETTING OF WINDOWS SERVICE

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

    public static void StartOnConsoleApp(string[] args) => __startApp();
    public static void StartOnWindowService(string[] args) => __startApp();
    public static void Stop() => __stopApp();

    #endregion;
}

