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

    static byte[] _pageAsBitmapBytes(PdfDocument doc, int pageCurrent)
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

        using (var image = doc.RenderTransparentBG(pageCurrent, w, h, 100, 100))
        using (var ms = new MemoryStream())
        {
            image.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
    }

    static void _updateDocInfo(string requestId, string file)
    {
        long docInfoId = 0, docId = 0;
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __PORT_WRITE));
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
                if (docInfoId > 0)
                    redis.ReplyRequest(requestId, cmd, 1, docInfoId, pageTotal, "", file);
            }
            catch (Exception exInfo)
            {
                string errInfo = cmd.ToString() + " -> " + file + Environment.NewLine + exInfo.Message + Environment.NewLine + exInfo.StackTrace;
                redis.HSET("_ERROR:PDF:" + cmd.ToString(), docInfoId.ToString(), errInfo);
            }
        }
    }

    static void _splitAllJpeg(string requestId, string file)
    {
    }

    static void _splitAllPng(string requestId, string file)
    {
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __PORT_WRITE));
            var cmd = COMMANDS.PDF_SPLIT_ALL_PNG.ToString();
            long docId = 0;
            try
            {
                using (var doc = PdfDocument.Load(file))
                {
                    int pageTotal = doc.PageCount;
                    docId = StaticDocument.BuildId(DOC_TYPE.PNG_OGRINAL, pageTotal, new FileInfo(file).Length);
                    var sizes = new Dictionary<string, string>();
                    for (int i = 0; i < pageTotal; i++)
                    {
                        byte[] buf = null;
                        string slen = "";
                        bool ok = false;
                        string err = "";
                        try
                        {
                            buf = _pageAsBitmapBytes(doc, i);
                            slen = buf.Length.ToString();
                            ok = redis.HSET(docId, i, buf);
                        }
                        catch (Exception ex)
                        {
                            err = ex.Message + Environment.NewLine + ex.StackTrace;
                        }
                        redis.ReplyRequest(requestId, cmd, ok ? 1 : 0, docId, i, "PROCESSING", file, err);
                        sizes.Add(string.Format("{0}:{1}", docId, i), slen);
                    }
                    redis.HMSET("_IMG_SIZE", sizes);
                    redis.ReplyRequest(requestId, cmd, 1, docId, pageTotal, "COMPLETE", file);
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
