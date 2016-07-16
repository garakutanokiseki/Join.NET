using System;
using System.Text;
using System.Net;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;

using WebSocket4Net;
using System.Collections.Generic;

namespace JoinNET
{

    public class CJoinnetClient
    {
        //サーバーのURL
#if true
        private const string m_szHPUrl = "https://livechatcof.herokuapp.com/";
        private const string m_szCGIUrl = "https://livechatcof.herokuapp.com/auth/login";
        private const string m_szChatUrl = "wss://livechatcof.herokuapp.com/chat";
#else
        private const string m_szHPUrl = "http://192.168.33.10:5000/";
        private const string m_szCGIUrl = "http://192.168.33.10:5000/auth/login";
        private const string m_szChatUrl = "ws://192.168.33.10:5000/chat";
#endif
        //Webクライアント
        WebClientWithTimeout m_WC;

        //ユーザー名
        private string m_szUser;

        //ログイン状態を判別するフラグ
        private bool m_is_login = false;

        //イベントID
        public enum EventID
        {
            Unknown = 0,
            message_recevied = 1,
            opened,
            closed,
            error,
        }

        //イベント処理用
        public Func<EventID, Object, bool> m_EventHndler = null;

        /// <summary>
        /// 指定したユーザーでログインし初期化する
        /// </summary>
        /// <param name="szUser"></param>
        /// <param name="szPass"></param>
        /// <returns></returns>
        public bool Login(string szUser, string szPass)
        {
            Debug.WriteLine("CJoinnetClient::Login >>");
            Debug.WriteLine("CJoinnetClient::Login authen_username={0}, password={1}", szUser, szPass);
            m_WC = new WebClientWithTimeout { Encoding = Encoding.GetEncoding("utf-8") };
            try
            {
                m_WC.UploadValues(m_szCGIUrl,
                    new NameValueCollection
                        {
                            {"authen_username", szUser},
                            {"authen_password", szPass},
                        });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CJoinnetClient::Login << Fail => " + ex.Message);
                m_is_login = false;
                return false;
            }

            m_szUser = szUser;
            m_is_login = true; 

            Debug.WriteLine("CJoinnetClient::Login <<");
            return true;
        }

        /// <summary>
        /// 使用したメモリを解放する
        /// </summary>
        /// <returns></returns>
        public bool Logout()
        {
            m_WC.Dispose();
            m_WC = null;
            m_szUser = "";

            if(m_WS != null)
            {
                CloseChat();
            }

            return true;
        }

        public string Get(string url)
        {
            return m_WC.DownloadString(url);
        }

        //////////////////////////////////////////////////////////////////////
        WebSocket m_WS = null;

        public bool OpenChat()
        {
            if(m_is_login == false)
            {
                return false;
            }

            string cookie = "";
            string cookie_data = "";

            try
            {
                cookie = m_WC.get_cookie_header(new Uri(m_szHPUrl));
                int start = cookie.IndexOf("username");
                int end = cookie.IndexOf("\"", start + 10);
                cookie_data = cookie.Substring(start + 9, end - (start + 8));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CJoinnetClient::OpenChat Error" + ex.Message);
                return false;
            }

            var list_cookie = new List<KeyValuePair<string, string>>();
            list_cookie.Add(new KeyValuePair<string, string>("username", cookie_data));

            m_WS = new WebSocket(m_szChatUrl, "", list_cookie);

            /// 文字列受信
            m_WS.MessageReceived += (s, e) =>
            {
                Debug.WriteLine("CJoinnetClient::OpenChat " + e.Message);
                if(m_EventHndler != null)
                {
                    m_EventHndler(EventID.message_recevied, e.Message);
                }
            };

            /// サーバー接続完了
            m_WS.Opened += (s, e) =>
            {
                Debug.WriteLine("CJoinnetClient::OpenChat サーバーに接続しました。 : " + e.ToString());
                //_ws.Send("{\"name\":\"俺様\", \"text\":\"こんちは\"}");
            };

            /// 接続断の発生
            m_WS.Error += (s, e) =>
            {
                Debug.WriteLine("CJoinnetClient::OpenChat OnError : " + e.Exception.Message);
                /// 再接続を試行する
            };

            /// 接続断の発生
            m_WS.Closed += (s, e) =>
            {
                Debug.WriteLine("CJoinnetClient::OpenChat OnClose : " + e.ToString());
                /// 再接続を試行する
                OpenChat();
            };

            /// サーバー接続開始
            m_WS.Open();

            return true;
        }

        public void CloseChat()
        {
            m_WS.Close();
            m_WS.Dispose();
        }

        public void Send(string message)
        {
            m_WS.Send(message);
        }
    }
}
