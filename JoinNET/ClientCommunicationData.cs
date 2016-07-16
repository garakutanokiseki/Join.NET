using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JoinNET
{
    /// <summary>
    /// 通信用データ保持クラス
    /// </summary>
    class ClientCommunicationData
    {
        //接続元のIPアドレス
        public string source_ip = "";

        //接続元のポート番号
        public int source_port = 0;

        //接続先のIPアドレス
        public string target_ip = "";

        //接続先のポート番号
        public int target_port = 0;
    }
}
