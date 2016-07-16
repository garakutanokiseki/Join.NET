using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using NVelocity;
using NVelocity.App;
using NVelocity.Exception;

namespace JoinNET.WebServer
{
    class CWebServerData
    {
        public string name { get; set; }
        public string path { get; set; }
        public string time { get; set; }
        public int type { get; set; }
        public string size { get; set; }
    }

    class CWebServer : HttpServer
    {
        Thread m_Thread;
        string m_document_root = ".";
        const int BUFFUER_SIZE = 10240;

        VelocityContext m_velocityctx;

        public CWebServer(int port)
            : base(port)
        {
            //NVelocityの初期化
            Velocity.Init();
            m_velocityctx = new VelocityContext();
        }

        public override void handleGETRequest(HttpProcessor p)
        {

            int nStartByte = 0;
            int nEndByte = 0;
            int nStart = 0;
            int nEnd = 0;
            bool is_range = false;

            //読み取り先のファイルパスを作成する
            string url = System.Web.HttpUtility.UrlDecode(p.http_url);
            string path = m_document_root + url.Replace("/", "\\");
            path = path.Replace("\\\\", "\\");
            path = System.IO.Path.GetFullPath(path);

            if(url.EndsWith("/") == false)
            {
                url = url + "/";
            }

            if (path.Contains(m_document_root) == false)
            {
                //パスの外の場合は404を返す
                path = "";
            }

            //Rangeを設定する
            if (p.httpHeaders.ContainsKey("Range") == true)
            {
                string szStr = (string)p.httpHeaders["Range"];
                szStr = (szStr.Replace(" ", "")).ToLower();

                int index = szStr.IndexOf("bytes=");
                if (index >= 0)
                {
                    is_range = true;

                    nStart = index + 6;
                    nEnd = szStr.IndexOf("-", nStart);
                    string szTmp = szStr.Substring(nStart, nEnd - nStart);
                    nStartByte = int.Parse(szTmp);

                    szTmp = szStr.Substring(nEnd+1);
                    if (szTmp != "")
                        nEndByte = int.Parse(szTmp);
                    else
                        nEndByte = 0;
                }
            }

            if (File.Exists(path))
            {
                // ファイルが存在すればレスポンス・ストリームに書き出す
                FileInfo fileinfo = new FileInfo(path);
                string header = "Content-Length: " + fileinfo.Length.ToString() + "\r\n";

                if (nEndByte <= 0)
                {
                    nEndByte = (int)fileinfo.Length;
                }

                if (is_range == true)
                {
                    string range = string.Format("Content-Range: bytes {0}-{1}/{2}\r\n", nStartByte, nEndByte - 1, nEndByte);//  21010 - 47021 / 47022
                    header = header + range;
                }

                string extention = Path.GetExtension(path);
                Debug.WriteLine("handleGETRequest : extention = " + extention);
                string mime_type = WebUtil.GetMimeType(extention);
                Debug.WriteLine("handleGETRequest : mime_type = " + mime_type);

                p.writeSuccess(header, mime_type);

                //ファイルを読み込む
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                fs.Seek(nStartByte, SeekOrigin.Begin);

                byte[] buf = new byte[BUFFUER_SIZE];

                while (nStartByte < nEndByte)
                {
                    int nReadSize = fs.Read(buf, 0, buf.Length);
                    Debug.WriteLine(string.Format("handleGETRequest : read={2}, start={0}, end={1}", nStartByte, nEndByte, nReadSize));
                    p.outputStream.Write(buf, 0, nReadSize);
                    nStartByte += nReadSize;
                }
                p.outputStream.BaseStream.Flush();
            }
            else if (IsAutoIndexes == true && Directory.Exists(path))
            {
                //ヘッダーを作成する
                string header = "";
                string mime_type = WebUtil.GetMimeType(".html");
                p.writeSuccess(header, mime_type);

                //ディレクトリの場合の処理
                List<CWebServerData> list = new List<CWebServerData>();

                if(path != m_document_root)
                {
                    CWebServerData data = new CWebServerData();
                    data.name = "..";
                    data.path = "..";
                    data.type = 0;
                    data.size = "-";
                    data.time = "";
                    list.Add(data);
                }

                var directories = Directory.EnumerateDirectories(path);
                foreach (string name in directories)
                {
                    CWebServerData data = new CWebServerData();
                    data.name = Path.GetFileName(name);
                    data.path = url + data.name;
                    data.type = 0;
                    data.size = "-";
                    DirectoryInfo info = new DirectoryInfo(name);
                    data.time = info.LastWriteTime.ToShortDateString() + " " + info.LastWriteTime.ToShortTimeString();
                    list.Add(data);
                }

                var files = Directory.EnumerateFiles(path);
                foreach (string name in files)
                {
                    CWebServerData data = new CWebServerData();
                    data.name = Path.GetFileName(name);
                    data.path = url + data.name;
                    data.type = 1;
                    FileInfo info = new FileInfo(name);
                    if(info.Length < 1024)
                        data.size = info.Length.ToString();
                    else if(info.Length < 1048576)
                        data.size = (info.Length / 1024).ToString() + "K";
                    else if (info.Length < 1073741824)
                        data.size = (info.Length / 1048576).ToString() + "M";
                    else
                        data.size = (info.Length / 1073741824).ToString() + "G";
                    data.time = info.LastWriteTime.ToShortDateString() + " " + info.LastWriteTime.ToShortTimeString();

                    list.Add(data);
                }

                m_velocityctx.Put("files", list);
                m_velocityctx.Put("directory", url);

                try
                {
                    //結果を格納するStringWriter
                    System.IO.StringWriter resultWriter = new System.IO.StringWriter();
                    //Shift-JISのテンプレートファイルを読み込み、コンテキストとマージ
                    Velocity.MergeTemplate("filelist.html", "UTF-8", m_velocityctx, resultWriter);
                    //Console.WriteLine(resultWriter.GetStringBuilder().ToString());
                    string result = resultWriter.GetStringBuilder().ToString();
                    //データがutf-8の場合
                    byte[] data = System.Text.Encoding.UTF8.GetBytes(result);
                    p.outputStream.Write(data);

                }
                catch (ResourceNotFoundException ex)
                {
                }
                catch (ParseErrorException ex)
                {
                }

            }
            else
            {
                p.writeFailure();

                string szResponse = string.Format("<html><head><title> 404 Not Found</title ></head><body><h1> Not Found </h1><p> The requested URL {0} was not found on this server.</p></body></html>", p.http_url);
                byte[] byresponse = System.Text.Encoding.ASCII.GetBytes(szResponse);
                p.outputStream.Write(byresponse, 0, byresponse.Length);
                p.outputStream.BaseStream.Flush();
            }
        }

