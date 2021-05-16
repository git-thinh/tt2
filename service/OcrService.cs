using System;
using System.Collections.Generic;
using Tesseract;

public class OcrService
{
    public static void convertImage2Text_OneOrAllPage(string requestId, COMMANDS cmd, string input, Dictionary<string, object> data)
    {
        var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __CONFIG.REDIS_PORT_READ));

        long docId = 0;
        int page = -1;
        string[] a = input.Split('.');
        long.TryParse(a[0], out docId);
        if (a.Length > 1) int.TryParse(a[1], out page);

        var ocr_lang = data.Get<string>("ocr_lang", "vie");
        var ocr_mode = data.Get<EngineMode>("ocr_mode", EngineMode.Default);
        var ocr_level = data.Get<PageIteratorLevel>("ocr_level", PageIteratorLevel.Word);

        try
        {
            if (redis.HEXISTS(docId.ToString(), page.ToString()))
            {
                var bitmap = redis.HGET_BITMAP(docId, page);
                if (bitmap != null)
                {
                    var dic = new Dictionary<string, object>() {
                            { "id", docId },
                            { "page", page },
                        };

                    using (var engine = new TesseractEngine("tessdata", ocr_lang, ocr_mode))
                    using (var pix = new BitmapToPixConverter().Convert(bitmap))
                    {
                        using (var tes = engine.Process(pix))
                        {
                            string s = tes.GetText().Trim();
                            dic.Add("ocr_text", s);
                            //var boxes = tes.GetSegmentedRegions(level).Select(x =>
                            //    string.Format("{0}_{1}_{2}_{3}", x.X, x.Y, x.Width, x.Height)).ToArray();
                            //dic.Add("box_format", "x_y_width_height");
                            //dic.Add("box_text", string.Join("|", boxes.Select(x => x.ToString()).ToArray()));
                            //dic.Add("box_count", boxes.Length);
                        }
                    }

                    App.Reply(cmd, requestId, input, dic);
                }
            }
        }
        catch (Exception exInfo)
        {
            //string errInfo = cmd.ToString() + " -> " + file + Environment.NewLine + exInfo.Message + Environment.NewLine + exInfo.StackTrace;
            //redis.HSET("_ERROR:PDF:" + cmd.ToString(), docId.ToString(), errInfo);
        }
    }
}
