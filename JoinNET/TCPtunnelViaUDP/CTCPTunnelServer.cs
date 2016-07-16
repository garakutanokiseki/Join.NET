using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using UDPTransfer;

namespace TCPtunnelViaUDP
{
    /// <summary>
    /// トンネルサーバークラス
    /// </summary>
    class CTCPTunnelServer
    {
        bool m_isListening = false;

        //通信先アドレス・ポート番号
        public string m_address = "";
        public int m_port = 0;

        //トンネル送信ソケット
        CUDPTransfer m_UdpTransfer;

        //送信ソケット保持配列
        List<TCPData> m_arSockets = new List<TCPData>();

        //受信情報保持配列
        List<AckData> m_arAck = new List<AckData>();

        // 受信イベント
        ManualResetEvent m_ReciveEvent = new ManualResetEvent(false);

        public CTCPTunnelServer(Socket udp_socket)
        {
            //UDPソケットを作成する
            m_UdpTransfer = new CUDPTransfer();
            m_UdpTransfer.AttachSocket(udp_socket);
            m_UdpTransfer.EventHndler = EventHandler_UDP;

            //m_UdpTransfer.OpenSocket();
            IPEndPoint IEP = (IPEndPoint)m_UdpTransfer.GetSocket().LocalEndPoint;

            Debug.WriteLine(string.Format("Address={0}, Port={1}", IEP.Address.ToString(), IEP.Port));
        }

        /// <summary>
        /// トンネルスレッドを開始する
        /// </summary>
        /// <returns></returns>
        public bool Run()
        {
            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run >>", Thread.CurrentThread.ManagedThreadId));

            //UDP通信待機を開始する
            m_UdpTransfer.RunListen();
            m_UdpTransfer.RunCheckAckThread();

            m_isListening = true;

            byte[] resBytes = new byte[59392];
            int resSize = 0;

            while (m_isListening == true)
            {

                //受信まで待機する
                if (m_arSockets.Count == 0)
                {
                    m_ReciveEvent.Reset();
                }
                m_ReciveEvent.WaitOne(1000);

                int count = m_arSockets.Count;
                for (int index = 0; index < count; ++index)
                {
                    try
                    {
                        if(index >= m_arSockets.Count)
                        {
                            break;
                        }
                        TCPData tcpdata = (TCPData)m_arSockets[index];

                        tcpdata.socket.Poll(50, SelectMode.SelectRead);

                        //データ受信し接続元に送り返す
                        if (tcpdata.socket.CanRead == true && tcpdata.socket.Available > 0)
                        {
                            //受信する
                            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run [{1}] Read", Thread.CurrentThread.ManagedThreadId, tcpdata.client_socket_num));
                            tcpdata.socket.Receive(ref resBytes, ref resSize);

                            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run [{1}] Received size={2}", Thread.CurrentThread.ManagedThreadId, tcpdata.client_socket_num, resSize));

                            //受信したデータを送信する
                            byte[] send_data = new byte[resSize + 8];
                            int pos = 0;
                            Buffer.BlockCopy(BitConverter.GetBytes(tcpdata.client_socket_num), 0, send_data, pos, 4);
                            pos += 4;
                            Buffer.BlockCopy(BitConverter.GetBytes(UDPData.COMMAND_SEND), 0, send_data, pos, 4);
                            pos += 4;
                            Buffer.BlockCopy(resBytes, 0, send_data, pos, resSize);

                            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run [{1}] Send to {2}:{3}", Thread.CurrentThread.ManagedThreadId, tcpdata.client_socket_num, tcpdata.address.ToString(), tcpdata.port));
                            uint id = m_UdpTransfer.SendData(tcpdata.address, tcpdata.port, send_data, 0);

                            //データが送信されたことを確認する
                            int retry = 0;
                            while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 500)
                            {
                                Thread.Sleep(10);
                                ++retry;
                            }

                        }

                        //切断されたかを確認する
                        if (tcpdata.socket.CanWrite == true)
                        {
                            tcpdata.socket.Send(resBytes, 0);
                            if (tcpdata.socket.Poll(0, SelectMode.SelectRead) == true && tcpdata.socket.Available == 0)
                            {
                                Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run [{1}] Received remote socket is closed", Thread.CurrentThread.ManagedThreadId, tcpdata.client_socket_num));

                                //ソケットをクローズする
                                DeleteTCPdata(tcpdata.client_socket_num);
                                --count;
                                --index;

                                //切断情報を送る
                                //受信したデータを送信する
                                byte[] send_data = new byte[8];
                                int pos = 0;
                                Buffer.BlockCopy(BitConverter.GetBytes(tcpdata.client_socket_num), 0, send_data, pos, 4);
                                pos += 4;
                                Buffer.BlockCopy(BitConverter.GetBytes(UDPData.COMMAND_CLOSE_SOCKET), 0, send_data, pos, 4);
                                pos += 4;

                                Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run [{1}] SendAck Close to {2}:{3}", Thread.CurrentThread.ManagedThreadId, tcpdata.client_socket_num, tcpdata.address.ToString(), tcpdata.port));
                                uint id = m_UdpTransfer.SendData(tcpdata.address, tcpdata.port, send_data, 0);

                                //データが送信されたことを確認する
                                int retry = 0;
                                while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 500)
                                {
                                    Thread.Sleep(10);
                                    ++retry;
                                }

                            }
                        }
                    }

                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run Error : {1}({2})", Thread.CurrentThread.ManagedThreadId, ex.Message, ex.StackTrace));
                    }

