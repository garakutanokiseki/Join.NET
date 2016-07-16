using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading;

namespace JoinNET
{
    //ファイル情報
    class TransferFileInfo
    {
        //データID(転送ID)
        public byte[] guid = new byte[32];

        //ファイルサイズ
        public long length;

        //ファイル更新日
        public long date;

        //ファイル名(最大1024文字)
        public string name;

        //転送元のファイル名
        public string original_name;

        //受信フラグ
        public bool[] bRecived;
    }

    //転送バイナリ情報
    class TransferBinaryInfo : TransferFileInfo
    {
        public byte[] data;
    }

    //転送データ
    class TransferData
    {
        public const int PACKET_SIZE = 59392;

        //データID(転送ID)
        public byte[] guid = new byte[32];

        //データインデックス
        public int index;

        //データサイズ
        public int data_size;

        //データ
        public byte[] data = new byte[PACKET_SIZE];
    }

    //転送データ状況
    public class TransferProgress
    {
        public string       name;       //転送データ名
        public string original_name;//転送元のファイル名
        public IPEndPoint endpoint;   //送信元エンドポイント
        public double       progress;   //進捗率
    }

    //送信クラス
    class UDPFileTransfer
    {
        //通信データ定義
        public enum DataID
        {
            ConnectionRequest   = 1,//接続要求
            Message             = 2,//文字列
            FileInfo            = 3,//ファイル情報
            File                = 4,//ファイル転送
            BinaryInfo,             //送信データ情報
            Binary,                 //データ送信
        }

        //イベント定義
        public enum EventID
        {

            //受信(データ)イベント
            Recived_ConnectionRequest = 1,
            Recived_Message,
            Recived_File,
            Recived_Binary,

            //受信(ACK)イベント
            Recived_Ack_ConnectionRequest = 100,

            //送信(データ)イベント
            SendBinary_Start = 200,
            SendBinary_Progress,
            SendBinary_Finish,
            SendBinary_Cancel,

            //送信(ファイル)イベント
            SendFile_Start = 200,
            SendFile_Progress,
            SendFile_Finish,
            SendFile_Cancel,

            //受信(ファイル)イベント
            ReciveFile_Start = 300,
            ReciveFile_Progress,
            ReciveFile_Finish,
            ReciveFile_Cancel,

            //エラー
            Error_CantReciveAck = 1000,
            Error_socket,
        }

        //通信用ソケット
        UDPTransfer m_Socket = new UDPTransfer();

        //キャンセルフラグ
        public bool IsCancel = false;

        //最大ビットレート(bytes / milsecond)
        public int MaxBitrate = 2 * 1024 * 1024;

        //受信ファイル管理用配列
        ArrayList m_ArrayRecieveFileInfo = new ArrayList();

        //受信データ管理用配列
        ArrayList m_ArrayRecieveBinaryInfo = new ArrayList();

        //イベントハンドラ
        public Func<EventID, Object, Object, bool> EventHndler = null;

        //ソケットを初期化する
        public bool Init()
        {
            m_Socket.OpenSocket();

            m_Socket.EventHndler = EventTransfer;

            return true;
        }

        //ソケットを開放する
        public bool Release()
        {
            StopListen();

            m_Socket.CloseSocket();

            return true;
        }

        //受信待機処理を開始する
        public void RunListen()
        {
            m_Socket.RunListen();
            m_Socket.RunCheckAckThread();
        }

        //受信待機処理を呈する
        public void StopListen()
        {
            m_Socket.StopListen();
            m_Socket.StopCheckAckThread();
        }

        //ポート番号を取得する
        public int GetPort()
        {
            return m_Socket.GetEndPount().Port;
        }

        //IPアドレスを取得する
        public IPAddress GetAddress()
        {
            return m_Socket.GetEndPount().Address;
        }

        //ソケットを取得する
        public Socket GetSocket()
        {
            return m_Socket.GetSocket();
        }

        /// <summary>
        /// 未送信のデータ数を取得する
        /// </summary>
        /// <returns></returns>
        public int GetRemainSendDataCount()
        {
            return m_Socket.GetRemainSendDataCount();
        }

