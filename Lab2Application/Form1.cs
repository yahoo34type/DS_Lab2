using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Messaging;
using System.Data.SQLite;
using MySql.Data.MySqlClient;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;


namespace Lab2Application
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
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
        private string fullPath = "D:\\DataBase\\first.db";
        private string queuePath = @".\private$\MyNewPrivateQueue";
        private void button1_Click(object sender, EventArgs e)
        {
            
            richTextBox1.Text += "Производится экспорт данных из SQLite в очередь сообщений\n";
            MessageQueue queue;
            try
            {
                if (!MessageQueue.Exists(queuePath))
                {
                    MessageQueue.Create(queuePath);
                }
                queue = new MessageQueue(queuePath);
                queue.Formatter = new BinaryMessageFormatter();
                SQLiteConnection Connect;
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
                        string[] s = new string[17];
                        s[0] = aeskey.ToString();
                        int i = 1;
                        while (i < s.Length)
                        {
                            s[i] = reader[(i++ - 1)].ToString();
                        }
                        richTextBox1.SelectionStart = richTextBox1.TextLength;
                        richTextBox1.ScrollToCaret();
                        queue.Send(ToAes256(s));
                    }
                    Connect.Close();
                    richTextBox1.Text += "Экспорт завершен\n";
                }
            }
            catch (MessageQueueException ex)
            {
                richTextBox1.Text += "Производится экспорт данных из SQLite через сокет\n";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            {
                try
                {
                    //mysql section
                    string connetionString = null;
                    MySqlConnection cnn;
                    connetionString = "server=127.0.0.1;database=new_schema;uid=root;pwd=12345;";
                    cnn = new MySqlConnection(connetionString);
                    richTextBox1.Text += "Производится удаление данных из БД MySQL\n";
                    cnn.Open();
                    {
                        {
                            string sql = @"DELETE FROM prices";
                            MySqlCommand command = new MySqlCommand(sql, cnn);
                            command.ExecuteNonQuery();
                        }
                        {
                            string sql = @"DELETE FROM shops";
                            MySqlCommand command = new MySqlCommand(sql, cnn);
                            command.ExecuteNonQuery();

                        }
                        {
                            string sql = @"DELETE FROM store_types";
                            MySqlCommand command = new MySqlCommand(sql, cnn);
                            command.ExecuteNonQuery();

                        }
                        {
                            string sql = @"DELETE FROM goods";
                            MySqlCommand command = new MySqlCommand(sql, cnn);
                            command.ExecuteNonQuery();
                        }
                        {
                            string sql = @"DELETE FROM marks";
                            MySqlCommand command = new MySqlCommand(sql, cnn);
                            command.ExecuteNonQuery();
                        }
                        richTextBox1.Text += "Удаление произведено\n";
                        richTextBox1.SelectionStart = richTextBox1.TextLength;
                        richTextBox1.ScrollToCaret();
                    }
                    cnn.Close();

                }
                catch (Exception exc)
                {
                    richTextBox1.Text += "Ошибка подключения, перезапустите службу (" + exc.Message + ")\n";
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                richTextBox1.Text += "Производится экспорт данных из SQLite через сокет\n";
                AsynchronousClient.StartClient();
                richTextBox1.Text += "Экспорт завершен\n";
                richTextBox1.SelectionStart = richTextBox1.TextLength;
                richTextBox1.ScrollToCaret();
            }
            catch
            {
                richTextBox1.Text += "Ошибка подключения, перезапустите службу\n";
            }
        }
    }
}
