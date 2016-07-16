using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;

using UDPTransfer;

namespace TCPtunnelViaUDP
{
    /// <summary>
    /// �g���l���T�[�o�[�N���X
    /// </summary>
    class CTCPTunnelClient
    {
        TcpListener m_TcpListener;
        bool m_isListening = false;

        //�g���l�����M�\�P�b�g
        CUDPTransfer m_UdpTransfer;
        IPAddress m_address_send;
        int m_port_send;

        //�g���l�����
        public bool m_isEstablish = false;

        //���M�\�P�b�g�ێ��z��
        ArrayList m_arSockets = new ArrayList();

        public CTCPTunnelClient(string _address, int _Port, Socket udp_socket, string send_to_address, int send_to_port)
        {
            //TCP�҂��󂯃\�P�b�g���쐬����
            System.Net.IPAddress ipAdd = System.Net.IPAddress.Parse(_address);
            m_TcpListener = new TcpListener(ipAdd, _Port);

            //UDP�\�P�b�g���쐬����
            m_UdpTransfer = new CUDPTransfer();
            m_UdpTransfer.AttachSocket(udp_socket);
            m_UdpTransfer.EventHndler = EventHandler_UDP;

            m_address_send = System.Net.IPAddress.Parse(send_to_address);
            m_port_send = send_to_port;
        }

        public bool Run()
        {
            //��M�ҋ@������
            m_TcpListener.Start();
            m_isListening = true;

            //UDP�ʐM�ҋ@���J�n����
            m_UdpTransfer.RunListen();
            m_UdpTransfer.RunCheckAckThread();

            //�ʐM�p���p�X���b�h�����s����
            var thread_connection_request = new Thread(() =>
            {
                DateTime timeConnectionRequest = DateTime.Now;
                while (m_isListening == true)
                {
                    TimeSpan ts = DateTime.Now.Subtract(timeConnectionRequest);
                    if (ts.TotalSeconds > 10)
                    {
                        timeConnectionRequest = DateTime.Now;
                        SendConnectionRequest();
                    }
                    Thread.Sleep(100);
                }
            });
            thread_connection_request.Start();

            //�ʐM����
            while (true)
            {
                try
                {
                    //�f�[�^����M����
                    TcpClient _LocalSocket = m_TcpListener.AcceptTcpClient();
                    NetworkStream _NetworkStreamLocal = _LocalSocket.GetStream();

                    //�\�P�b�g����ێ�����
                    IPEndPoint ipendpoint = (IPEndPoint)_LocalSocket.Client.RemoteEndPoint;

                    TCPData tcpdata = new TCPData();
                    tcpdata.socket = new CTCPClient(_LocalSocket);
                    tcpdata.client_socket_num = (uint)ipendpoint.Port;
                    tcpdata.address = ipendpoint.Address;
                    tcpdata.port = ipendpoint.Port;
                    AddTCPData(tcpdata);

                    //�f�[�^�𑗐M����
                    var thread = new Thread(() =>
                    {
                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread >>", Thread.CurrentThread.ManagedThreadId));

                        byte[] resBytes = new byte[59392];
                        int resSize = 0;

                        bool is_first = false;

                        while (m_isListening == true)
                        {
                            //�N���C�A���g���瑗��ꂽ�f�[�^����M����
                            try
                            {
                                _LocalSocket.Client.Poll(100, SelectMode.SelectRead);
                                
                                //�f�[�^�̈ꕔ����M����
                                if (_NetworkStreamLocal.CanRead == true && _LocalSocket.Available > 0)
                                {

                                    resSize = _NetworkStreamLocal.Read(resBytes, 0, resBytes.Length);

                                    Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread [{2}] Received size={1}", Thread.CurrentThread.ManagedThreadId, resSize, ipendpoint.Port));

                                    //��M�����f�[�^�𑗐M����
                                    byte[] send_data = new byte[resSize + 8];
                                    int pos = 0;
                                    Buffer.BlockCopy(BitConverter.GetBytes(ipendpoint.Port), 0, send_data, pos, 4);
                                    pos += 4;
                                    Buffer.BlockCopy(BitConverter.GetBytes(UDPData.COMMAND_SEND), 0, send_data, pos, 4);
                                    pos += 4;
                                    Buffer.BlockCopy(resBytes, 0, send_data, pos, resSize);

                                    Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread [{3}] Send to {1}:{2}", Thread.CurrentThread.ManagedThreadId, m_address_send.ToString(), m_port_send, ipendpoint.Port));
                                    uint id = m_UdpTransfer.SendData(m_address_send, m_port_send, send_data, 0);

                                    //�f�[�^�����M���ꂽ���Ƃ��m�F����
                                    int retry = 0;
                                    while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 500)
                                    {
                                        Thread.Sleep(10);
                                        ++retry;
                                    }
                                }
                                else if(is_first == false)
                                {
                                    
                                    //�ڑ����𑗂�
                                    byte[] send_data = new byte[8];
                                    int pos = 0;
                                    Buffer.BlockCopy(BitConverter.GetBytes(ipendpoint.Port), 0, send_data, pos, 4);
                                    pos += 4;
                                    Buffer.BlockCopy(BitConverter.GetBytes(UDPData.COMMAND_CONNECT), 0, send_data, pos, 4);
                                    pos += 4;

                                    Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread [{3}] Send Close to {1}:{2}", Thread.CurrentThread.ManagedThreadId, m_address_send.ToString(), m_port_send, ipendpoint.Port));
                                    uint id = m_UdpTransfer.SendData(m_address_send, m_port_send, send_data, 0);

                                    //�f�[�^�����M���ꂽ���Ƃ��m�F����
                                    int retry = 0;
                                    while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 500)
                                    {
                                        Thread.Sleep(10);
                                        ++retry;
                                    }

                                    is_first = true;
                                    continue;
                                }

                                //�ؒf���ꂽ�����m�F����
                                if (_NetworkStreamLocal.CanWrite == true)
                                {
                                    _NetworkStreamLocal.Write(resBytes, 0, 0);
                                    if (_LocalSocket.Client.Poll(0, SelectMode.SelectRead) == true && _LocalSocket.Available == 0)
                                    {
                                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread [{1}] Received local socket is closed", Thread.CurrentThread.ManagedThreadId, ipendpoint.Port));

                                        //�\�P�b�g���N���[�Y����
                                        DeleteTCPdata((uint)ipendpoint.Port);

                                        //�ؒf���𑗂�
                                        byte[] send_data = new byte[8];
                                        int pos = 0;
                                        Buffer.BlockCopy(BitConverter.GetBytes(ipendpoint.Port), 0, send_data, pos, 4);
                                        pos += 4;
                                        Buffer.BlockCopy(BitConverter.GetBytes(UDPData.COMMAND_CLOSE_SOCKET), 0, send_data, pos, 4);
                                        pos += 4;

                                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread [{3}] Send Close to {1}:{2}", Thread.CurrentThread.ManagedThreadId, m_address_send.ToString(), m_port_send, ipendpoint.Port));
                                        uint id = m_UdpTransfer.SendData(m_address_send, m_port_send, send_data, 0);

                                        //�f�[�^�����M���ꂽ���Ƃ��m�F����
                                        int retry = 0;
                                        while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 500)
                                        {
                                            Thread.Sleep(10);
                                            ++retry;
                                        }
                                        break;
                                    }
                                }
                            }

                            catch (Exception ex)
                            {
                                Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Error : {1}({2})", Thread.CurrentThread.ManagedThreadId, ex.Message, ex.StackTrace));
                                break;
                            }
                        }

                        //�\�P�b�g���N���[�Y����
                        DeleteTCPdata((uint)ipendpoint.Port);
                        _NetworkStreamLocal.Close();
                        //Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Close sockets", Thread.CurrentThread.ManagedThreadId));
                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::Run Thread <<", Thread.CurrentThread.ManagedThreadId));

                    });
                    thread.Start();
                }

