using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using System;
using System.Net;

namespace Phantasma.Neo.Utils
{
    public enum RequestType
    {
        GET,
        POST
    }

    public static class RequestUtils
    {
        public static DataNode Request(RequestType kind, string url, DataNode data = null)
        {
            string contents;

            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            try
            {
                switch (kind)
                {
                    case RequestType.GET:
                        {
                            contents = GetWebRequest(url); break;
                        }
                    case RequestType.POST:
                        {
                            var paramData = data != null ? JSONWriter.WriteToString(data) : "{}";
                            contents = PostWebRequest(url, paramData);
                            break;
                        }
                    default: return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }

            if (string.IsNullOrEmpty(contents))
            {
                return null;
            }

            //File.WriteAllText("response.json", contents);

            var root = JSONReader.ReadFromString(contents);
            return root;
        }

        public static string GetWebRequest(string url)
        {
            if (!url.Contains("://"))
            {
                url = "http://" + url;
            }

            using (var  client = new WebClient { Encoding = System.Text.Encoding.UTF8 })
            {
				client.Headers.Add("Content-Type", "application/json-rpc");
                return client.DownloadString(url);
            }
        }

        public static string PostWebRequest(string url, string paramData)
        {
            using (var client = new WebClient { Encoding = System.Text.Encoding.UTF8 })
            {
				client.Headers.Add("Content-Type", "application/json-rpc");
                return client.UploadString(url, paramData);
            }
        }
    }
}
