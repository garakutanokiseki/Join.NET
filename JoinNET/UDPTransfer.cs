﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace JoinNET
{
    class UDPTransfer
    {
        //転送データ(データ保持)
        const int payload_size = 61440;
        public struct PacketPayload
        {
            //ヘッダ
            public uint header;
            //ID
            public int id;
            //送信先アドレス(送信時のみ設定される)
            public IPAddress ip;
            //送信先ポート(送信時のみ設定される)
            public int port;
            //シーケンシャル番号
            public uint sequenceNumber;
            //送信チケット
            public long sentTicks;
            //リトライ回数
            public uint retryCount;
            //データサイズ
            public int size;
            //ペイロードデータ
            public byte[] payload;
        }

        //ACKデータ(コールバック用構造体)
        public struct DataACK
        {
            //ID
            public int id;
            //リモートホスト
            public IPEndPoint endPoint;
        }

        //転送データ(コールバック用構造体)
        public struct DataRecived
        {
            //ID
            public int id;
            //リモートホスト
            public IPEndPoint endPoint;
            //転送データ
            public byte[] payload;
        }

        //定数
        //ヘッダ：データ転送
        const uint HeaderData = 0x0FFF0001;
        //ヘッダ：データ転送ACK
        const uint HeaderAck = 0xF0000001;

        //受信データバッファサイズ(UDP最大パケットサイズ)
        const int SizeData = 65536;
        //ACKデータサイズ
        const int SizeAck = (sizeof(int) + sizeof(int) + sizeof(int));

        //再送信回数
        const int RetrySendCount = 100;

        // 送信データ保持配列
        ArrayList arraySentPacket = new ArrayList();

        //送信ソケット
        Socket senderSocket;

        //受信用データバッファ
        byte[] bufferData = new byte[SizeData];

        //Ack確認スレッド終了フラグ
        bool closingSender = true;

        //送信シーケンス番号(パケットID)
        uint sequenceNumber = 1;

        //通信処理排他オブジェクト
        object syncObjectTransfer = new object();

        //受信待機フラグ
        bool m_bIsListen = false;

        //イベント通知用デリゲート
        public enum EventID
        {
            DataRecived,
            AckRecived,
            ErrorAckRecived,
            ErrorSocket,
        }
        public Func<EventID, Object, bool> EventHndler = null;

        //送信ソケットを作成する
        public void OpenSocket()
        {
            Debug.WriteLine("OpenSenderSocket >>");
            if (senderSocket != null)
            {
                Debug.WriteLine("OpenSenderSocket <<");
            }

            //送受信用ソケットを作成する
            senderSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            //受信待機をする
            EndPoint bindEndPoint = new IPEndPoint(IPAddress.Any, 0);
            senderSocket.Bind(bindEndPoint); // any free port

            /*
            ListenForData();

            //ACK確認スレッドを開始する
            RunCheckAckThread();
            */

            // 注意: このサンプルは簡易コードです。
            // データ構造のロックと効率的なスレッド同期、およびommitsオブジェクト処分を無視します

            Debug.WriteLine("OpenSenderSocket <<");
        }

        //ソケットをクローズする
        public void CloseSocket()
        {
            if (senderSocket == null)
            {
                return;
            }

            senderSocket.Close();
            senderSocket.Dispose();
        }

        //ソケットをセットする
        public void AttachSocket(Socket socket)
        {
            senderSocket = socket;
        }

        //ソケットを取得する
        public Socket GetSocket()
        {
            return senderSocket;
        }

        //ACK確認スレッドを開始する
        public void RunCheckAckThread()
        {
            // ACK待機スレッドを開始する
            if (closingSender == true)
            {
                closingSender = false;
                ThreadPool.QueueUserWorkItem(CheckPendingAcks, 0);
            }
        }

        //ACK確認スレッドを停止する
        public void StopCheckAckThread()
        {
            Debug.WriteLine("StopCheckAckThread >>");
            closingSender = true;
            Debug.WriteLine("StopCheckAckThread <<");
        }

        //ソケットの情報を取得する
        public IPEndPoint GetEndPount()
        {
            return (IPEndPoint)senderSocket.LocalEndPoint;
        }

        //中断しているACKを確認する
        public void CheckPendingAcks(object o)
        {
            Debug.WriteLine("CheckPendingAcks >>");
            Debug.WriteLine("CheckPendingAcks Checking for missing ACKs");
            int currentPosition = 0;
            for (; !closingSender; )
            {
                Thread.Sleep(10);
                ResendNextPacket(ref currentPosition, 10, DateTime.Now.Ticks - TimeSpan.TicksPerSecond / 10);
            }
            Debug.WriteLine("CheckPendingAcks <<");
        }

        //受信待機処理を開始する
        public void RunListen()
        {
            m_bIsListen = true;
            ListenForData();
        }

        //受信待機処理を呈する
        public void StopListen()
        {
            m_bIsListen = false;

        }

        //受信処理を開始する
        public void ListenForData()
        {
            Debug.WriteLine("ListenForData >>");
            if (m_bIsListen == false)
            {
                Debug.WriteLine("ListenForData << m_bIsListen is false;");
                return;
            }

            bool bIsSuccess = false;
            for(int i=0;i<5;++i){
                try
                {
                    Monitor.Enter(syncObjectTransfer);

                    EndPoint endPoint = new IPEndPoint(0, 0);
                    senderSocket.BeginReceiveFrom(bufferData, 0, SizeData, SocketFlags.None, ref endPoint, OnReceiveData, (object)this);
                    bIsSuccess = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ListenForData : Error " + ex.Message);
                }
                finally
                {
                    Monitor.Exit(syncObjectTransfer);
                }
                if (bIsSuccess == true) break;
            }
 
            Debug.WriteLine("ListenForData <<");
        }

        //受信した時に呼ばれるコールバック関数
        static void OnReceiveData(IAsyncResult result)
        {
            Debug.WriteLine("OnReceiveData >>");
            
            UDPTransfer uf = (UDPTransfer)result.AsyncState;
            try
            {
                Monitor.Enter(uf.syncObjectTransfer);

                EndPoint remoteEndPoint = new IPEndPoint(0, 0);
                int bytesRead = 0;
                bytesRead = uf.senderSocket.EndReceiveFrom(result, ref remoteEndPoint);

                //データヘッダを取得する
                byte[] byHeader = new byte[4];
                Buffer.BlockCopy(uf.bufferData, 0, byHeader, 0, 4);
                uint uheader = BitConverter.ToUInt32(byHeader, 0);

                //ヘッダにより処理を分ける
                switch (uheader)
                {
                    case HeaderAck:
                        Debug.WriteLine("OnReceiveData ACK");
                        uf.ProcessIncomingAck(uf.bufferData, bytesRead, (IPEndPoint)remoteEndPoint);
                        break;
                    case HeaderData:
                        Debug.WriteLine("OnReceiveData Data");
                        uf.ProcessIncomingData(uf.bufferData, bytesRead, (IPEndPoint)remoteEndPoint);
                        break;
                    default:
                        Debug.WriteLine("OnReceiveData Unknown");
                        break;
                }
               
            }

            catch (SocketException ex)
            {
                Debug.WriteLine(string.Format("OnReceiveData [SocketException:{0}]Error = {1}", ex.ErrorCode, ex.Message));
                uf.EventHndler(EventID.ErrorSocket, ex);
            }

            catch (ArgumentNullException ex)
            {
                Debug.WriteLine("OnReceiveData [ArgumentNullException]Error = " + ex.Message);
            }

            catch (ArgumentException ex)
            {
                Debug.WriteLine("OnReceiveData [ArgumentException]Error = " + ex.Message);

            }

            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine("OnReceiveData [ObjectDisposedException]Error = " + ex.Message);
            }

            catch (InvalidOperationException ex)
            {
                Debug.WriteLine("OnReceiveData [InvalidOperationException]Error = " + ex.Message);

            }

            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("OnReceiveData [{0}]Error = {1}", ex.GetType().ToString(), ex.Message));
            }

            finally
            {
                Monitor.Exit(uf.syncObjectTransfer);
            }

            //データの非同期受信を再開する
            uf.ListenForData();

            Debug.WriteLine("OnReceiveData <<");
        }

        /// <summary>
        /// データを送信する 
        /// </summary>
        /// <param name="data">転送データ(最大 61440 bytes)</param>
        /// <param name="id">アプリケーションが設定するデータID</param>
        public uint SendData(IPAddress ip, int port, byte[] data, int id)
        {
            return SendData(ip, port, data, id, 0, false, 0);
        }

        /// <summary>
        /// データを送信する(再送信無し)
        /// </summary>
        /// <param name="data">転送データ(最大 61440 bytes)</param>
        /// <param name="id">アプリケーションが設定するデータID</param>
        public uint SendDataNoRetry(IPAddress ip, int port, byte[] data, int id)
        {
            Debug.WriteLine("SendDataNoRetry >>");
#if false
            PacketPayload payload = new PacketPayload();

            //ヘッダを作成する
            payload.header = HeaderData;
            payload.id = id;
            payload.sequenceNumber = 0;

            //送信先データを保存する
            payload.ip = ip;
            payload.port = port;

            //送信データを作成する
            payload.payload = new byte[payload_size];
            payload.size = data.Length;
            System.Buffer.BlockCopy(data, 0, payload.payload, 0, payload.size);

            payload.sentTicks = DateTime.Now.Ticks;

            //構造体をバイト配列に変換する
            byte[] packetData = new byte[65536];
            int pos = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.header), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.id), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.sequenceNumber), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.sentTicks), 0, packetData, pos, 8);
            pos += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.retryCount), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.size), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(payload.payload, 0, packetData, pos, payload.size);
            pos += payload.size;

            //データを送信する
            Debug.WriteLine("SendDataNoRetry length=" + pos.ToString());
            EndPoint senderEndPoint = new IPEndPoint(payload.ip, payload.port);
            senderSocket.SendTo(packetData, pos, SocketFlags.None, senderEndPoint);
            Debug.WriteLine("SendDataNoRetry <<");

            return 0;
