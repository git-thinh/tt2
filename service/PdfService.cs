﻿using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
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


    public static void __mmf_Write(oMMF o, Stream body)
    {
        var lsSize = new List<Value>();
        foreach (int len in o.sizes) lsSize.Add(Value.ForNumber(len));
        var head = new Struct
        {
            Fields =
            {
                ["id"] = Value.ForNumber(o.id),
                ["compress"] = Value.ForBool(o.compress),
                ["type"] = Value.ForString(o.type.ToString()),
                ["title"] = Value.ForString(string.Empty),
                ["author"] = Value.ForString(string.Empty),
                ["tag"] = Value.ForString(string.Empty),
                ["info"] = Value.ForString(JsonConvert.SerializeObject(o.infos)),
                ["year_created"] = Value.ForNumber(0),
                ["size"] = Value.ForList(lsSize.ToArray()),
            }
        };
        byte[] bs;
        var lsHead = new List<byte>();
        var bfh = head.ToByteArray();
        bs = BitConverter.GetBytes(bfh.Length);
        lsHead.AddRange(bs);
        lsHead.AddRange(bfh);

        ////var vd = Struct.Parser.ParseFrom(bfh);
        ////Value vid;
        ////vd.Fields.TryGetValue("id", out vid);
        ////long id2 = (long)vid.NumberValue;

        //int size = buf.Length;
        //MemoryMappedFile map = MemoryMappedFile.Create(MapProtection.PageReadWrite, size, name);
        //using (Stream view = map.MapView(MapAccess.FileMapWrite, 0, size))
        //    view.Write(buf, 0, size);
        //map.Close();
    }


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

    public static void SplitAllJpeg(string requestId, string file)
    {
    }

    public static void SplitAllPng(string requestId, string file)
    {
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __CONFIG.REDIS_PORT_WRITE));
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

    public static void PDF_MMF_TT(string requestId, string file)
    {
        if (File.Exists(file))
        {
            var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __CONFIG.REDIS_PORT_WRITE));
            var cmd = COMMANDS.PDF_MMF_TT.ToString();
            long docId = 0;
            try
            {
                using (var doc = PdfDocument.Load(file))
                {
                    MemoryStream body = new MemoryStream();
                    int pageTotal = doc.PageCount;
                    docId = StaticDocument.BuildId(DOC_TYPE.TT_FILE, pageTotal, new FileInfo(file).Length);
                    var sizes = new List<int>();
                    for (int i = 0; i < pageTotal; i++)
                    {
                        byte[] buf = null;
                        bool ok = false;
                        string err = "";
                        try
                        {
                            buf = _pageAsBitmapBytes(doc, i);
                            sizes.Add(buf.Length);
                            body.Write(buf, 0, buf.Length);
                        }
                        catch (Exception ex)
                        {
                            err = ex.Message + Environment.NewLine + ex.StackTrace;
                        }
                        redis.ReplyRequest(requestId, cmd, ok ? 1 : 0, docId, i, "PROCESSING", file, err);
                    }
                    oMMF mmf = new oMMF()
                    {
                        file = Path.Combine(__CONFIG.PATH_TT_RAW, string.Format("{0}.tt", docId)),
                        compress = false,
                        id = docId,
                        infos = doc.GetInformation().toDictionary(),
                        sizes = sizes,
                        type = MMF_TYPE.PNG
                    };
                    __mmf_Write(mmf, body);

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
