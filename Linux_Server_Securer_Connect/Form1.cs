using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH.Client;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Windows.Forms.DataVisualization.Charting;

namespace Linux_Server_Securer_Connect
{
    public partial class Form1 : Form
    {
        private SshClient cSSH_Command = null;
        private SshClient cSSH_Monitering = null;
        private SshClient cSSH_TomCat_Log1 = null;
        private SshClient cSSH_TomCat_Log2 = null;
        private SshClient cSSH_TomCat_Log3 = null;

        private ShellStream Command_sShell = null;
        private ShellStream Monitering_sShell = null;
        private ShellStream Tomcat_Log_sShell1 = null;
        private ShellStream Tomcat_Log_sShell2 = null;
        private ShellStream Tomcat_Log_sShell3 = null;

        private Thread Command_thread = null;
        private Thread Sftp_thread = null;
        private Thread Mon_thread1 = null;
        private Thread Tomcat_Log_thread1 = null;
        private Thread Tomcat_Log_thread2 = null;
        private Thread Tomcat_Log_thread3 = null;

        private Series sSin1 = null;
        private Series sSin2 = null;
        private static System.Windows.Forms.Timer aTimer;
        private static int totalMem = 0;

        public Form1()
        {
            InitializeComponent();
        }

        delegate void CrossThread_Control(Control ctl);

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox7.Text = "/webdata/";
            listBox1.Items.Add("All");
            listBox1.Items.Add("tomcat-doc1");
            listBox1.Items.Add("tomcat-doc2");
            listBox1.Items.Add("tomcat-doc3");
            listBox1.SelectedIndex = 0;

            dataGridView1.ReadOnly = true;
            dataGridView1.ColumnCount = 18;

            dataGridView1.Columns[0].HeaderText = "시간";
            dataGridView1.Columns[1].HeaderText = "r";
            dataGridView1.Columns[2].HeaderText = "b";
            dataGridView1.Columns[3].HeaderText = "swpd";
            dataGridView1.Columns[4].HeaderText = "free";
            dataGridView1.Columns[5].HeaderText = "buff";
            dataGridView1.Columns[6].HeaderText = "cache";
            dataGridView1.Columns[7].HeaderText = "si";
            dataGridView1.Columns[8].HeaderText = "so";
            dataGridView1.Columns[9].HeaderText = "bi";
            dataGridView1.Columns[10].HeaderText = "bo";
            dataGridView1.Columns[11].HeaderText = "in";
            dataGridView1.Columns[12].HeaderText = "cs";
            dataGridView1.Columns[13].HeaderText = "us";
            dataGridView1.Columns[14].HeaderText = "sy";
            dataGridView1.Columns[15].HeaderText = "id";
            dataGridView1.Columns[16].HeaderText = "wa";
            dataGridView1.Columns[17].HeaderText = "st";

            chart1.Series.Clear();
            sSin1 = chart1.Series.Add("cpu");
            sSin1.ChartType = SeriesChartType.Pie;

            chart2.Series.Clear();
            sSin2 = chart2.Series.Add("mem");
            sSin2.ChartType = SeriesChartType.Pie;
        }


