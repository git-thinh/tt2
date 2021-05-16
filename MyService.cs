using System;
using System.ServiceProcess;

public class MyService : ServiceBase
{
    public static readonly string SERVICE_ID = Guid.NewGuid().ToString();
    public MyService()
    {
        ServiceName = SERVICE_ID;
    }

    protected override void OnStart(string[] args)
    {
        App.StartOnWindowService(args);
    }

    protected override void OnStop()
    {
        App.Stop();
    }
}