                catch (Exception ex)
                {
                    Debug.WriteLine("CTCPTunnelClient::Run Error : " + ex.Message);
                    break;
                }
            }

            return true;
        }

        public bool Stop()
        {
            m_TcpListener.Stop();
            m_UdpTransfer.StopListen();
            m_isListening = false;
            m_isEstablish = false;

            return true;
        }

        private bool SendCommand(uint command)
        {
            Debug.WriteLine("CTCPTunnelClient::SendCommand cmd = " + command.ToString());
            //�ڑ����𑗂�
            byte[] send_data = new byte[8];
            int pos = 0;
            int data = 0;//dummy data
            Buffer.BlockCopy(BitConverter.GetBytes(data), 0, send_data, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(command), 0, send_data, pos, 4);
            pos += 4;

            uint id = m_UdpTransfer.SendData(m_address_send, m_port_send, send_data, 0);

            //�f�[�^�����M���ꂽ���Ƃ��m�F����
            int retry = 0;
            while (m_UdpTransfer.IsRecivedACK(id) == false && retry < 100)
            {
                Thread.Sleep(10);
                ++retry;
            }

            return m_UdpTransfer.IsRecivedACK(id);
        }

        public bool SendConnectionRequest()
        {
            Debug.WriteLine("CTCPTunnelClient::SendConnectionRequest");
            return SendCommand(UDPData.COMMAND_CONNECT_REQUEST);
        }

        public int Port
        {
            get
            {
                if (m_TcpListener == null) return 0;
                IPEndPoint endpoint = (IPEndPoint)m_TcpListener.Server.LocalEndPoint;
                return endpoint.Port;
            }
        }

        private TCPData FindTCPdata(uint socket_num)
        {
            foreach (TCPData tcpdata in m_arSockets)
            {
                if (tcpdata.client_socket_num == socket_num)
                {
                    return tcpdata;
                }

            }

            return null;
        }

        private void AddTCPData(TCPData data)
        {
            Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::AddTCPData socket_num = [{1}]", Thread.CurrentThread.ManagedThreadId, data.client_socket_num));
            m_arSockets.Add(data);
        }

        private void DeleteTCPdata(uint socket_num)
        {
            try
            {
                int count = m_arSockets.Count;
                for (int index = 0; index < count; ++index)
                {
                    TCPData tcpdata = (TCPData)m_arSockets[index];
                    if (tcpdata.client_socket_num == socket_num)
                    {
                        Debug.WriteLine(string.Format("[ID:{0}] CTCPTunnelClient::DeleteTCPdata socket_num = [{1}]", Thread.CurrentThread.ManagedThreadId, socket_num));

                        tcpdata.socket.Close();
                        m_arSockets.RemoveAt(index);
                        return;
                    }

                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine("DeleteTCPdata Error = " + ex.Message);
            }

        }

        //UDP��M�C�x���g�n���h��
        bool EventHandler_UDP(CUDPTransfer.EventID eventid, Object obj)
        {
            Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP >>"));

            switch (eventid)
            {
                case CUDPTransfer.EventID.DataRecived:
                    {
                        //��M�f�[�^
                        UDPData udpdata = new UDPData();

                        //�f�[�^�ϊ��p�o�b�t�@
                        byte[] byTmp = new byte[8];
                        int pos = 0;
                        int data_length;

                        CUDPTransfer.DataRecived data = (CUDPTransfer.DataRecived)obj;

                        //��M�f�[�^��UDPData�ɕϊ�����
                        Buffer.BlockCopy(data.payload, pos, byTmp, 0, 4);
                        pos += 4;
                        udpdata.socket_num = BitConverter.ToUInt32(byTmp, 0);

                        Buffer.BlockCopy(data.payload, pos, byTmp, 0, 4);
                        pos += 4;
                        udpdata.command = BitConverter.ToUInt32(byTmp, 0);

                        data_length = data.payload.Length - pos;
                        if (data_length > 0)
                        {
                            udpdata.data = new byte[data_length];
                            Buffer.BlockCopy(data.payload, pos, udpdata.data, 0, data_length);
                        }

                        switch (udpdata.command)
                        {
                            case UDPData.COMMAND_SEND:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => COMMAND_SEND", udpdata.socket_num));
                                    //���M�\�P�b�g��T������
                                    TCPData tcpdata = FindTCPdata(udpdata.socket_num);
                                    if (tcpdata != null)
                                    {
                                        //�f�[�^��ڑ���ɑ��M����
                                        tcpdata.socket.Send(udpdata.data, data_length);
                                    }
                                }
                                break;
                            case UDPData.COMMAND_ACK:
                                Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => COMMAND_ACK", udpdata.socket_num));
                                break;
                            case UDPData.COMMAND_CLOSE_SOCKET:
                                Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => COMMAND_CLOSE_SOCKET", udpdata.socket_num));
                                DeleteTCPdata(udpdata.socket_num);
                                break;
                            case UDPData.COMMAND_CONNECT_REQUEST:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => COMMAND_CONNECT_REQUEST", udpdata.socket_num));
                                    m_address_send = data.endPoint.Address;
                                    m_port_send = data.endPoint.Port;
                                    m_isEstablish = true;

                                    //�ڑ����𑗂�
                                    SendCommand(UDPData.COMMAND_CONNECT_ANSWER);

                                    //Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => address={1}, port={2}", udpdata.socket_num, data.endPoint.Address.ToString(), data.endPoint.Port));
                                }
                                break;
                            case UDPData.COMMAND_CONNECT_ANSWER:
                                {
                                    Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => COMMAND_CONNECT_ANSWER", udpdata.socket_num));
                                    m_address_send = data.endPoint.Address;
                                    m_port_send = data.endPoint.Port;
                                    m_isEstablish = true;
                                    //Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP [{0}] => address={1}, port={2}", udpdata.socket_num, data.endPoint.Address.ToString(), data.endPoint.Port));
                                }
                                break;
                        }
                    }
                    break;

            }
            Debug.WriteLine(string.Format("CTCPTunnelClient::EventHandler_UDP <<"));
            return true;
        }
    }
}