        // 指定したsequenceNumberのパケットがACKを受信したかを確認する
        public bool IsRecivedACK(uint sequenceNumber)
        {
            return m_Socket.IsRecivedACK(sequenceNumber);
        }

        //ファイルを送信する
        public bool Send(string file, IPAddress ip, int port)
        {
            //ファイルを開く
            //GUIDを作成する
            //最大パケット数読み込む
            //データを送信する

            if (EventHndler != null)
            {
                EventHndler(EventID.SendFile_Start, null, null);
            }


            FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
            int fileSize = (int)fs.Length; // ファイルのサイズ
            byte[] guid;// = new byte[32];

            //ファイル情報を送信する
            {
                DateTime date = File.GetLastWriteTime(file);

                //guidを作成する
                guid = Guid.NewGuid().ToByteArray();

                //送信データを作成する
                byte[] packetData = new byte[50000];
                int pos = 0;
                Buffer.BlockCopy(guid, 0, packetData, pos, 16);
                pos += 32;
                Buffer.BlockCopy(BitConverter.GetBytes(fs.Length), 0, packetData, pos, 8);
                pos += 8;
                Buffer.BlockCopy(BitConverter.GetBytes(date.ToBinary()), 0, packetData, pos, 8);
                pos += 8;
                //送信文字列をバイナリに変換する
                Byte[] massage_byte = System.Text.Encoding.GetEncoding("utf-8").GetBytes(file);
                int length;
                if (massage_byte.Length > 2048)
                {
                    length = 2048;
                }
                else
                {
                    length = massage_byte.Length;
                }
                Buffer.BlockCopy(BitConverter.GetBytes(length), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(massage_byte, 0, packetData, pos, length);

                //データを送信する
                uint sequenceNumber = m_Socket.SendData(ip, port, packetData, (int)DataID.FileInfo);

                //ACKを得るまで待機する
                int retry = 0;
                for (retry = 0; retry < 100; ++retry)
                {
                    if (m_Socket.IsRecivedACK(sequenceNumber) == true) break;
                    Thread.Sleep(10);
                }
                if (retry >= 100)
                {
                    Debug.WriteLine("UDPFileTransfer::Send Can't get ACK for file information");
                    return false;
                }
            }

            //ファイル本体を送信する
            int readSize; // Readメソッドで読み込んだバイト数
            int remain = fileSize; // 読み込むべき残りのバイト数

            TransferData transferData = new TransferData();
            Buffer.BlockCopy(guid, 0, transferData.guid, 0, 16);
            transferData.index = 0;

            //待機時間を計算する
            int sleepTime = 1000 / (MaxBitrate / TransferData.PACKET_SIZE);

            while (remain > 0 && IsCancel == false)
            {
                // バッファサイズずつ読み込む
                readSize = fs.Read(transferData.data, 0, Math.Min(TransferData.PACKET_SIZE, remain));

                //データ情報を設定する
                //transferData.index;
                transferData.data_size = readSize;

                //残りデータ数を計算する
                remain -= readSize;

                //構造体をバイト配列に変換する
                byte[] packetData = new byte[44 + TransferData.PACKET_SIZE];
                int pos = 0;
                Buffer.BlockCopy(BitConverter.GetBytes(TransferData.PACKET_SIZE), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(transferData.guid, 0, packetData, pos, 32);
                pos += 32;
                Buffer.BlockCopy(BitConverter.GetBytes(transferData.index), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(BitConverter.GetBytes(transferData.data_size), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(transferData.data, 0, packetData, pos, TransferData.PACKET_SIZE);
                pos += TransferData.PACKET_SIZE;

                //データを転送する
                uint sequenceNumber = m_Socket.SendData(ip, port, packetData, (int)DataID.File);

                //状態を通知する
                if (EventHndler != null)
                {
                    EventHndler(EventID.SendFile_Progress, ((double)fileSize / (double)readSize) * 100, null);
                }

                //ACKが返る/受信エラーで消去されるまで待機する
                while(true)
                {
                    if (m_Socket.IsRecivedACK(sequenceNumber) == true) break;
                    Thread.Sleep(10);
                }

                //転送インデックスを追加する
                ++transferData.index;
            }
            fs.Dispose();

            if (EventHndler != null)
            {
                if (IsCancel == false)
                    EventHndler(EventID.SendFile_Finish, null, null);
                else
                    EventHndler(EventID.SendFile_Cancel, null, null);
            }

            
            return true;
        }

        //byte配列を送信する
        public bool Send(byte [] data, IPAddress ip, int port)
        {
            Debug.WriteLine("UDPFileTransfer::Send >>");
            //ファイルを開く
            //GUIDを作成する
            //最大パケット数読み込む
            //データを送信する

            if (EventHndler != null)
            {
                EventHndler(EventID.SendBinary_Start, null, null);
            }


            long fileSize = data.Count(); // ファイルのサイズ
            byte[] guid;// = new byte[32];

            //ファイル情報を送信する
            {
                DateTime date = DateTime.Now;

                //guidを作成する
                guid = Guid.NewGuid().ToByteArray();

                //送信データを作成する
                byte[] packetData = new byte[50000];
                int pos = 0;
                Buffer.BlockCopy(guid, 0, packetData, pos, 16);
                pos += 32;
                Buffer.BlockCopy(BitConverter.GetBytes(fileSize), 0, packetData, pos, 8);
                pos += 8;
                Buffer.BlockCopy(BitConverter.GetBytes(date.ToBinary()), 0, packetData, pos, 8);
                pos += 8;
                //送信文字列をバイナリに変換する
                Byte[] massage_byte = System.Text.Encoding.GetEncoding("utf-8").GetBytes("data");
                int length;
                if (massage_byte.Length > 2048)
                {
                    length = 2048;
                }
                else
                {
                    length = massage_byte.Length;
                }
                Buffer.BlockCopy(BitConverter.GetBytes(length), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(massage_byte, 0, packetData, pos, length);

                //データを送信する
                uint sequenceNumber = m_Socket.SendData(ip, port, packetData, (int)DataID.BinaryInfo);

                //ACKを得るまで待機する
                int retry = 0;
                for (retry = 0; retry < 100; ++retry)
                {
                    if (m_Socket.IsRecivedACK(sequenceNumber) == true) break;
                    Thread.Sleep(10);
                }
                if (retry >= 100)
                {
                    Debug.WriteLine("UDPFileTransfer::Send << Can't get ACK for file information");
                    return false;
                }
            }

            //ファイル本体を送信する
            int readSize; // Readメソッドで読み込んだバイト数
            long remain = fileSize; // 読み込むべき残りのバイト数

            TransferData transferData = new TransferData();
            Buffer.BlockCopy(guid, 0, transferData.guid, 0, 16);
            transferData.index = 0;

            //待機時間を計算する
            int sleepTime = 1000 / (MaxBitrate / TransferData.PACKET_SIZE);

            while (remain > 0 && IsCancel == false)
            {
                // バッファサイズずつ読み込む
                //readSize = fs.Read(transferData.data, 0, Math.Min(TransferData.PACKET_SIZE, remain));
                if (remain >= TransferData.PACKET_SIZE)
                {
                    readSize = TransferData.PACKET_SIZE;
                }
                else
                {
                    readSize = (int)remain;
                }
                Buffer.BlockCopy(data, (int)(fileSize - remain), transferData.data, 0, readSize);

                //データ情報を設定する
                //transferData.index;
                transferData.data_size = readSize;

                //残りデータ数を計算する
                remain -= readSize;

                //構造体をバイト配列に変換する
                byte[] packetData = new byte[44 + TransferData.PACKET_SIZE];
                int pos = 0;
                Buffer.BlockCopy(BitConverter.GetBytes(TransferData.PACKET_SIZE), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(transferData.guid, 0, packetData, pos, 32);
                pos += 32;
                Buffer.BlockCopy(BitConverter.GetBytes(transferData.index), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(BitConverter.GetBytes(transferData.data_size), 0, packetData, pos, 4);
                pos += 4;
                Buffer.BlockCopy(transferData.data, 0, packetData, pos, TransferData.PACKET_SIZE);
                pos += TransferData.PACKET_SIZE;

                //データを転送する
                uint sequenceNumber = m_Socket.SendData(ip, port, packetData, (int)DataID.Binary);

                //状態を通知する
                if (EventHndler != null)
                {
                    EventHndler(EventID.SendBinary_Progress, ((double)fileSize / (double)readSize) * 100, null);
                }

                //ACKが返る/受信エラーで消去されるまで待機する
                while(true)
                {
                    if (m_Socket.IsRecivedACK(sequenceNumber) == true) break;
                    Thread.Sleep(10);
                }

                //転送インデックスを追加する
                ++transferData.index;
            }

            if (EventHndler != null)
            {
                if (IsCancel == false)
                    EventHndler(EventID.SendBinary_Finish, null, null);
                else
                    EventHndler(EventID.SendBinary_Cancel, null, null);
            }

            Debug.WriteLine("UDPFileTransfer::Send <<");

            return true;
        }

        //接続要求を送る
        public uint SendConnectionRequest(string name, string source_ip, int source_port, string target_ip, int target_port)
        {
            try
            {
                byte[] data = new byte[1024];
                data[0] = 1;

                //(送信先)IPアドレスをバイト列に変換する
                IPAddress ipaddress = IPAddress.Parse(source_ip);
                Buffer.BlockCopy(ipaddress.GetAddressBytes(), 0, data, 1, 4);

                //(送信先)ポート番号をバイト列に変換する
                Buffer.BlockCopy(BitConverter.GetBytes(source_port), 0, data, 5, 2);

                //(送信元)IPアドレスをバイト列に変換する
                ipaddress = IPAddress.Parse(target_ip);
                Buffer.BlockCopy(ipaddress.GetAddressBytes(), 0, data, 7, 4);

                //(送信元)ポート番号をバイト列に変換する
                Buffer.BlockCopy(BitConverter.GetBytes(target_port), 0, data, 11, 2);

                //送信文字列をバイナリに変換する
                byte[] massage_byte = System.Text.Encoding.GetEncoding("utf-8").GetBytes(name);
                int length;
                if (massage_byte.Length > 1004)
                {
                    length = 1004;
                }
                else
                {
                    length = massage_byte.Length;
                }
                Buffer.BlockCopy(BitConverter.GetBytes(length), 0, data, 13, 4);
                Buffer.BlockCopy(massage_byte, 0, data, 19, length);

                //送信アドレスを作成する
                IPAddress address = IPAddress.Parse(target_ip);

                //データを送信する
                return m_Socket.SendDataNoRetry(address, target_port, data, (int)DataID.ConnectionRequest);
            }

            catch(Exception ex){
                Debug.WriteLine("SendConnectionRequest Error => " + ex.Message);
            }

            return 0;
        }

        //文字列を送る
        public uint SendString(string source_ip, int source_port, string target_ip, int target_port, string message)
        {
            byte[] data = new byte[1024];
            data[0] = 3;

            //IPアドレスをバイト列に変換する
            IPAddress ipaddress = IPAddress.Parse(source_ip);
            Buffer.BlockCopy(ipaddress.GetAddressBytes(), 0, data, 1, 4);

            //ポート番号をバイト列に変換する
            Buffer.BlockCopy(BitConverter.GetBytes(source_port), 0, data, 5, 2);

            //(送信元)IPアドレスをバイト列に変換する
            ipaddress = IPAddress.Parse(target_ip);
            Buffer.BlockCopy(ipaddress.GetAddressBytes(), 0, data, 7, 4);

            //(送信元)ポート番号をバイト列に変換する
            Buffer.BlockCopy(BitConverter.GetBytes(target_port), 0, data, 11, 2);

            //送信文字列をバイナリに変換する
            Byte[] massage_byte = System.Text.Encoding.GetEncoding("utf-8").GetBytes(message);
            int length;
            if (massage_byte.Length > 1008)
            {
                length = 1008;
            }
            else
            {
                length = massage_byte.Length;
            }
            Buffer.BlockCopy(BitConverter.GetBytes(length), 0, data, 16, 4);
            Buffer.BlockCopy(massage_byte, 0, data, 20, length);

            IPAddress address = IPAddress.Parse(target_ip);

            return m_Socket.SendData(address, target_port, data, (int)DataID.Message);
        }

        /// <summary>
        /// GUIDを使用して受信ファイル情報を取得する
        /// </summary>
        /// <returns></returns>
        int RecieveFileInfo_FindData(byte [] guid)
        {
            for (int i = 0; i < m_ArrayRecieveFileInfo.Count; ++i)
            {
                TransferFileInfo info = (TransferFileInfo)m_ArrayRecieveFileInfo[i];
                if (info.guid.SequenceEqual(guid) == true) return i;
            }

            return -1;
        }

        int RecieveBinaryInfo_FindData(byte[] guid)
        {
            for (int i = 0; i < m_ArrayRecieveBinaryInfo.Count; ++i)
            {
                TransferBinaryInfo info = (TransferBinaryInfo)m_ArrayRecieveBinaryInfo[i];
                if (info.guid.SequenceEqual(guid) == true) return i;
            }

            return -1;
        }

        //データ受信イベントハンドラ
        private bool EventTransfer(UDPTransfer.EventID eventid, Object obj)
        {
            Debug.WriteLine("EventTransfer >>");
            switch (eventid)
            {
                case UDPTransfer.EventID.DataRecived:
                    OnEventDataRecived((UDPTransfer.DataRecived)obj);
                    break;
                case UDPTransfer.EventID.AckRecived:
                    OnEventAckRecived((UDPTransfer.DataACK)obj);
                    break;
                case UDPTransfer.EventID.ErrorAckRecived:
                    OnEventErrorAckRecived((UDPTransfer.PacketPayload)obj);
                    break;
                case UDPTransfer.EventID.ErrorSocket:
                    OnEventErrorSocket((SocketException)obj);
                    break;
            }
            Debug.WriteLine("EventTransfer <<");
            return true;
        }

        //Ackハンドラ
        private void OnEventAckRecived(UDPTransfer.DataACK data)
        {
            Debug.WriteLine("OnEventAckRecived >>");
            Debug.WriteLine(string.Format("OnEventAckRecived : DataID={0} from {1}:{2}", data.id, data.endPoint.Address.ToString(), data.endPoint.Port));
            switch ((DataID)data.id)
            {
                case DataID.ConnectionRequest:
                    if (EventHndler != null)
                    {
                        EventHndler(EventID.Recived_Ack_ConnectionRequest, null, data.endPoint);
                    }
                    break;
            }
            Debug.WriteLine("OnEventAckRecived <<");
        }

        //Ack受信エラーハンドラ
        private void OnEventErrorAckRecived(UDPTransfer.PacketPayload payload)
        {
            if (EventHndler != null)
            {
                EventHndler(EventID.Error_CantReciveAck, payload.id, payload.sequenceNumber);
            }

        }

        //ソケットエラーハンドラ
        private void OnEventErrorSocket(SocketException exception){
            if (EventHndler != null)
            {
                EventHndler(EventID.Error_socket, exception, null);
            }
        }

        //Data受信ハンドラ
        private void OnEventDataRecived(UDPTransfer.DataRecived data)
        {
            Debug.WriteLine("OnEventDataRecived >>");
            Debug.WriteLine(string.Format("OnEventDataRecived : DataID={0} from {1}:{2}", data.id, data.endPoint.Address.ToString(), data.endPoint.Port));
            switch ((DataID)data.id)
            {
                case DataID.ConnectionRequest:
                    OnEventDataRecived_ConnectionRequest(data);
                    break;
                case DataID.Message:
                    OnEventDataRecived_Message(data);
                    break;
                case DataID.FileInfo:
                    OnEventDataRecived_FileInfo(data);
                    break;
                case DataID.File:
                    OnEventDataRecived_File(data);
                    break;
                case DataID.BinaryInfo:
                    OnEventDataRecived_BinaryInfo(data);
                    break;
                case DataID.Binary:
                    OnEventDataRecived_Binary(data);
                    break;
            }
            Debug.WriteLine("OnEventDataRecived <<");
        }

        //接続要求受信ハンドラ
        private void OnEventDataRecived_ConnectionRequest(UDPTransfer.DataRecived data)
        {
            //送信元ユーザーIDを取得する
            byte[] byTmp = new byte[4];
            Buffer.BlockCopy(data.payload, 13, byTmp, 0, 4);
            int length = BitConverter.ToInt32(byTmp, 0);
            string returnData = System.Text.Encoding.UTF8.GetString(data.payload, 19, length);

            if (EventHndler != null)
            {
                EventHndler(EventID.Recived_ConnectionRequest, returnData, data.endPoint);
            }
        }

        //メッセージ受信ハンドラ
        private void OnEventDataRecived_Message(UDPTransfer.DataRecived data)
        {
            byte[] byTmp = new byte[4];
            Buffer.BlockCopy(data.payload, 16, byTmp, 0, 4);
            int length = BitConverter.ToInt32(byTmp, 0);
            string returnData = System.Text.Encoding.UTF8.GetString(data.payload, 20, length);

            if (EventHndler != null)
            {
                EventHndler(EventID.Recived_Message, returnData, data.endPoint);
            }
        }

        //ファイル情報受信ハンドラ
        private void OnEventDataRecived_FileInfo(UDPTransfer.DataRecived data)
        {
            Debug.WriteLine(string.Format("OnEventDataRecived_FileInfo >>"));
            //データを読み込む
            byte[] byTmp = new byte[8];
            TransferFileInfo transferFileInfo = new TransferFileInfo();

            Buffer.BlockCopy(data.payload, 0, transferFileInfo.guid, 0, 32);

            Buffer.BlockCopy(data.payload, 32, byTmp, 0, 8);
            transferFileInfo.length = BitConverter.ToInt64(byTmp, 0);

            Buffer.BlockCopy(data.payload, 40, byTmp, 0, 8);
            transferFileInfo.date = BitConverter.ToInt64(byTmp, 0);

            Buffer.BlockCopy(data.payload, 48, byTmp, 0, 4);
            int name_length = BitConverter.ToInt32(byTmp, 0);
            transferFileInfo.original_name = System.Text.Encoding.UTF8.GetString(data.payload, 52, name_length);

            //ファイルを実行ディレクトリに保存するためファイル名のみにする
            transferFileInfo.name = Path.GetTempPath() + Path.GetFileName(transferFileInfo.original_name);

            //重複ファイルを確認し、重複していた場合はファイル名を変更する
            if (File.Exists(transferFileInfo.name) == true)
            {
                for(int i=0;i<999;++i){
                    string filename = Path.GetTempPath() + Path.GetFileNameWithoutExtension(transferFileInfo.name)
                        + "_" + i.ToString()
                        + Path.GetExtension(transferFileInfo.name);
                    if (File.Exists(filename) == false)
                    {
                        Debug.WriteLine(string.Format("OnEventDataRecived_FileInfo rename {0} => {1}", i, filename));
                        transferFileInfo.name = filename;
                        break;
                    }
                }
            }

            int packet_count = (int)(transferFileInfo.length / TransferData.PACKET_SIZE) + 1;
            transferFileInfo.bRecived = new bool[packet_count];
            for (int i = 0; i < packet_count; ++i)
            {
                transferFileInfo.bRecived[i] = false;
            }

            m_ArrayRecieveFileInfo.Add(transferFileInfo);

            Debug.WriteLine(string.Format("OnEventDataRecived_FileInfo : length={0}, name={1}", transferFileInfo.length, transferFileInfo.name));
            Debug.WriteLine(string.Format("OnEventDataRecived_FileInfo <<"));
        }

        //ファイル受信ハンドラ
        private void OnEventDataRecived_File(UDPTransfer.DataRecived data)
        {
            try
            {

                //データを読み込む
                byte[] byTmp = new byte[4];
                TransferData transferData = new TransferData();

                Debug.WriteLine(string.Format("OnEventDataRecived_File payload size = {0}", data.payload.Length));

                Buffer.BlockCopy(data.payload, 4, transferData.guid, 0, 32);

                Buffer.BlockCopy(data.payload, 36, byTmp, 0, 4);
                transferData.index = BitConverter.ToInt32(byTmp, 0);

                Buffer.BlockCopy(data.payload, 40, byTmp, 0, 4);
                transferData.data_size = BitConverter.ToInt32(byTmp, 0);

                Debug.WriteLine(string.Format("OnEventDataRecived_File data_size = {0}, index={1}", transferData.data_size, transferData.index));

                //Buffer.BlockCopy(data.payload, 44, transferData.data, 0, TransferData.PACKET_SIZE);
                Debug.WriteLine(string.Format("OnEventDataRecived_File 0"));
                Buffer.BlockCopy(data.payload, 44, transferData.data, 0, data.payload.Length - 44);
                Debug.WriteLine(string.Format("OnEventDataRecived_File 1"));

                //ファイル情報を取得する
                int index = RecieveFileInfo_FindData(transferData.guid);
                if (index < 0)
                {
                    Debug.WriteLine(string.Format("OnEventDataRecived_File : this data is already recived"));
                    Debug.WriteLine(string.Format("OnEventDataRecived_File <<"));
                    return;
                }
                Debug.WriteLine(string.Format("OnEventDataRecived_File 2"));
                TransferFileInfo transferFileInfo = (TransferFileInfo)m_ArrayRecieveFileInfo[index];
                Debug.WriteLine(string.Format("OnEventDataRecived_File : length={0}, name={1}", transferFileInfo.length, transferFileInfo.name));

                if (File.Exists(transferFileInfo.name) == false)
                {
                    FileStream fs = new FileStream(transferFileInfo.name, FileMode.CreateNew, FileAccess.Write);
                    fs.SetLength(transferFileInfo.length);
                    fs.Close();
                    fs.Dispose();
                }

                //ファイルを出力する
                using (FileStream fs = new FileStream(transferFileInfo.name, FileMode.Open, FileAccess.Write))
                {
                    //書き込み位置にシークする
                    fs.Seek(transferData.index * TransferData.PACKET_SIZE, SeekOrigin.Begin);

                    //ファイルへ出力する
                    fs.Write(transferData.data, 0, transferData.data_size);

                    transferFileInfo.bRecived[transferData.index] = true;

                }

                //全データを受信したかを確認する
                bool IsRecived = true;
                int downloaded_count = 0;
                for (int i = 0; i < transferFileInfo.bRecived.Length; ++i)
                {
                    if (transferFileInfo.bRecived[i] == false)
                    {
                        IsRecived = false;
                        //break;
                        ++downloaded_count;
                    }
                }

                //全データを受信した時の処理
                if (IsRecived == true)
                {
                    //受信完了を通知する
                    if (EventHndler != null)
                    {
                        TransferProgress progress = new TransferProgress();
                        progress.name = transferFileInfo.name;
                        progress.original_name = transferFileInfo.original_name;
                        progress.progress = 1.0;
                        progress.endpoint = data.endPoint;

                        EventHndler(EventID.Recived_File, progress, null);
                    }
                    m_ArrayRecieveFileInfo.RemoveAt(index);
                }
                else
                {
                    //受信経過を通知する
                    if (EventHndler != null)
                    {
                        TransferProgress progress = new TransferProgress();
                        progress.name = transferFileInfo.name;
                        progress.original_name = transferFileInfo.original_name;
                        progress.progress = (double)(transferFileInfo.bRecived.Length - downloaded_count) / (double)transferFileInfo.bRecived.Length;
                        progress.endpoint = data.endPoint;

                        EventHndler(EventID.ReciveFile_Progress, progress, null);
                    }
                }

            }
            catch(Exception ex){
                Debug.WriteLine("OnEventDataRecived_File : Error : " + ex.Message);

            }
        }

        //バイナリ情報受信ハンドラ
        private void OnEventDataRecived_BinaryInfo(UDPTransfer.DataRecived data)
        {
            //データを読み込む
            byte[] byTmp = new byte[8];
            TransferBinaryInfo transferFileInfo = new TransferBinaryInfo();

            Buffer.BlockCopy(data.payload, 0, transferFileInfo.guid, 0, 32);

            Buffer.BlockCopy(data.payload, 32, byTmp, 0, 8);
            transferFileInfo.length = BitConverter.ToInt64(byTmp, 0);

            Buffer.BlockCopy(data.payload, 40, byTmp, 0, 8);
            transferFileInfo.date = BitConverter.ToInt64(byTmp, 0);

            Buffer.BlockCopy(data.payload, 48, byTmp, 0, 4);
            int name_length = BitConverter.ToInt32(byTmp, 0);
            transferFileInfo.name = System.Text.Encoding.UTF8.GetString(data.payload, 52, name_length);

            int packet_count = (int)(transferFileInfo.length / TransferData.PACKET_SIZE) + 1;
            transferFileInfo.bRecived = new bool[packet_count];
            for (int i = 0; i < packet_count; ++i)
            {
                transferFileInfo.bRecived[i] = false;
            }
            transferFileInfo.data = new byte[transferFileInfo.length];
            m_ArrayRecieveBinaryInfo.Add(transferFileInfo);

            Debug.WriteLine(string.Format("OnEventDataRecived_BinaryInfo : length={0}, name={1}", transferFileInfo.length, transferFileInfo.name));
        }

        //バイナリ受信ハンドラ
        private void OnEventDataRecived_Binary(UDPTransfer.DataRecived data)
        {
            try
            {

                //データを読み込む
                byte[] byTmp = new byte[4];
                TransferData transferData = new TransferData();

                Debug.WriteLine(string.Format("OnEventDataRecived_Binary payload size = {0}", data.payload.Length));

                Buffer.BlockCopy(data.payload, 4, transferData.guid, 0, 32);

                Buffer.BlockCopy(data.payload, 36, byTmp, 0, 4);
                transferData.index = BitConverter.ToInt32(byTmp, 0);

                Buffer.BlockCopy(data.payload, 40, byTmp, 0, 4);
                transferData.data_size = BitConverter.ToInt32(byTmp, 0);

                Debug.WriteLine(string.Format("OnEventDataRecived_Binary data_size = {0}, index={1}", transferData.data_size, transferData.index));

                Buffer.BlockCopy(data.payload, 44, transferData.data, 0, TransferData.PACKET_SIZE);

                //データ情報を取得する
                int index = RecieveBinaryInfo_FindData(transferData.guid);
                if (index < 0)
                {
                    Debug.WriteLine(string.Format("OnEventDataRecived_Binary : this data is already recived"));
                    Debug.WriteLine(string.Format("OnEventDataRecived_Binary <<"));
                    return;
                }
                TransferBinaryInfo transferFileInfo = (TransferBinaryInfo)m_ArrayRecieveBinaryInfo[index];
                Debug.WriteLine(string.Format("OnEventDataRecived_Binary : length={0}, name={1}", transferFileInfo.length, transferFileInfo.name));

                //データをコピーする
                Buffer.BlockCopy(transferData.data, 0, transferFileInfo.data, transferData.index * TransferData.PACKET_SIZE, transferData.data_size);
                Debug.WriteLine(string.Format("OnEventDataRecived_Binary : 1"));
                transferFileInfo.bRecived[transferData.index] = true;
                Debug.WriteLine(string.Format("OnEventDataRecived_Binary : 2"));

                //全データを受信したかを確認する
                bool IsRecived = true;
                for (int i = 0; i < transferFileInfo.bRecived.Length; ++i)
                {
                    if (transferFileInfo.bRecived[i] == false)
                    {
                        IsRecived = false;
                        break;
                    }
                }

                //全データを受信した時の処理
                if (IsRecived == true)
                {
                    if (EventHndler != null)
                    {
                        EventHndler(EventID.Recived_Binary, transferFileInfo.data, data.endPoint);
                    }
                    m_ArrayRecieveBinaryInfo.RemoveAt(index);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OnEventDataRecived_Binary : Error : " + ex.Message);

            }
        }
    }
}
