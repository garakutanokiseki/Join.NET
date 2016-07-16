using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

namespace TCPtunnelViaUDP
{
    class CTCPClient
    {
        TcpClient m_Client;

        public CTCPClient(TcpClient client)
        {
            m_Client = client;
        }

        public CTCPClient(string address, int port)
        {
            m_Client = new TcpClient(address, port);
        }

        public bool Close()
        {
            try
            {
                m_Client.Client.Disconnect(false);
                m_Client.Client.Close();
                m_Client.Client.Dispose();
                m_Client.Close();

            }

            catch (Exception ex)
            {
                Debug.WriteLine("CTCPClient::Close Error = " + ex.Message);
                return false;
            }

            return true;
        }

        public bool Send(byte [] data, int length)
        {
            try
            {
                //NetworkStreamを取得する
                System.Net.Sockets.NetworkStream ns = m_Client.GetStream();

                //読み取り、書き込みのタイムアウトを10秒にする
                //デフォルトはInfiniteで、タイムアウトしない
                //(.NET Framework 2.0以上が必要)
                ns.ReadTimeout = 10000;
                ns.WriteTimeout = 10000;

                //データを送信する
                ns.Write(data, 0, length);
            }
            catch(Exception ex)
            {
                Console.WriteLine("CTCPClient::Send Error => " + ex.Message);

                return false;
            }

            return true;
        }

        public bool CanRead
        {
            get { return m_Client.GetStream().CanRead; }
        }

        public bool CanWrite
        {
            get { return m_Client.GetStream().CanWrite; }
        }

        public bool Poll(int microSeconds, SelectMode mode)
        {
            return m_Client.Client.Poll(microSeconds, mode);
        }

        public int Available
        {
            get { return m_Client.Available; }
        }

        public bool Receive(ref byte [] data, ref int data_size)
        {
            try
            {
                
                data_size = 0;
                NetworkStream _NetworkStreamLocal = m_Client.GetStream();

                //データの一部を受信する
                _NetworkStreamLocal.ReadTimeout = 1000;
                data_size = _NetworkStreamLocal.Read(data, 0, data.Length);

                Console.WriteLine(string.Format("CTCPClient::Receive data_size={2}, Available={0}, Connected={1}", m_Client.Available, m_Client.Connected, data_size));

                return true;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("CTCPClient::Receive Error = " + ex.Message);
            }

            return false;
        }
    }
}
