/* The MIT License (MIT)
* 
* Copyright (c) 2015 Marc Clifton
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.IO;
using System.Net;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phantasma.Kademlia.Protocols
{
    public static class RestCall
    {
        private static int REQUEST_TIMEOUT = 500;       // 500 ms for response.

        public static string Get(string url)
        {
            string ret = String.Empty;
            WebResponse resp = null;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            resp = request.GetResponse();
            ret = new StreamReader(resp.GetResponseStream()).ReadToEnd();
            resp.Close();

            return ret;
        }

        public static R Post<R, E>(string url, object obj, out E errorResponse, out bool timeoutError, int? timeout = null) where R : new() where E : ErrorResponse, new()
        {
            errorResponse = default(E);
            timeoutError = false;
            R target = Activator.CreateInstance<R>();
            Stream st = null;
            string json = string.Empty;
            string retjson = string.Empty;

            json = JsonConvert.SerializeObject(obj);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = json.Length;
            request.Timeout = timeout ?? REQUEST_TIMEOUT;

            try
            {
                st = request.GetRequestStream();
            }

            catch (WebException ex)
            {
                E error = Activator.CreateInstance<E>();
                error.ErrorMessage = ex.Message;

                // Bail now.
                return target;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            st.Write(bytes, 0, bytes.Length);
            WebResponse resp;

            try
            {
                resp = request.GetResponse();
                retjson = new StreamReader(resp.GetResponseStream()).ReadToEnd();
                JObject jobj = JObject.Parse(retjson);
                JsonConvert.PopulateObject(jobj.ToString(), target);
                resp.Close();
            }
            catch (WebException ex)
            {
                E error = Activator.CreateInstance<E>();

                try
                {
                    retjson = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                    JObject jobj = JObject.Parse(retjson);
                    JsonConvert.PopulateObject(jobj.ToString(), error);
                    errorResponse = error;
                }
                catch
                {
                    // This is a timeout exception
                    timeoutError = true;
                }
            }
            catch (Exception ex)
            {
                E error = Activator.CreateInstance<E>();
                error.ErrorMessage = ex.Message;
            }

            st.Close();

            return target;
        }
    }
}