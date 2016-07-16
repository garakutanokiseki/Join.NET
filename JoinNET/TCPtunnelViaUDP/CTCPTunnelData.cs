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
    /// トンネル用データ
    /// </summary>
    class UDPData
    {
        //トンネル通信コマンド
        public const uint COMMAND_SEND = 1;
        public const uint COMMAND_CLOSE_SOCKET = 2;
        public const uint COMMAND_ACK = 3;
        public const uint COMMAND_CONNECT = 4;

        //UDPホール作成用コマンド
        public const uint COMMAND_CONNECT_REQUEST = 5;
        public const uint COMMAND_CONNECT_ANSWER = 6;
        public const uint COMMAND_CONNECT_KEEP = 7;

        //クライアント側で通信に使われているソケット番号
        public uint socket_num;
        //ソケットのコマンド
        public uint command;
        //送信データ
        public byte[] data;
    }

    /// <summary>
    /// トンネル先通信データ
    /// </summary>
    class TCPData
    {
        //トンネル通信先のソケット番号
        public uint client_socket_num;
        //送信元アドレス
        public IPAddress address { get; set; }
        //送信元ポート
        public int port;
        //通信先のソケット
        public CTCPClient socket;
    }

    /// <summary>
    /// 受信情報を保存するデータ
    /// </summary>
    class AckData
    {
        //送信元エンドポイント
        public IPEndPoint endpoint;
        //受信時刻
        public DateTime received_time;
    }
}

