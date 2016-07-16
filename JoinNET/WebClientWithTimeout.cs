using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace JoinNET
{
    class WebClientWithTimeout : WebClient
    {
        private CookieContainer cookieContainer = new CookieContainer();

        private int timeout = 60000;
        public int Timeout
        {
            get
            {
                return timeout;
            }
            set
            {
                timeout = value;
            }
        }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = timeout;

            // WebClientはWebRequestのラッパーにすぎないので、
            // GetWebRequestのところの動作をちょっと横取りして書き換える
            if (w is HttpWebRequest)
            {
                (w as HttpWebRequest).CookieContainer = cookieContainer;
            }

            return w;
        }

        public string get_cookie_header(Uri uri)
        {
            return cookieContainer.GetCookieHeader(uri);
        }
    }
}
