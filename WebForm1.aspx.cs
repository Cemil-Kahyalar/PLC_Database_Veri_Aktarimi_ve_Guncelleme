using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Policy;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Configuration;

namespace PLC_Database_Veri_Aktarimi_ve_Guncelleme
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        // private static int columnCount = 1;
        Plc plc1510;

        private static int columnStartIndex = 2;


        private static int GetColumnCount()
        {
            string connectionString = "Data Source=DESKTOP-MCFAIJF\\SQLEXPRESS;Initial Catalog=PLC_DB;Integrated Security=True;Encrypt=False";
            string getColumnCountQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DB101'";

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    SqlCommand getColumnCountCommand = new SqlCommand(getColumnCountQuery, connection);
                    connection.Open();
                    int columnCount = (int)getColumnCountCommand.ExecuteScalar();
                    return columnCount;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hata: {ex.Message}");
                return -1; // Hata durumunda -1 döndürüyoruz
            }
        }

        private static void GenerateExcelFile()
        {
            string connectionString = "Data Source=DESKTOP-MCFAIJF\\SQLEXPRESS;Initial Catalog=PLC_DB;Integrated Security=True;Encrypt=False";

            int columnCount = GetColumnCount();

            // columnCount geçerli bir değer mi kontrol edelim
            if (columnCount < 1)
            {
                Console.WriteLine("Geçersiz column count değeri.");
                return;
            }

            string columnName = "okuma_" + columnStartIndex.ToString();
            string dateColumnName = "tarih_" + columnStartIndex.ToString();

            string addColumnQuery = $@"
        ALTER TABLE DB101
        ADD {columnName} NVARCHAR(50) NULL, {dateColumnName} NVARCHAR(50) NULL";

            string updateDataQuery = $@"
        UPDATE DB101
        SET [{columnName}] = @deger, [{dateColumnName}] = @tarih
        WHERE [deger_isim] = @isim"; // Assuming you have an ID column or primary key to update specific rows

            using (var plc1510 = new Plc(CpuType.S71500, "192.168.0.20", 0, 1))
            {
                plc1510.Open();

                if (plc1510.IsConnected)
                {
                    int dbNumber = 101; // DB101
                    int startByte = 0; // Verinin başlangıç baytı
                    int arrayLength = 100; // Array uzunluğu

                    byte[] Data = plc1510.ReadBytes(DataType.DataBlock, dbNumber, startByte, arrayLength * 2);
                    ushort[] result = Word.ToArray(Data);

                    try
                    {
                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();

                            // Yeni sütun ekle
                            SqlCommand addColumnCommand = new SqlCommand(addColumnQuery, connection);
                            addColumnCommand.ExecuteNonQuery();

                            // Verileri ekle
                            string getColumnNamesQuery = $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DB101' ORDER BY ORDINAL_POSITION";
                            SqlCommand getColumnNamesCommand = new SqlCommand(getColumnNamesQuery, connection);
                            List<string> columns = new List<string>();
                            using (SqlDataReader reader = getColumnNamesCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    columns.Add(reader["COLUMN_NAME"].ToString());
                                }
                            }
                            int currentColumnCount = columns.Count;

                            if (columns.Count > 28)
                            {
                                // En eski okuma_ ve tarih_ sütunlarını sil
                                for (int i = 2; i < columns.Count && columns.Count > 28; i += 2)
                                {
                                    // Okuma ve tarih sütunlarını seç
                                    string columnToRemove = columns[i]; // İlk okuma_ sütunu
                                    string dateColumnToRemove = columns[i + 1]; // İlk tarih_ sütunu

                                    // Sütunları silme sorgusu
                                    string removeColumnQuery = $@"
                                    ALTER TABLE DB101 
                                    DROP COLUMN {columnToRemove}, 
                                     {dateColumnToRemove}";

                                    using (SqlCommand removeColumnCommand = new SqlCommand(removeColumnQuery, connection))
                                    {
                                        removeColumnCommand.ExecuteNonQuery();
                                    }

                                    // Silinen sütunları listeden kaldır
                                    columns.RemoveAt(i + 1); // Tarih sütunu
                                    columns.RemoveAt(i); // Okuma sütunu

                                    // En fazla 28 sütun olacak şekilde devam et
                                    if (columns.Count <= 28)
                                    {
                                        break;
                                    }
                                }
                            }

                            using (SqlCommand updateDataCommand = new SqlCommand(updateDataQuery, connection))
                            {
                                for (int i = 0; i < 100; i++)
                                {
                                    System.DateTime now = System.DateTime.Now;
                                    string tarih = now.ToString("yyyy-MM-dd HH:mm:ss");
                                    string isim = "Data[" + i.ToString() + "]";

                                    updateDataCommand.Parameters.Clear();
                                    updateDataCommand.Parameters.AddWithValue("@deger", Convert.ToInt16(result[i]));
                                    updateDataCommand.Parameters.AddWithValue("@tarih", tarih);
                                    updateDataCommand.Parameters.AddWithValue("@isim", isim);
                                    updateDataCommand.ExecuteNonQuery();
                                }
                            }

                            Console.WriteLine($"Sütunlar {columnName} ve {dateColumnName} başarıyla eklendi ve veriler güncellendi.");
                            columnStartIndex++; // Bir sonraki sütun adı için sayacı artır
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Hata: {ex.Message}");
                    }
                }
            }
        }
        protected void Page_Load(object sender, EventArgs e)
        {

            if (!IsPostBack)
            {
                Timer1.Enabled = false;

                using (plc1510 = new Plc(CpuType.S71500, "192.168.0.20", 0, 1))
                {

                    try
                    {
                        plc1510.Open();


                        if (plc1510.IsConnected)
                        {
                            Label1.Text = "PLC baglandi -";
                            Label1.ForeColor = Color.Green;
                        }
                        else
                        {
                            Label1.Text = "PLC baglanilamadi -";
                            Label1.ForeColor = Color.Red;
                        }
                    }
                    catch (Exception ex)
                    {
                        Response.Write("Bağlantı hatası: " + ex.Message);
                    }
                }
                string connectionString = "Data Source=DESKTOP-MCFAIJF\\SQLEXPRESS;Initial Catalog=PLC_DB;Integrated Security=True;Encrypt=False";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        Label1.Text += " SQL baglandi ";
                        Label1.ForeColor = Color.Green;

                    }
                    catch (Exception ex)
                    {
                        Response.Write("Bağlantı hatası: " + ex.Message);
                        Label1.Text += " SQL baglanilamadi ";
                        Label1.ForeColor = Color.Red;

                    }
                }
            }
        }



        protected void Button1_Click(object sender, EventArgs e)
        {
            using (plc1510 = new Plc(CpuType.S71500, "192.168.0.20", 0, 1))
            {

                plc1510.Open();


                if (plc1510.IsConnected)
                {
                    int dbNumber = 101; // DB101
                    int startByte = 0; // Verinin başlangıç baytı
                    int arrayLength = 100; // Array uzunluğu

                    byte[] Data = plc1510.ReadBytes(DataType.DataBlock, dbNumber, startByte, arrayLength * 2);
                    ushort[] result = Word.ToArray(Data);


                    string connectionString = "Data Source=DESKTOP-MCFAIJF\\SQLEXPRESS;Initial Catalog=PLC_DB;Integrated Security=True;Encrypt=False";

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();

                        string query = "INSERT INTO DB101(deger_isim,ilk_okuma,ilk_tarih) VALUES (@isim, @deger,@tarih)";


                        for (int i = 1; i <= 100; i++)
                        {
                            using (SqlCommand command = new SqlCommand(query, connection))
                            {

                                System.DateTime now = System.DateTime.Now;
                                string tarih = now.ToString("yyyy-MM-dd HH:mm:ss");
                                string isim = "Data[" + Convert.ToString(i) + "]";

                                command.Parameters.AddWithValue("@isim", isim);
                                command.Parameters.AddWithValue("@deger", Convert.ToInt16(result[i - 1]));
                                command.Parameters.AddWithValue("@tarih", tarih);
                                command.ExecuteNonQuery();
                            }
                        }


                    }
                }

            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            Timer1.Enabled = true; // Timer'ı başlat
            Label1.Text = "Timer started, file will be updated every 10 seconds.";


        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            Timer1.Enabled = false; // Timer'ı durdur
            Label1.Text = "Timer stopped.";
        }


        protected void Timer1_Tick(object sender, EventArgs e)
        {
            GenerateExcelFile();
        }


    }
}