        public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
        {
            Console.WriteLine("POST request: {0}", p.http_url);
            string data = inputData.ReadToEnd();

            string path = m_document_root + p.http_url.Replace("/", "\\");
            // ファイルが存在すればレスポンス・ストリームに書き出す
            if (File.Exists(path))
            {
                p.writeSuccess("");
                byte[] content = File.ReadAllBytes(path);
                //res.OutputStream.Write(content, 0, content.Length);
                p.outputStream.Write(content, 0, content.Length);
                p.outputStream.BaseStream.Flush();
            }
            else
            {
                p.writeFailure();
                p.outputStream.BaseStream.Flush();
            }
        }

        public bool IsAlive
        {
            get {
                if (m_Thread == null) return false;
                return m_Thread.IsAlive;
            }
        }

        //自動的にファイルリストを表示するかの設定
        private bool _IsAutoIndexes = true;
        public bool IsAutoIndexes
        {
            get { return _IsAutoIndexes; }
            set { _IsAutoIndexes = value; }
        }

        public bool listen(string document_root)
        {
            if (m_Thread != null) return false;

            SetDocumentRoot(document_root);

            m_Thread = new Thread(() =>
            {
                Debug.WriteLine("CWebServer::lithen Thread >>");
                try
                { 
                    listen();
                }

                catch (Exception ex)
                {
                    Debug.WriteLine("CWebServer::lithen Error => " + ex.Message);
                }

                Debug.WriteLine("CWebServer::lithen Thread <<");
            });
            m_Thread.Start();

            return true;
        }

        public new void stop()
        {
            Debug.WriteLine("CWebServer::stop >>");
            base.stop();

            while (m_Thread.IsAlive == true)
            {

            }
            m_Thread = null;
            Debug.WriteLine("CWebServer::stop <<");
        }

        public void SetDocumentRoot(string path)
        {
            m_document_root = System.IO.Path.GetFullPath(path);
            if(m_document_root.EndsWith("\\") == false)
            {
                m_document_root = m_document_root + "\\";
            }
        }

        //絶対パスから相対パスを作成する
        private string GetRelativePath(string base_path, string file_path) 
        {
            Uri u1 = new Uri(base_path);
            Uri u2 = new Uri(file_path);

            //絶対Uriから相対Uriを取得する
            Uri relativeUri = u1.MakeRelativeUri(u2);
            //文字列に変換する
            string relativePath = relativeUri.ToString();

            //"/"を"\"に変換する
            return relativePath.Replace('/', '\\');
        }
}
}
