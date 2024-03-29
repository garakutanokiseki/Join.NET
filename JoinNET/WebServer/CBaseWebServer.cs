﻿using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

namespace JoinNET.WebServer
{
    /// <summary>
    /// Http Server Base Class
    /// </summary>
    public abstract class HttpServer
    {

        protected int port;
        TcpListener listener;
        bool is_active = true;

        public HttpServer(int port)
        {
            this.port = port;
        }

        public void listen()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            while (is_active)
            {
                TcpClient s = listener.AcceptTcpClient();
                HttpProcessor processor = new HttpProcessor(s, this);
                Thread thread = new Thread(new ThreadStart(processor.process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public void stop()
        {
            if (is_active == false) return;
            is_active = false;
            listener.Stop();
        }

        public int Port
        {
            get
            {
                if (listener == null) return 0;
                IPEndPoint endpoint = (IPEndPoint)listener.Server.LocalEndPoint;
                return endpoint.Port;
            }
        }

        public abstract void handleGETRequest(HttpProcessor p);
        public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);
    }

    /// <summary>
    /// Htttp Request Header Parser Class
    /// </summary>
    public class HttpProcessor
    {
        public TcpClient socket;
        public HttpServer srv;

        private Stream inputStream;
        public BinaryWriter outputStream;

        public String http_method;
        public String http_url;
        public String http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();


        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient s, HttpServer srv)
        {
            this.socket = s;
            this.srv = srv;
        }


        private string streamReadLine(Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        public void process()
        {
            Debug.WriteLine("HttpProcessor::process >>");
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new BinaryWriter(new BufferedStream(socket.GetStream()));
            try
            {
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET"))
                {
                    handleGETRequest();
                }
                else if (http_method.Equals("POST"))
                {
                    handlePOSTRequest();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("HttpProcessor::process Exception(1): " + e.ToString());
                writeFailure();
            }


            try
            {
                outputStream.Flush();
                // bs.Flush(); // flush any remaining output
                inputStream = null; outputStream = null; // bs = null;            
                socket.Close();
            }
            catch (Exception e)
            {
                Debug.WriteLine("HttpProcessor::process Exception(2): " + e.ToString());
                writeFailure();
            }
            Debug.WriteLine("HttpProcessor::process <<");
        }

        public void parseRequest()
        {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];

            Debug.WriteLine("starting: " + request);
        }

        public void readHeaders()
        {
            Debug.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Debug.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Debug.WriteLine("header: {0}:{1}", name, value);
                httpHeaders[name] = value;
            }
        }

        public void handleGETRequest()
        {
            srv.handleGETRequest(this);
        }

        private const int BUF_SIZE = 4096;
        public void handlePOSTRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Debug.WriteLine("get post data start");
            int content_len = 0;
            MemoryStream ms = new MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length"))
            {
                content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(
                        String.Format("POST Content-Length({0}) too big for this simple server",
                          content_len));
                }
                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Debug.WriteLine("starting Read, to_read={0}", to_read);

                    int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Debug.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
            }
            Debug.WriteLine("get post data end");
            srv.handlePOSTRequest(this, new StreamReader(ms));

        }

        public void writeSuccess(string header = "", string content_type = "text/html")
        {
            string responce = "HTTP/1.0 200 OK\r\nContent-Type: " + content_type + "\r\n" + header + "Connection: close\r\n\r\n";
            Debug.WriteLine("writeSuccess : responce = " + responce);
            byte[] byresponse = System.Text.Encoding.ASCII.GetBytes(responce);
            outputStream.Write(byresponse);
        }

        public void writeFailure()
        {
            outputStream.Write(System.Text.Encoding.ASCII.GetBytes("HTTP/1.0 404 File not found\r\nContent-Type: text/html\r\nConnection: close\r\n\r\n"));
        }
    }
}
