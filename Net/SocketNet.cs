using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Net
{
    public class SocketNet
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        public static byte[] MsgBuffer = new byte[4096];
        public void Connect(IPAddress ip, int port)
        {
            this.clientSocket.BeginConnect(ip, port, new AsyncCallback(ConnectCallback), this.clientSocket);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                handler.EndConnect(ar);
            }
            catch
            { }
        }
        public void Send(string data)
        {
            Send(System.Text.Encoding.UTF8.GetBytes(data));
        }

        private void Send(byte[] byteData)
        {
            try
            {
                int length = byteData.Length;
                byte[] head = BitConverter.GetBytes(length);
                byte[] data = new byte[head.Length + byteData.Length];
                Array.Copy(head, data, head.Length);
                Array.Copy(byteData, 0, data, head.Length, byteData.Length);
                this.clientSocket.BeginSend(data, 0, data.Length, 0, new AsyncCallback(SendCallback), this.clientSocket);
            }
            catch
            { }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                handler.EndSend(ar);
            }
            catch
            { }
        }

        public void ReceiveData()
        {
            clientSocket.BeginReceive(MsgBuffer, 0, MsgBuffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                int REnd = clientSocket.EndReceive(ar);
                if (REnd > 0)
                {
                    byte[] data = new byte[REnd];
                    Array.Copy(MsgBuffer, 0, data, 0, REnd);

                    //在此次可以对data进行按需处理

                    clientSocket.BeginReceive(MsgBuffer, 0, MsgBuffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
                }
                else
                {
                    dispose();
                }
            }
            catch 
            { }
        }

        private void dispose()
        {
            try
            {
                this.clientSocket.Shutdown(SocketShutdown.Both);
                this.clientSocket.Close();
            }
            catch 
            { }
        }


        public Hashtable DataTable = new Hashtable();//因为要接收到多个服务器（ip）发送的数据，此处按照ip地址分开存储发送数据

        public void DataArrial(byte[] Data, string ip)
        {
            try
            {
                if (Data.Length < 12)//按照需求进行判断
                {
                    lock (DataTable)
                    {
                        if (DataTable.Contains(ip))
                        {
                            DataTable[ip] = Data;
                            return;
                        }
                    }
                }

                if (Data[0] != 0x1F || Data[1] != 0xF1)//标志位（按照需求编写）
                {
                    if (DataTable.Contains(ip))
                    {
                        if (DataTable != null)
                        {
                            byte[] oldData = (byte[])DataTable[ip];//取出粘包数据
                            if (oldData[0] != 0x1F || oldData[1] != 0xF1)
                            {
                                return;
                            }
                            byte[] newData = new byte[Data.Length + oldData.Length];
                            Array.Copy(oldData, 0, newData, 0, oldData.Length);
                            Array.Copy(Data, 0, newData, oldData.Length, Data.Length);//组成新数据数组，先到的数据排在前面，后到的数据放在后面

                            lock (DataTable)
                            {
                                DataTable[ip] = null;
                            }
                            DataArrial(newData, ip);
                            return;
                        }
                    }
                    return;
                }

                int revDataLength = Data[2];//打算发送数据的长度
                int revCount = Data.Length;//接收的数据长度
                if (revCount > revDataLength)//如果接收的数据长度大于发送的数据长度，说明存在多帧数据，继续处理
                {
                    byte[] otherData = new byte[revCount - revDataLength];
                    Data.CopyTo(otherData, revCount - 1);
                    Array.Copy(Data, revDataLength, otherData, 0, otherData.Length);
                    Data = (byte[])Redim(Data, revDataLength);
                    DataArrial(otherData, ip);
                }
                if (revCount < revDataLength) //接收到的数据小于要发送的长度
                {
                    if (DataTable.Contains(ip))
                    {
                        DataTable[ip] = Data;//更新当前粘包数据
                        return;
                    }
                }

                //此处可以按需进行数据处理
            }
            catch 
            { }
        }

        private Array Redim(Array origArray, Int32 desizedSize)
        {
            //确认每个元素的类型
            Type t = origArray.GetType().GetElementType();
            //创建一个含有期望元素个数的新数组
            //新数组的类型必须匹配数组的类型
            Array newArray = Array.CreateInstance(t, desizedSize);
            //将原数组中的元素拷贝到新数组中。
            Array.Copy(origArray, 0, newArray, 0, Math.Min(origArray.Length, desizedSize));
            //返回新数组
            return newArray;
        }
    }
}