        private SshClient Connect_SSH(string host, int port, string user, string passwd)
        {
            try
            {
                SshClient cSSH = new SshClient(host, port, user, passwd);

                cSSH.ConnectionInfo.Timeout = TimeSpan.FromSeconds(120);

                cSSH.Connect();

                return cSSH;
            }
            catch (Exception ex)
            {
                label5.Text = "Status : Error";
                return null;
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void AlldisConnection()
        {
            try
            {
                aTimer.Stop();

                Mon_thread1.Abort();
                Tomcat_Log_thread1.Abort();
                Tomcat_Log_thread2.Abort();
                Tomcat_Log_thread3.Abort();

                Command_sShell.Close();
                Monitering_sShell.Close();
                Tomcat_Log_sShell1.Close();
                Tomcat_Log_sShell2.Close();
                Tomcat_Log_sShell3.Close();

                cSSH_Monitering.Disconnect();
                cSSH_TomCat_Log1.Disconnect();
                cSSH_TomCat_Log2.Disconnect();
                cSSH_TomCat_Log3.Disconnect();

                textBox5.Text = "";
                textBox9.Text = "";
                textBox10.Text = "";
                textBox11.Text = "";

                chart1 = null;
                chart2 = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (cSSH_Command == null)
            {
                cSSH_Command = Connect_SSH(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text);

                Command_sShell = cSSH_Command.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

                if (cSSH_Command.IsConnected)
                {
                    label5.Text = "Status : Connected";

                    Command_thread = new Thread(() => recvCommSSHData());

                    Command_thread.IsBackground = true;
                    Command_thread.Start();
                }
            }

            if (cSSH_Monitering == null)
            {
                cSSH_Monitering = Connect_SSH(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text);

                Monitering_sShell = cSSH_Monitering.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

                if (cSSH_Monitering.IsConnected)
                {
                    Mon_thread1 = new Thread(() => recvSSHData());

                    Mon_thread1.IsBackground = true;
                    Mon_thread1.Start();
                }

                Tomcat_LogStart();
            }
        }

        private void textBox6_KeyPress(object sender, KeyPressEventArgs e)
        {
            try
            {
                if (e.KeyChar == 13)
                {
                    Command_sShell.Write(textBox6.Text + "\n");
                    Command_sShell.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void recvCommSSHData()
        {
            while (true)
            {
                try
                {
                    if (Command_sShell != null && Command_sShell.DataAvailable)
                    {
                        String strData = Command_sShell.Read();

                        appendTextBoxInThread(textBox5, strData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }

                Thread.Sleep(200);
            }
        }

        private void recvSSHData()
        {
            string[] originArray = null;
            int oldLen = 0;
            char[] splitChar = new char[] { '\n' };

            while (true)
            {
                try
                {
                    if (Monitering_sShell != null && Monitering_sShell.DataAvailable)
                    {
                        String strData = Monitering_sShell.Read();

                        if (strData.LastIndexOf("$") != -1)
                        {
                            if (originArray == null)
                            {
                                originArray = strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);
                            }
                            else
                            {
                                oldLen = originArray.Length;
                                originArray = (string[])ResizeArray(originArray, originArray.Length + strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries).Length);
                                strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries).CopyTo(originArray, oldLen);
                            }

                            if (Array.Exists(originArray, element => element.StartsWith("vmstat")))
                            {
                                insertGridView(originArray);
                            }
                            else if (Array.Exists(originArray, element => element.StartsWith("MemTotal")))
                            {
                                totalMem = Int32.Parse(originArray[1].Substring(originArray[1].IndexOf(":") + 1, originArray[1].IndexOf("kB") - (originArray[1].IndexOf(":") + 1)).Trim());
                                this.Invoke((MethodInvoker)delegate { label10.Text = "전체메모리 : " + totalMem.ToString("###,###,###.###"); });
                            }

                            originArray = null;
                        }
                        else
                        {
                            if (originArray == null)
                            {
                                originArray = strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);
                            }
                            else
                            {
                                oldLen = originArray.Length;
                                originArray = (string[])ResizeArray(originArray, originArray.Length + strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries).Length);
                                strData.Split(splitChar, StringSplitOptions.RemoveEmptyEntries).CopyTo(originArray, oldLen);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    originArray = null;
                }

                Thread.Sleep(200);
            }
        }

        private void recvLogSSHData(ShellStream sShell, TextBox textbox)
        {
            while (true)
            {
                try
                {
                    if (sShell != null && sShell.DataAvailable)
                    {
                        String strData = sShell.Read();

                        appendTextBoxInThread(textbox, strData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }

                Thread.Sleep(200);
            }
        }

        private void insertGridView(string[] originArray)
        {
            char[] splitChar = new char[] { ' ' };

            try
            {
                string[] vmstat_header = originArray[2].ToString().Replace("\r", "").Split(splitChar, StringSplitOptions.RemoveEmptyEntries);
                string[] vmstat = originArray[4].ToString().Replace("\t\r", "").Split(splitChar, StringSplitOptions.RemoveEmptyEntries);

                if (chart1.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate { UpdatePieChart(vmstat); });
                    this.Invoke((MethodInvoker)delegate { UpdateDataGrid(vmstat_header, vmstat); });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void UpdatePieChart(string[] vmstat)
        {
            chart1.Series["cpu"].Points.Clear();

            chart1.Series["cpu"].Points.AddXY("비커널", vmstat[12]);
            chart1.Series["cpu"].Points.AddXY("커널", vmstat[13]);
            chart1.Series["cpu"].Points.AddXY("유휴", vmstat[14]);

            label8.Text = "비커널/커널사용량(%) : " + vmstat[12] + " / " + vmstat[13];
            label9.Text = "유휴CPU(%) : " + vmstat[14];

            chart2.Series["mem"].Points.Clear();

            chart2.Series["mem"].Points.AddXY("FREE", vmstat[4]);
            chart2.Series["mem"].Points.AddXY("USED", totalMem - Int32.Parse(vmstat[4]));

            label11.Text = "사용량(KB) : " + (totalMem - Int32.Parse(vmstat[4])).ToString("###,###,###.###");
            label12.Text = "여유량(KB) : " + (Int32.Parse(vmstat[4])).ToString("###,###,###.###");
        }

        private void UpdateDataGrid(string[] vmstat_header, string[] vmstat)
        {
            dataGridView1.Rows.Add();

            for (int i = 0; i < vmstat.Length; i++)
            {
                if (i == 0)
                {
                    dataGridView1.Rows[dataGridView1.Rows.Count - 2].Cells[i].Value = DateTime.Now.ToString("yyyy-mm-dd HH:mm:ss");
                }
                else
                {
                    dataGridView1.Rows[dataGridView1.Rows.Count - 2].Cells[i].Value = vmstat[i];
                }
            }
        }

        private void appendTextBoxInThread(TextBox t, String s)
        {
            if (t.InvokeRequired)
            {
                t.Invoke(new Action<TextBox, string>(appendTextBoxInThread), new object[] { t, s });
            }
            else
            {
                t.AppendText(s);
            }
        }

        public static System.Array ResizeArray(System.Array oldArray, int newSize)
        {
            int oldSize = oldArray.Length;

            System.Type elementType = oldArray.GetType().GetElementType();
            System.Array newArray = System.Array.CreateInstance(elementType, newSize);

            int preserveLength = System.Math.Min(oldSize, newSize);

            if (preserveLength > 0)
            {
                System.Array.Copy(oldArray, newArray, preserveLength);
            }

            return newArray;
        } 

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            AlldisConnection();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            AlldisConnection();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            aTimer = new System.Windows.Forms.Timer();
            aTimer.Interval = 5000;
            aTimer.Tick += new EventHandler(timer_tick);
            aTimer.Start();

            Monitering_sShell.Write("cat /proc/meminfo | grep MemTotal \n");
            Monitering_sShell.Flush();
        }

        private void timer_tick(object sender, EventArgs e)
        {
            Monitering_sShell.Write("vmstat 1 2 \n");
            Monitering_sShell.Flush();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            aTimer.Stop();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            Tomcat_Service("./startup.sh");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Tomcat_Service("./shutdown.sh");
        }

        private void Tomcat_LogStart()
        {
            if (listBox1.SelectedItem.ToString() == "All")
            {
                for (int t = 0; t < listBox1.Items.Count; t++)
                {
                    if (!listBox1.Items[t].ToString().Equals("All"))
                    {
                        switch (t)
                        {
                            case 1:
                                cSSH_TomCat_Log1 = Connect_SSH(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text);

                                Tomcat_Log_sShell1 = cSSH_TomCat_Log1.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

                                if (cSSH_TomCat_Log1.IsConnected)
                                {
                                    Tomcat_Log_thread1 = new Thread(() => recvLogSSHData(Tomcat_Log_sShell1, textBox10));

                                    Tomcat_Log_thread1.IsBackground = true;
                                    Tomcat_Log_thread1.Start();
                                }
                                break;
                            case 2:
                                cSSH_TomCat_Log2 = Connect_SSH(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text);

                                Tomcat_Log_sShell2 = cSSH_TomCat_Log2.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

                                if (cSSH_TomCat_Log2.IsConnected)
                                {
                                    Tomcat_Log_thread2 = new Thread(() => recvLogSSHData(Tomcat_Log_sShell2, textBox9));

                                    Tomcat_Log_thread2.IsBackground = true;
                                    Tomcat_Log_thread2.Start();
                                }
                                break;
                            case 3:
                                cSSH_TomCat_Log3 = Connect_SSH(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text);

                                Tomcat_Log_sShell3 = cSSH_TomCat_Log3.CreateShellStream("vt100", 80, 60, 800, 600, 65536);

                                if (cSSH_TomCat_Log3.IsConnected)
                                {
                                    Tomcat_Log_thread3 = new Thread(() => recvLogSSHData(Tomcat_Log_sShell3, textBox11));

                                    Tomcat_Log_thread3.IsBackground = true;
                                    Tomcat_Log_thread3.Start();
                                }
                                break;
                            default :
                                break;
                        }
                    }
                }
            }
        }
        private void Tomcat_LogCommad()
        {
            if (cSSH_TomCat_Log1.IsConnected)
            {
                Tomcat_Log_sShell1.Write("tail -f " + textBox7.Text + listBox1.Items[1].ToString() + "/logs/catalina.out \n");
                Tomcat_Log_sShell1.Flush();
            }

            if (cSSH_TomCat_Log2.IsConnected)
            {
                Tomcat_Log_sShell2.Write("tail -f " + textBox7.Text + listBox1.Items[2].ToString() + "/logs/catalina.out \n");
                Tomcat_Log_sShell2.Flush();
            }

            if (cSSH_TomCat_Log3.IsConnected)
            {
                Tomcat_Log_sShell3.Write("tail -f " + textBox7.Text + listBox1.Items[3].ToString() + "/logs/catalina.out \n");
                Tomcat_Log_sShell3.Flush();
            }
        }

        private void Tomcat_Service(string strComm)
        {
            if (listBox1.SelectedItem.ToString() == "All")
            {
                for (int t = 0; t < listBox1.Items.Count; t++)
                {
                    if (!listBox1.Items[t].ToString().Equals("All"))
                    {
                        Command_sShell.Write(textBox7.Text + listBox1.Items[t].ToString() + "/bin/" + strComm + " \n");
                        Command_sShell.Flush();
                    }
                }
            }
            else
            {
                Command_sShell.Write(textBox7.Text + listBox1.SelectedItem.ToString() + "/bin/" + strComm + " \n");
                Command_sShell.Flush();
            }
        }

        private void openFileDialog1_FileOk(object sender, CancelEventArgs e)
        {
            textBox8.Text = openFileDialog1.FileName;
        }

        private void textBox8_DoubleClick(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void textBox8_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
        }

        private void button7_Click(object sender, EventArgs e)
        {
            progressBar1.Minimum = 0;
            progressBar1.Maximum = 0;

            upLoad_Sftp();
        }

        private void upLoad_Sftp()
        {
            if (textBox8.Text == "")
            {
                MessageBox.Show("파일을 선택하세요");
            }
            else
            {
                if (listBox1.SelectedItem.ToString() == "All")
                {
                    for (int t = 0; t < listBox1.Items.Count; t++)
                    {
                        if (!listBox1.Items[t].ToString().Equals("All"))
                        {
                            string tomcat_doc = textBox7.Text.ToString() + listBox1.Items[t].ToString();
                            Sftp_thread = new Thread(() =>
                                Connect_Sftp(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text, tomcat_doc, textBox8.Text.ToString()));

                            Sftp_thread.IsBackground = true;
                            Sftp_thread.Start();
                        }
                    }
                }
                else
                {
                    string tomcat_doc = textBox7.Text.ToString() + listBox1.SelectedItem.ToString();

                    Sftp_thread = new Thread(() =>
                                Connect_Sftp(textBox1.Text, Convert.ToInt32(textBox2.Text), textBox3.Text, textBox4.Text, tomcat_doc, textBox8.Text.ToString()));

                    Sftp_thread.IsBackground = true;
                    Sftp_thread.Start();
                }
            }
        }

        private void Connect_Sftp(string host, int port, string user, string passwd, string tomcat_doc, string filePath)
        {
            SftpClient sFtp = new SftpClient(host, port, user, passwd);
            FileStream fStream = null;

            try
            {
                sFtp.ConnectionInfo.Timeout = TimeSpan.FromSeconds(120);

                sFtp.Connect();

                sFtp.ChangeDirectory(tomcat_doc + "/webapps/");

                if (sFtp.Exists(Path.GetFileName(filePath)))
                {
                    sFtp.Delete(Path.GetFileName(filePath));
                }

                fStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                progressBar1.Invoke((MethodInvoker)delegate { progressBar1.Maximum = (int)fStream.Length; });

                sFtp.BufferSize = 4 * 1024;

                sFtp.UploadFile(fStream, Path.GetFileName(filePath), UpdateProgresBar);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                this.Invoke((MethodInvoker)delegate { label15.Text = "파일업로드 오류"; });
                sFtp.Disconnect();
                fStream.Close();
            }
            finally
            {
                sFtp.Disconnect();
                fStream.Close();
            }
        }

        private void UpdateProgresBar(ulong uploaded)
        {
            progressBar1.Invoke((MethodInvoker)delegate { progressBar1.Value = (int)uploaded; });
        }

        private void button8_Click(object sender, EventArgs e)
        {
            button3.PerformClick();
            Tomcat_LogCommad();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            textBox9.Text = "";
            textBox10.Text = "";
            textBox11.Text = "";
        }
    }
}
