public class __CONFIG
{
    public const string HTTP_HOST = "127.0.0.1";
    public const int HTTP_PORT = 12300;

    public const int REDIS_DB = 15;
    public const string REDIS_HOST = "127.0.0.1";
    public const int REDIS_PORT_WRITE = 1000;
    public const int REDIS_PORT_READ = 1001;

    public const string CHANNEL_NAME = "TT2";

    public static string PATH_TT_RAW = System.Configuration.ConfigurationManager.AppSettings["PATH_TT_RAW"];
    public static string PATH_TT_ZIP = System.Configuration.ConfigurationManager.AppSettings["PATH_TT_ZIP"];
}
