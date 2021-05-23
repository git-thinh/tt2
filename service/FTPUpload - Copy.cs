// $Id: Upload.cs,v 1.1 2005/02/17 22:47:24 jeffreyphillips Exp $
// Upload.cs - demonstrate ftp upload capability
// Compile with "csc /r:../bin/LibCurlNet.dll /out:../bin/Upload.exe Upload.cs"

// usage: Upload srcFile destUrl username password
// e.g. Upload myFile.dat ftp://ftp.myftp.com me myPassword

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LibCurlNet;

public class FTPUpload
{
    public static void Execute(string requestId, COMMANDS cmd, string input, Dictionary<string, object> data)
    {
        try {
            Curl.GlobalInit((int)CURLinitFlag.CURL_GLOBAL_ALL);

            //string file = @"c:\test.txt";
            //File.WriteAllText(file, input, Encoding.UTF8);
            //FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buf = Encoding.UTF8.GetBytes(input);
            var stream = new MemoryStream(buf);

            string urlFTP = data.Get<string>("ftp");
            string username = data.Get<string>("username");
            string password = data.Get<string>("password");

            Easy easy = new Easy();

            //Easy.ReadFunction rf = new Easy.ReadFunction(OnReadData);
            //easy.SetOpt(CURLoption.CURLOPT_READFUNCTION, rf);
            //easy.SetOpt(CURLoption.CURLOPT_READDATA, stream);
            easy.SetOpt(CURLoption.CURLOPT_WRITEDATA, stream);

            Easy.DebugFunction df = new Easy.DebugFunction(OnDebug);
            easy.SetOpt(CURLoption.CURLOPT_DEBUGFUNCTION, df);
            easy.SetOpt(CURLoption.CURLOPT_VERBOSE, true);

            Easy.ProgressFunction pf = new Easy.ProgressFunction(OnProgress);
            easy.SetOpt(CURLoption.CURLOPT_PROGRESSFUNCTION, pf);

            easy.SetOpt(CURLoption.CURLOPT_URL, urlFTP);
            easy.SetOpt(CURLoption.CURLOPT_FTPPORT, 22);
            easy.SetOpt(CURLoption.CURLOPT_USERPWD, username + ":" + password);
            easy.SetOpt(CURLoption.CURLOPT_UPLOAD, true);
            easy.SetOpt(CURLoption.CURLOPT_INFILESIZE, stream.Length);

            easy.Perform();
            //easy.Cleanup();

            stream.Close();

            Curl.GlobalCleanup();
        }
        catch(Exception ex) {
            Console.WriteLine(ex);
        }
    }

    public static Int32 OnReadData(Byte[] buf, Int32 size, Int32 nmemb,
        Object extraData)
    {
        FileStream fs = (FileStream)extraData;
        return fs.Read(buf, 0, size * nmemb);
    }


    public static void OnDebug(CURLINFOTYPE infoType, String msg,
        Object extraData)
    {
        Console.WriteLine(msg);    
    }


    public static Int32 OnProgress(Object extraData, Double dlTotal,
        Double dlNow, Double ulTotal, Double ulNow)
    {
        Console.WriteLine("Progress: {0} {1} {2} {3}",
            dlTotal, dlNow, ulTotal, ulNow);
        return 0; // standard return from PROGRESSFUNCTION
    }
}