#endif
            uint sequenceNumber = SendData(ip, port, data, id, 0, false, RetrySendCount);
            Debug.WriteLine("SendDataNoRetry <<");
            return sequenceNumber;
        }

        /// <summary>
        /// データを送信する 
        /// </summary>
        /// <param name="ip">送信先アドレス</param>
        /// <param name="port">送信先ポート</param>
        /// <param name="data">転送データ(最大 61440 bytes)</param>
        /// <param name="id">アプリケーションが設定するデータID</param>
        /// <param name="retry_sequenceNumber">このクラスが使用するデータ転送ID</param>
        /// <param name="retry">再転送かの確認フラグ</param>
        /// <param name="retry_count">再転送済み回数の初期値を設定する</param>
        private uint SendData(IPAddress ip, int port, byte[] data, int id, uint retry_sequenceNumber, bool retry, uint retry_count)
        {
            //Debug.WriteLine("SendData >>");
            PacketPayload payload = new PacketPayload();

            //保持データに指定sequenceNumberのデータがあるかを確認する
            bool is_exist = false;
            for(int i=0; i < arraySentPacket.Count; ++i)
            {
                if (((PacketPayload)arraySentPacket[i]).sequenceNumber == retry_sequenceNumber)
                {
                    payload = (PacketPayload)arraySentPacket[i];
                    payload.sentTicks = DateTime.Now.Ticks;
                    if (retry == false)
                    {
                        payload.retryCount = 0;
                    }
                    else
                    {
                        ++payload.retryCount;
                    }
                    //Debug.WriteLine(string.Format("SendData retry_sequenceNumber={0}, retry_count={1}", retry_sequenceNumber, payload.retryCount));
                    arraySentPacket.RemoveAt(i);
                    arraySentPacket.Add(payload);
                    is_exist = true;
                    break;
                }
            }

            if (is_exist == false && data == null)
            {
                //Debug.WriteLine("SendData data is none");
                //Debug.WriteLine("SendData <<");
                return 0;
            }

            if (is_exist == false)
            {
                payload = new PacketPayload();
                //シーケンスナンバーを修正する
                try
                {
                    sequenceNumber++;
                }
                catch(Exception ex){
                    Debug.WriteLine("SendData Error = " + ex.Message);
                    sequenceNumber = 1;
                }

                //ヘッダを作成する
                payload.header = HeaderData;
                payload.id = id;
                payload.sequenceNumber = sequenceNumber;
                payload.retryCount = retry_count;

                //送信先データを保存する
                payload.ip = ip;
                payload.port = port;

                //送信データを作成する
                payload.payload = new byte[payload_size];
                payload.size = data.Length;
                System.Buffer.BlockCopy(data, 0, payload.payload, 0, payload.size);

                payload.sentTicks = DateTime.Now.Ticks;

                //データを送信リストに追加する
                arraySentPacket.Add(payload);
            }

            //構造体をバイト配列に変換する
            byte[] packetData = new byte[65536];
            int pos = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.header), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.id), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.sequenceNumber), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.sentTicks), 0, packetData, pos, 8);
            pos += 8;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.retryCount), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(payload.size), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(payload.payload, 0, packetData, pos, payload.size);
            pos += payload.size;

            //データを送信する
            EndPoint senderEndPoint = new IPEndPoint(payload.ip, payload.port);
            try
            {
                senderSocket.SendTo(packetData, pos, SocketFlags.None, senderEndPoint);
            }
            catch(Exception ex){
                Debug.WriteLine("SendData Error : " + ex.Message);

            }
            //Debug.WriteLine("SendData <<");

            return sequenceNumber;
        }

        // 受信したACKを処理する
        void ProcessIncomingAck(byte[] packetData, int size, IPEndPoint remoteEndPoint)
        {
            Debug.WriteLine("ProcessIncomingAck >>");
            //int pos = 0;
            uint header = 0;
            int id = 0;
            int sequenceNumber = 0;

            // error packet is ignored
            if (size != SizeAck)
            {
                Debug.WriteLine(string.Format("ProcessIncomingAck Error size id not SizeAck (size = {0})", size));
                return;
            }

            byte[] byTmp = new byte[4];
            Buffer.BlockCopy(packetData, 0, byTmp, 0, 4);
            header = BitConverter.ToUInt32(byTmp, 0);

            // bad header
            if (header != HeaderAck)
            {
                Debug.WriteLine(string.Format("ProcessIncomingAck Error header is not Ack header (header = 0x{0:X8})", header));
                return; 
            }

            Buffer.BlockCopy(packetData, 4, byTmp, 0, 4);
            sequenceNumber = BitConverter.ToInt32(byTmp, 0);

            Buffer.BlockCopy(packetData, 8, byTmp, 0, 4);
            id = BitConverter.ToInt32(byTmp, 0);

            //コールバックを転送する
            if (EventHndler != null)
            {
                DataACK ack = new DataACK();
                ack.id = id;
                ack.endPoint = remoteEndPoint;
                //ack.ip = remoteEndPoint.Address.ToString();
                //ack.port = remoteEndPoint.Port;
                EventHndler(EventID.AckRecived, ack);
            }

            // 'needing ACK'リストからもっとも新しい情報を削除する
            bool is_exist = false;
            for (int i = 0; i < arraySentPacket.Count; ++i)
            {
                PacketPayload payload = (PacketPayload)arraySentPacket[i];
                if (payload.sequenceNumber != sequenceNumber) continue;
                {
                    arraySentPacket.RemoveAt(i);
                    is_exist = true;
                    Debug.WriteLine(string.Format("ProcessIncomingAck Received current ACK on {0} {1}", id, sequenceNumber));
                    Debug.WriteLine(string.Format("ProcessIncomingAck remain no ack data = {0}", arraySentPacket.Count));
                    break;
                }
            }
            if (is_exist == false)
            {
                Debug.WriteLine(string.Format("ProcessIncomingAck Received outdated ACK on {0} {1}", id, sequenceNumber));

            }
            Debug.WriteLine("ProcessIncomingAck <<");
        }

        // 指定したsequenceNumberのパケットがACKを受信したかを確認する
        public bool IsRecivedACK(uint sequenceNumber)
        {
            for (int i = 0; i < arraySentPacket.Count; ++i)
            {
                PacketPayload payload = (PacketPayload)arraySentPacket[i];
                if (payload.sequenceNumber == sequenceNumber)
                {
                    return false;
                }
            }

            return true;
        }

        // データを再送信する
        void ResendData(uint sequenceNumber)
        {
            //Debug.WriteLine("ResendData >>");
            //指定したデータを再送信する
            SendData(null, 0, null, 0, sequenceNumber, true, 0);

            //Debug.WriteLine(string.Format("ResendData sequenceNumber={0}", sequenceNumber));
            //Debug.WriteLine("ResendData <<");
        }

        void ResendNextPacket(ref int currentPosition, int maxPacketsToSend, long olderThan)
        {
            //Debug.WriteLine("ResendNextPacket >>");
            int packetsSent = 0;
            int newPosition = currentPosition + 1;

            if (newPosition >= arraySentPacket.Count)
            {
                //Debug.WriteLine("ResendNextPacket RESET packet position");
                newPosition = 0;
            }

            for (; newPosition < arraySentPacket.Count && packetsSent < maxPacketsToSend; ++newPosition)
            {
                //Debug.WriteLine(string.Format("ResendNextPacket : newPosition = {0}", newPosition));
                PacketPayload payload = (PacketPayload)arraySentPacket[newPosition];

                if ((payload.sequenceNumber != 0) &&  payload.sentTicks < olderThan)
                {
                    if (payload.retryCount > RetrySendCount)
                    {
                        //Debug.WriteLine(string.Format("ResendNextPacket : {0} Too many retries, should fail connection", newPosition));
                        if (EventHndler != null)
                        {
                            EventHndler(EventID.ErrorAckRecived, payload);
                        }
                        arraySentPacket.RemoveAt(newPosition);
                        --newPosition;
                    }
                    else
                    {
                        Debug.WriteLine(string.Format("ResendNextPacket : resend sequenceNumber = {0}", payload.sequenceNumber));
                        ResendData(payload.sequenceNumber);
                        packetsSent++;
                    }
                }
            }
            currentPosition = newPosition;
            //Debug.WriteLine("ResendNextPacket <<");
        }

        /// <summary>
        /// 未送信のデータ数を取得する
        /// </summary>
        /// <returns></returns>
        public int GetRemainSendDataCount()
        {
            return arraySentPacket.Count;
        }

        //////////////////////////////////////////////////////////////////////
        //データ受信処理用
        void SendAck(int id, uint sequenceNumber, EndPoint remoteEndPoint)
        {
            Debug.WriteLine("SendAck >>");
            int pos = 0;
            byte[] packetData = new byte[(sizeof(int) + sizeof(int) + sizeof(int))];
            Buffer.BlockCopy(BitConverter.GetBytes(HeaderAck), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(sequenceNumber), 0, packetData, pos, 4);
            pos += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(id), 0, packetData, pos, 4);
            pos += 4;
            // send
            senderSocket.SendTo(packetData, SizeAck, SocketFlags.None, remoteEndPoint);
            Debug.WriteLine("SendAck Sending Ack {0} {1}", id, sequenceNumber);
            Debug.WriteLine("SendAck <<");
        }

        //データ受信処理
        void ProcessIncomingData(byte[] packetData, int size, IPEndPoint remoteEndPoint)
        {
            Debug.WriteLine("ProcessIncomingData >>");
            PacketPayload payload;
            //int sequenceNumber = 0;
            int pos = 0;
            //if (size != SizeData) return; // error packet is ignored
            byte [] byTmp = new byte[8];
            
            //ヘッダを変換する
            Buffer.BlockCopy(packetData, pos, byTmp, 0, 4);
            pos += 4;
            payload.header = BitConverter.ToUInt32(byTmp, 0);

            Buffer.BlockCopy(packetData, pos, byTmp, 0, 4);
            pos += 4;
            payload.id = BitConverter.ToInt32(byTmp, 0);

            Buffer.BlockCopy(packetData, pos, byTmp, 0, 4);
            pos += 4;
            payload.sequenceNumber = BitConverter.ToUInt32(byTmp, 0);

            Buffer.BlockCopy(packetData, pos, byTmp, 0, 8);
            pos += 8;
            payload.sentTicks = BitConverter.ToInt64(byTmp, 0);

            Buffer.BlockCopy(packetData, pos, byTmp, 0, 4);
            pos += 4;
            payload.retryCount = BitConverter.ToUInt32(byTmp, 0);

            Buffer.BlockCopy(packetData, pos, byTmp, 0, 4);
            pos += 4;
            payload.size = BitConverter.ToInt32(byTmp, 0);

            payload.payload = new byte[payload.size];
            Buffer.BlockCopy(packetData, pos, payload.payload, 0, payload.size);

            Debug.WriteLine(string.Format("ProcessIncomingData id={0}, sequenceNumber={1}", payload.id, payload.sequenceNumber));

            if (payload.header != HeaderData)
            {
                Debug.WriteLine("ProcessIncomingData : Error bad header");
                return;
            }

            // validate dup
            SendAck(payload.id, payload.sequenceNumber, remoteEndPoint);

            //受信通知を行う
            if (EventHndler != null)
            {
                DataRecived data = new DataRecived();
                data.id = payload.id;
                data.endPoint = (IPEndPoint)remoteEndPoint;
                //data.endPoint = new IPEndPoint(remoteEndPoint.Address, remoteEndPoint.Port);
                data.payload = payload.payload;
                EventHndler(EventID.DataRecived, data);
            }

            Debug.WriteLine("ProcessIncomingData <<");
        }
    }
}
