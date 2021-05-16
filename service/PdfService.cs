using Newtonsoft.Json;
using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public class PdfService
{
    #region [ + ]

    static byte[] _pageAsBitmapBytes(PdfDocument doc, int pageCurrent, DOC_TYPE docType)
    {
        int w = (int)doc.PageSizes[pageCurrent].Width;
        int h = (int)doc.PageSizes[pageCurrent].Height;

        ////if (w >= h) w = this.Width;
        ////else w = 1200;
        //if (w < 1200) w = 1200;
        //h = (int)((w * doc.PageSizes[i].Height) / doc.PageSizes[i].Width);

        if (w > 1200)
        {
            w = 1200;
            h = (int)((w * doc.PageSizes[pageCurrent].Height) / doc.PageSizes[pageCurrent].Width);
        }

        if (docType == DOC_TYPE.PNG_OGRINAL)
        {
            using (var image = doc.RenderTransparentBG(pageCurrent, w, h, 100, 100))
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
        else
        {
            using (var image = doc.Render(pageCurrent, w, h, 100, 100, false))
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }

    public static void UpdateDocInfo(string requestId, string file)
    {
        long docInfoId = 0;
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __CONFIG.REDIS_PORT_WRITE));
            var cmd = COMMANDS.DOC_INFO.ToString();
            try
            {
                int pageTotal = 0;
                using (var doc = PdfDocument.Load(file))
                {
                    pageTotal = doc.PageCount;
                    long fileSize = new FileInfo(file).Length;
                    docInfoId = StaticDocument.BuildId(DOC_TYPE.INFO_OGRINAL, pageTotal, fileSize);
                    var docInfo = new oDocument()
                    {
                        id = docInfoId,
                        file_length = fileSize,
                        file_name_ascii = "",
                        file_name_ogrinal = Path.GetFileNameWithoutExtension(file),
                        file_page = pageTotal,
                        file_path = file,
                        file_type = DOC_TYPE.PDF_OGRINAL,
                        infos = doc.GetInformation().toDictionary(),
                        metadata = ""
                    };
                    var json = JsonConvert.SerializeObject(docInfo, Formatting.Indented);
                    var bsInfo = Encoding.UTF8.GetBytes(json);
                    var lz = LZ4.LZ4Codec.Wrap(bsInfo);
                    ////var decompressed = LZ4Codec.Unwrap(compressed);
                    redis.HSET("_DOC_INFO", docInfoId.ToString(), lz);
                }
                //if (docInfoId > 0)
                //    redis.ReplyRequest(requestId, cmd, 1, docInfoId, pageTotal, "", file);
            }
            catch (Exception exInfo)
            {
                string errInfo = cmd.ToString() + " -> " + file + Environment.NewLine + exInfo.Message + Environment.NewLine + exInfo.StackTrace;
                redis.HSET("_ERROR:PDF:" + cmd.ToString(), docInfoId.ToString(), errInfo);
            }
        }
    }

    public static void SplitAllJpeg(string requestId, COMMANDS cmd, string input, Dictionary<string, object> data)
    {
        string file = input;
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __CONFIG.REDIS_PORT_WRITE));
            long docId = 0;
            try
            {
                using (var doc = PdfDocument.Load(file))
                {
                    int pageTotal = doc.PageCount;
                    DOC_TYPE docType = DOC_TYPE.JPG_OGRINAL;
                    if (data.ContainsKey("png")) docType = DOC_TYPE.PNG_OGRINAL;
                    docId = StaticDocument.BuildId(docType, pageTotal, new FileInfo(file).Length);

                    if (redis.HEXISTS(docId.ToString(), "0"))
                    {
                        App.Reply(cmd, requestId, input, new Dictionary<string, object>() {
                            { "id", docId },
                            { "type", docType.ToString() },
                            { "size", 0 },
                            { "page", 0 },
                            { "page_total", pageTotal },
                        });
                        return;
                    }

                    var sizes = new Dictionary<string, string>();
                    for (int i = 0; i < pageTotal; i++)
                    {
                        byte[] buf = null;
                        int len = 0;
                        bool ok = false;
                        string err = "";
                        try
                        {
                            buf = _pageAsBitmapBytes(doc, i, docType);
                            len = buf.Length;
                            ok = redis.HSET(docId, i, buf);
                        }
                        catch (Exception ex)
                        {
                            err = ex.Message + Environment.NewLine + ex.StackTrace;
                        }
                        App.Reply(cmd, requestId, input, new Dictionary<string, object>() {
                            { "id", docId },
                            { "type", docType.ToString() },
                            { "size", len },
                            { "page", i },
                            { "page_total", pageTotal },
                        });
                        sizes.Add(string.Format("{0}:{1}", docId, i), len.ToString());
                        //Thread.Sleep(100);
                    }
                    redis.HMSET("_IMG_SIZE", sizes);
                    App.Reply(cmd, requestId, input, new Dictionary<string, object>() {
                            { "id", docId },
                            { "type", docType.ToString() },
                            { "size", 0 },
                            { "page", pageTotal },
                            { "page_total", pageTotal },
                        });
                }
            }
            catch (Exception exInfo)
            {
                string errInfo = cmd.ToString() + " -> " + file + Environment.NewLine + exInfo.Message + Environment.NewLine + exInfo.StackTrace;
                redis.HSET("_ERROR:PDF:" + cmd.ToString(), docId.ToString(), errInfo);
            }
        }
    }

    #endregion
}
