using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Data.SQLite;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.IO;

namespace Lab2Application
{
    /* https://docs.microsoft.com/ru-ru/dotnet/framework/network-programming/asynchronous-client-socket-example
    Примеры асинхронных сокетов клиента
    Использование только в личных и некоммерческих целях.
    Если не указано иное, Службы предназначены для использования в личных и некоммерческих целях. 
    Без предварительного письменного согласия Microsoft Вы не имеете права модифицировать, копировать, распространять, 
    пересылать, отображать, публично демонстрировать, воспроизводить, публиковать, лицензировать, 
    передавать и продавать какую-либо информацию, программное обеспечение, продукты или услуги, 
    полученные через данные Службы, а также создавать на их основе производные работы 
    (за исключением тех случаев, когда это предназначается для вашего собственного, личного, некоммерческого использования).
    Microsoft не требует права собственности на материалы, которые вы предоставляете ей прямо 
    (включая отзывы и предложения), а также публикуете, загружаете на сервер или передаете на любой веб-узел или в любые 
    Службы (или в смежные службы) для предоставления широкой общественности или участникам какого-либо сообщества 
    (такие данные отдельно и в совокупности называются «Предоставляемыми данными»). Однако, публикуя, отправляя, 
    внося, предоставляя или передавая Предоставляемые данные («Публикация»), вы передаете Microsoft, аффилированным 
    компаниям Microsoft и владельцам сублицензий Microsoft разрешение на использование Предоставляемых данных в связи
    с их профессиональной деятельностью в Интернете (это также относится, среди прочего, ко всем службам Microsoft), 
    включая, среди прочего, лицензионные права на: копирование, распространение, передачу, публичную демонстрацию, 
    публичное исполнение, воспроизводство, редактирование, перевод, и реформатирование Предоставляемых данных; на
    публикацию вашего имени в связи с Предоставляемыми данными; а также предоставляете право на сублицензирование 
    таких прав любому поставщику Служб.
    */
    public class StateObject
    {
        // Client socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 1024;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }

    public class AsynchronousClient
    {
        // The port number for the remote device.  
        private const int port = 11000;
        public static RichTextBox rtb;
        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);

        // The response from the remote device.  
        private static String response = String.Empty;
        private static byte[] ObjectToByteArray(Object obj)
        {
            if (obj == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);

            return ms.ToArray();
        }

        // Convert a byte array to an Object
        private static Object ByteArrayToObject(byte[] arrBytes)
        {
            MemoryStream memStream = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            Object obj = (Object)binForm.Deserialize(memStream);

            return obj;
        }
        public static void StartClient()
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // The name of the   
                // remote device is "host.contoso.com".  
                IPHostEntry ipHostInfo = Dns.GetHostEntry("127.0.0.1");
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                byte[] bytes = new byte[1024];
                // Create a TCP/IP socket.  x
                Socket sender = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);
                // Connect to the remote endpoint.
                SQLiteConnection Connect;
                string fullPath = "D:\\DataBase\\first.db";
                var connectionString = "data source=\"" + fullPath + "\"";
                Connect = new SQLiteConnection(connectionString);
                Connect.Open();
                using (Connect)
                {
                    
                    string commandText = @"SELECT marks_id,
                                           marks_name,
                                           marks_official_site,
                                           goods_id,
                                           goods_mark_id,
                                           goods_model,
                                           prices_id,
                                           prices_good_id,
                                           prices_shop_id,
                                           prices_price,
                                           datetime(prices_date,'unixepoch','localtime') AS `prices_date`,
                                           stores_id,
                                           stores_type_id,
                                           stores_adress,
                                           store_types_id,
                                           store_types_name
                                            FROM one;";
                    SQLiteCommand Command = new SQLiteCommand(commandText, Connect);
                    SQLiteDataReader reader;
                    reader = Command.ExecuteReader();
                    while (reader.Read())
                    {
                        sender = new Socket(ipAddress.AddressFamily,SocketType.Stream, ProtocolType.Tcp);
                        sender.Connect(remoteEP);
                        //MessageBox.Show(reader["marks_name"].ToString());
                        string[] ss = new string[17];
                        ss[0] = 12332.ToString();
                        int i = 1;
                        while (i < ss.Length)
                        {
                            ss[i] = reader[(i++ - 1)].ToString();
                        }
                        ss[16] += "<EOF>";
                        // Send test data to the remote device.  
                        int bytesSent = sender.Send(ObjectToByteArray(ss));
                        // Receive the response from the remote device.  
                        // int bytesRec = sender.Receive(bytes);
                        // MessageBox.Show("Echoed test = " +
                        // Encoding.ASCII.GetString(bytes, 0, bytesRec));
                        sender.Shutdown(SocketShutdown.Both);
                        sender.Close();
                    }
                    
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Ошибка подключения, перезапустите службу (" + e.Message + ")\n");
            }
        }

    }
}