                    Thread.Sleep(1);
                }

            }

            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::Run <<", Thread.CurrentThread.ManagedThreadId));

            return true;
        }

        /// <summary>
        /// トンネルスレッドを停止する
        /// </summary>
        /// <returns></returns>
        public bool Stop()
        {
            m_UdpTransfer.StopCheckAckThread();
            m_UdpTransfer.StopListen();

            m_ReciveEvent.Set();
            m_isListening = false;

            foreach(TCPData data in m_arSockets)
            {
                data.socket.Close();
            }
            m_arSockets.Clear();

            return true;
        }

        /// <summary>
        /// 接続要求を送る
        /// </summary>
        /// <param name="ToAddress"></param>
        /// <param name="ToPort"></param>
        /// <returns></returns>
        public bool SendConnectionRequest(string ToAddress, int ToPort)
        {
            Debug.WriteLine("CTCPTunnelServer::SendConnectionRequest");
            return SendCommand(IPAddress.Parse(ToAddress), ToPort, 0, UDPData.COMMAND_CONNECT_REQUEST);
        }

        /// <summary>
        /// 接続中のソケットリストを取得する
        /// </summary>
        /// <returns></returns>
        public List<TCPData> GetCurrentConnection()
        {
            List<TCPData> list = new List<TCPData>();

            Debug.WriteLine(string.Format("CTCPTunnelServer::GetCurrentConnection count = {0}", m_arSockets.Count));

            foreach (TCPData tcpdata in m_arSockets)
            {
                bool is_found = false;
                foreach (TCPData tmp in list)
                {
                    if (tmp.address == tcpdata.address)
                    {
                        is_found = true;
                        break;
                    }

                }
                if(is_found == false)
                {
                    list.Add(tcpdata);
                }
            }

            return list;
        }

        private TCPData FindTCPdata(uint socket_num)
        {
            foreach(TCPData tcpdata in m_arSockets)
            {
                if(tcpdata.client_socket_num == socket_num)
                {
                    return tcpdata;
                }

            }

            return null;
        }

        private void AddTCPData(TCPData data)
        {
            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::AddTCPData socket_num = [{1}]", Thread.CurrentThread.ManagedThreadId, data.client_socket_num));
            m_arSockets.Add(data);
        }

        private void DeleteTCPdata(uint socket_num)
        {
            int count = m_arSockets.Count;
            for (int index = 0; index < count; ++index)
            {
                TCPData tcpdata = (TCPData)m_arSockets[index];
                if (tcpdata.client_socket_num == socket_num)
                {
                    Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelServer::DeleteTCPdata socket_num = [{1}]", Thread.CurrentThread.ManagedThreadId, socket_num));
                    tcpdata.socket.Close();
                    m_arSockets.RemoveAt(index);
                    return;
                }

            }
        }

        private bool SendCommand(IPAddress ToAddress, int ToPort, int port, uint command)
        {
            Debug.WriteLine("CTCPTunnelServer::SendCommand cmd = " + command.ToString());

            //接続情報を送る
            byte[] send_data = new byte[8];
            int pos = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(port), 0, send_data, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(command), 0, send_data, pos, 4);
            pos += 4;

            Debug.WriteLine(string.Format("CTCPTunnelServer::SendCommand To({0} : {1})", ToAddress.ToString(), ToPort));

            uint id = m_UdpTransfer.SendData(ToAddress, ToPort, send_data, 0);

            //データが送信されたことを確認する
            int retry = 0;
            while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 100)
            {
                Thread.Sleep(10);
                ++retry;
            }

            return m_UdpTransfer.IsRecivedACK(id);
        }

        //UDP受信イベントハンドラ
        bool EventHandler_UDP(CUDPTransfer.EventID eventid, Object obj)
        {
            Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP >>"));
            switch (eventid)
            {
                case CUDPTransfer.EventID.DataRecived:
                    {
                        //イベントを設定する
                        m_ReciveEvent.Set();

                        //受信データ
                        UDPData udpdata = new UDPData();

                        //データ変換用バッファ
                        byte[] byTmp = new byte[8];
                        int pos = 0;
                        int data_length;

                        CUDPTransfer.DataRecived data = (CUDPTransfer.DataRecived)obj;
                        Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP data size = {0}", data.payload.Length));

                        //受信データをUDPDataに変換する
                        Buffer.BlockCopy(data.payload, pos, byTmp, 0, 4);
                        pos += 4;
                        udpdata.socket_num = BitConverter.ToUInt32(byTmp, 0);

                        Buffer.BlockCopy(data.payload, pos, byTmp, 0, 4);
                        pos += 4;
                        udpdata.command = BitConverter.ToUInt32(byTmp, 0);

                        data_length = data.payload.Length - pos;
                        if(data_length > 0)
                        {
                            udpdata.data = new byte[data_length];
                            Buffer.BlockCopy(data.payload, pos, udpdata.data, 0, data_length);
                        }

                        switch (udpdata.command)
                        {
                            case UDPData.COMMAND_SEND:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_SEND", udpdata.socket_num));
                                    //送信ソケットを探索する
                                    TCPData tcpdata = FindTCPdata(udpdata.socket_num);
                                    if (tcpdata == null)
                                    {
                                        tcpdata = new TCPData();
                                        tcpdata.client_socket_num = udpdata.socket_num;
                                        tcpdata.socket = new CTCPClient(m_address, m_port);
                                        tcpdata.address = data.endPoint.Address;
                                        tcpdata.port = data.endPoint.Port;
                                        AddTCPData(tcpdata);
                                    }

                                    //データを接続先に送信する
                                    tcpdata.socket.Send(udpdata.data, data_length);
                                }
                                break;
                            case UDPData.COMMAND_ACK:
                                Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_ACK", udpdata.socket_num));
                                break;
                            case UDPData.COMMAND_CLOSE_SOCKET:
                                Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_CLOSE_SOCKET", udpdata.socket_num));
                                DeleteTCPdata(udpdata.socket_num);
                                break;
                            case UDPData.COMMAND_CONNECT:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_CONNECT", udpdata.socket_num));
                                    //送信ソケットを探索する
                                    TCPData tcpdata = FindTCPdata(udpdata.socket_num);
                                    if (tcpdata == null)
                                    {
                                        tcpdata = new TCPData();
                                        tcpdata.client_socket_num = udpdata.socket_num;
                                        tcpdata.socket = new CTCPClient(m_address, m_port);
                                        tcpdata.address = data.endPoint.Address;
                                        tcpdata.port = data.endPoint.Port;
                                        AddTCPData(tcpdata);
                                    }
                                }
                                break;
                            case UDPData.COMMAND_CONNECT_REQUEST:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_CONNECT_REQUEST", udpdata.socket_num));
                                    SendCommand(data.endPoint.Address, data.endPoint.Port, 0, UDPData.COMMAND_CONNECT_ANSWER);
                                }
                                break;
                            case UDPData.COMMAND_CONNECT_ANSWER:
                                Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP [{0}] => COMMAND_CONNECT_ANSWER", udpdata.socket_num));
                                {
                                    bool is_found = false;
                                    foreach(AckData ack_data in m_arAck)
                                    {
                                        if(ack_data.endpoint.Address.Equals(data.endPoint.Address) &&
                                            ack_data.endpoint.Port == data.endPoint.Port)
                                        {
                                            ack_data.received_time = DateTime.Now;
                                        }
                                    }

                                    if(is_found == false)
                                    {
                                        AckData ack_data = new AckData();
                                        ack_data.endpoint = data.endPoint;
                                        ack_data.received_time = DateTime.Now;
                                    }
                                }
                                break;
                        }
                    }
                    break;

            }
            Debug.WriteLine(string.Format("CTCPTunnelServer::EventHandler_UDP <<"));
            return true;
        }
    }
}
