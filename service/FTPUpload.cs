using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;

public class FTPUpload
{
    public static void Execute(string requestId, COMMANDS cmd, string input, Dictionary<string, object> data)
    {
        string file = @"c:\test.txt";
        string host = data.Get<string>("host");
        int port = data.Get<int>("port");
        string username = data.Get<string>("username");
        string password = data.Get<string>("password");

        try
        {
            using (var client = new SftpClient(host, port, username, password))
            {
                client.Connect();

                //var ls = client.ListDirectory("/").Select(x => x.FullName).ToArray();

                FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                client.UploadFile(stream, "/root/test/" + Guid.NewGuid().ToString() + ".txt");
            }
        }
        catch (Exception ex)
        {
        }
    }

}
