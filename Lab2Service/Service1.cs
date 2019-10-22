using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.IO;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Messaging;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;

namespace Lab2Service
{
    public partial class Service1 : ServiceBase
    {
        DBImporter dbimporter;
        public Service1()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            dbimporter = new DBImporter();
            Thread dbimporterThread = new Thread(new ThreadStart(dbimporter.Start));
            dbimporterThread.Start();
        }

        protected override void OnStop()
        {
            dbimporter.Stop();
            Thread.Sleep(1000);
        }
    }
    class DBImporter
    {
        private static byte[] aeskey = { 38, 179, 214, 150, 131, 92, 176, 244,
                                        80, 210, 80, 128, 175, 199, 138, 247, 179,
            73, 210, 138, 203, 80, 50, 166, 124, 181, 249, 231, 73, 32, 140, 62 };
        private static byte[] ObjectToByteArray(Object obj)
        {
            if (obj == null)
                return null;

            BinaryFormatter bf = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            bf.Serialize(ms, obj);

            return ms.ToArray();
        }
        private static Object ByteArrayToObject(byte[] arrBytes)
        {
            MemoryStream memStream = new MemoryStream();
            BinaryFormatter binForm = new BinaryFormatter();
            memStream.Write(arrBytes, 0, arrBytes.Length);
            memStream.Seek(0, SeekOrigin.Begin);
            Object obj = (Object)binForm.Deserialize(memStream);
            return obj;
        }
        public static byte[] ToAes256(string[] src)
        {
            //Объявляем объект класса AES
            Aes aes = Aes.Create();
            //Генерируем соль
            aes.GenerateIV();
            aes.GenerateKey();
            //Присваиваем ключ. aeskey - переменная (массив байт), сгенерированная методом GenerateKey() класса AES
            aes.Key = aeskey;
            Console.WriteLine(aes.Key);
            byte[] encrypted;
            ICryptoTransform crypt = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, crypt, CryptoStreamMode.Write))
                {
                    cs.Write(ObjectToByteArray(src), 0, ObjectToByteArray(src).Length);
                }
                //Записываем в переменную encrypted зашиврованный поток байтов
                encrypted = ms.ToArray();
            }
            //Возвращаем поток байт + крепим соль
            return encrypted.Concat(aes.IV).ToArray();
        }
        public static string[] FromAes256(byte[] shifr)
        {
            byte[] bytesIv = new byte[16];
            byte[] mess = new byte[shifr.Length - 16];
            //Списываем соль
            for (int i = shifr.Length - 16, j = 0; i < shifr.Length; i++, j++)
                bytesIv[j] = shifr[i];
            //Списываем оставшуюся часть сообщения
            for (int i = 0; i < shifr.Length - 16; i++)
                mess[i] = shifr[i];
            //Объект класса Aes
            Aes aes = Aes.Create();
            //Задаем тот же ключ, что и для шифрования
            aes.Key = aeskey;
            //Задаем соль
            aes.IV = bytesIv;
            //Строковая переменная для результата
            string[] res;
            byte[] data = mess;
            ICryptoTransform crypt = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (CryptoStream cs = new CryptoStream(ms, crypt, CryptoStreamMode.Read))
                {
                    byte[] buf = new byte[1024];
                    cs.Read(buf, 0, 1024);
                    res = (string[])ByteArrayToObject(buf);
                }
            }
            return res;
        }
        string connectionString = "server=127.0.0.1;database=new_schema;uid=root;pwd=12345;";
        private string queuePath = @".\private$\MyNewPrivateQueue";
        /* https://docs.microsoft.com/ru-ru/dotnet/api/system.messaging.messagequeue?view=netframework-4.8
        MessageQueue Class
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
        MessageQueue queue;
        MySqlConnection cnn;
        bool enabled = true;
        public DBImporter()
        {
            cnn = new MySqlConnection(connectionString);
        }
        public void Start()
        {
            enabled = true;
            cnn.Open();
            queue = new MessageQueue(queuePath);
            while (enabled)
            {
                Thread.Sleep(1000);
                ImportMQ();
                ImportSocket();
            }
        }
        public void Stop()
        {
            enabled = false;
            cnn.Close();
        }
        public void ImportMQ()
        {
            try
            {
                queue.Formatter = new BinaryMessageFormatter();
                var enumerator = queue.GetMessageEnumerator2();
                while (enumerator.MoveNext())
                {
                    System.Messaging.Message message = queue.Receive();
                    string[] msg = FromAes256((byte[])message.Body);
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
                }
            }
            catch (Exception e)
            {
            }

        }
        public void ImportSocket()
        {

        }
    }
}