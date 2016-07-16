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
    /// �g���l���p�f�[�^
    /// </summary>
    class UDPData
    {
        //�g���l���ʐM�R�}���h
        public const uint COMMAND_SEND = 1;
        public const uint COMMAND_CLOSE_SOCKET = 2;
        public const uint COMMAND_ACK = 3;
        public const uint COMMAND_CONNECT = 4;

        //UDP�z�[���쐬�p�R�}���h
        public const uint COMMAND_CONNECT_REQUEST = 5;
        public const uint COMMAND_CONNECT_ANSWER = 6;
        public const uint COMMAND_CONNECT_KEEP = 7;

        //�N���C�A���g���ŒʐM�Ɏg���Ă���\�P�b�g�ԍ�
        public uint socket_num;
        //�\�P�b�g�̃R�}���h
        public uint command;
        //���M�f�[�^
        public byte[] data;
    }

    /// <summary>
    /// �g���l����ʐM�f�[�^
    /// </summary>
    class TCPData
    {
        //�g���l���ʐM��̃\�P�b�g�ԍ�
        public uint client_socket_num;
        //���M���A�h���X
        public IPAddress address { get; set; }
        //���M���|�[�g
        public int port;
        //�ʐM��̃\�P�b�g
        public CTCPClient socket;
    }

    /// <summary>
    /// ��M����ۑ�����f�[�^
    /// </summary>
    class AckData
    {
        //���M���G���h�|�C���g
        public IPEndPoint endpoint;
        //��M����
        public DateTime received_time;
    }
}

