using System;
using System.ServiceProcess;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using MySql.Data.MySqlClient;

namespace Lab2ServiceSocket
{
    /* https://docs.microsoft.com/ru-ru/dotnet/framework/network-programming/synchronous-server-socket-example
    Пример синхронного сокета сервера
    Использование только в личных и некоммерческих целях.
    Если не указано иное, Службы предназначены для использования в личных и некоммерческих целях.
    Без предварительного письменного согласия Microsoft Вы не имеете права модифицировать, копировать, распространять,
    пересылать, отображать, публично демонстрировать, воспроизводить, публиковать, лицензировать,
    передавать и продавать какую-либо информацию, программное обеспечение, продукты или услуги,
    полученные через данные Службы, а также создавать на их основе производные работы
    (за исключением тех случаев, когда это предназначается для вашего собственного, личного, некоммерческого использования).
    Microsoft не требует права собственности на материалы, которые вы предоставляете ей прямо
    (включая отзывы и предложения), а также публикуете, загружаете на сервер или передаете на любой веб-узел или в любые
    Службы(или в смежные службы) для предоставления широкой общественности или участникам какого-либо сообщества
   (такие данные отдельно и в совокупности называются «Предоставляемыми данными»). Однако, публикуя, отправляя, 
    внося, предоставляя или передавая Предоставляемые данные(«Публикация»), вы передаете Microsoft, аффилированным
   компаниям Microsoft и владельцам сублицензий Microsoft разрешение на использование Предоставляемых данных в связи
    с их профессиональной деятельностью в Интернете(это также относится, среди прочего, ко всем службам Microsoft), 
    включая, среди прочего, лицензионные права на: копирование, распространение, передачу, публичную демонстрацию,
    публичное исполнение, воспроизводство, редактирование, перевод, и реформатирование Предоставляемых данных; на
    публикацию вашего имени в связи с Предоставляемыми данными; а также предоставляете право на сублицензирование
    таких прав любому поставщику Служб.
    */
    public partial class Service1 : ServiceBase
    {
        static bool enabled = true;
        public static string data = null;
        public static byte[] datas = null;
        private static Object ByteArrayToObject(byte[] arrBytes)
        {
            MemoryStream memStream = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            Object obj = (Object)binForm.Deserialize(memStream);
            return obj;
        }
        public Service1()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            Thread dbimporterThread = new Thread(new ThreadStart(StartListening));
            dbimporterThread.Start();
        }

        protected override void OnStop()
        {
            enabled = false;
            Thread.Sleep(2000);
        }
        public static void StartListening()
        {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the   
            // host running the application.  
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
            MySqlConnection cnn;
            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and   
            // listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                // Start listening for connections.  
                while (enabled)
                {
                    Console.WriteLine("Waiting for a connection...");
                    // Program is suspended while waiting for an incoming connection.  
                    Socket handler = listener.Accept();
                    data = "";
                    datas = new byte[0];
                    // An incoming connection needs to be processed.  
                    while (true) //считывание по байтам
                    {
                        int bytesRec = handler.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        byte[] rv = new byte[datas.Length + bytes.Length];
                        System.Buffer.BlockCopy(datas, 0, rv, 0, datas.Length);
                        System.Buffer.BlockCopy(bytes, 0, rv, datas.Length, bytes.Length);
                        datas = rv;
                        if (data.IndexOf("<EOF>") > -1)
                        {
                            break;
                        }
                    }
                    string[] msg = (string[])ByteArrayToObject(datas);
                    msg[16] = msg[16].Substring(0, msg[16].Length - 5);
                    // Show the data on the console.  
                    string connectionString = "server=127.0.0.1;database=new_schema;uid=root;pwd=12345;";
                    cnn = new MySqlConnection(connectionString);
                    cnn.Open();
                    string key = msg[0];
                    {
                        string sql = @"INSERT IGNORE INTO `new_schema`.`marks`(`id`,`name`,`official_site`)
                                            VALUES(@id,@name,@official_site);";
                        // объект для выполнения SQL-запроса
                        MySqlCommand command = new MySqlCommand(sql, cnn);
                        command.Prepare();
                        command.Parameters.AddWithValue("@id", int.Parse(msg[1].ToString()));
                        command.Parameters.AddWithValue("@name", msg[2].ToString());
                        command.Parameters.AddWithValue("@official_site", msg[3].ToString());
                        command.ExecuteNonQuery();
                    }

                    {
                        string sql = @"INSERT IGNORE INTO `new_schema`.`goods`(`id`,`mark_id`,`model`)
                                            VALUES(@id,@mark_id,@model);";
                        // объект для выполнения SQL-запроса
                        MySqlCommand command = new MySqlCommand(sql, cnn);
                        command.Prepare();
                        command.Parameters.AddWithValue("@id", int.Parse(msg[4].ToString()));
                        command.Parameters.AddWithValue("@mark_id", int.Parse(msg[5].ToString()));
                        command.Parameters.AddWithValue("@model", msg[6].ToString());
                        command.ExecuteNonQuery();
                    }

                    {
                        string sql = @"INSERT IGNORE INTO `new_schema`.`store_types`(`id`,`name`)
                                            VALUES(@id,@name);";
                        // объект для выполнения SQL-запроса
                        MySqlCommand command = new MySqlCommand(sql, cnn);
                        command.Prepare();
                        command.Parameters.AddWithValue("@id", int.Parse(msg[15].ToString()));
                        command.Parameters.AddWithValue("@name", msg[16].ToString());
                        command.ExecuteNonQuery();
                    }

                    {
                        string sql = @"INSERT IGNORE INTO `new_schema`.`shops`(`id`,`store_type_id`,`adress`)
                                            VALUES(@id,@store_type_id,@adress);";
                        // объект для выполнения SQL-запроса
                        MySqlCommand command = new MySqlCommand(sql, cnn);
                        command.Prepare();
                        command.Parameters.AddWithValue("@id", int.Parse(msg[12].ToString()));
                        command.Parameters.AddWithValue("@store_type_id", int.Parse(msg[13].ToString()));
                        command.Parameters.AddWithValue("@adress", msg[14].ToString());
                        command.ExecuteNonQuery();
                    }

                    {
                        string sql = @"INSERT IGNORE INTO `new_schema`.`prices`(`id`,`good_id`,`shop_id`,`price`,`date`)
                                            VALUES(@id,@good_id,@shop_id,@price,@date);";
                        // объект для выполнения SQL-запроса
                        MySqlCommand command = new MySqlCommand(sql, cnn);
                        command.Prepare();
                        command.Parameters.AddWithValue("@id", int.Parse(msg[7].ToString()));
                        command.Parameters.AddWithValue("@good_id", int.Parse(msg[8].ToString()));
                        command.Parameters.AddWithValue("@shop_id", int.Parse(msg[9].ToString()));
                        command.Parameters.AddWithValue("@price", int.Parse(msg[10].ToString()));
                        command.Parameters.AddWithValue("@date", msg[11].ToString());
                        command.ExecuteNonQuery();
                    }
                    cnn.Close();
                    //Console.WriteLine("Text received : {0}", ByteArrayToObject(datas));
                    foreach (string str in msg)
                    {
                        // Console.WriteLine(str);
                    }
                    // Echo the data back to the client.  
                    //byte[] msgs = Encoding.ASCII.GetBytes(data);

                    //handler.Send(msgs);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                }

            }
            catch (Exception e)
            {
                //Console.WriteLine(e.ToString());
            }

        }
    }
}
