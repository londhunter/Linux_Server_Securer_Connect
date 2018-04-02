using System;
using System.Management;
using System.Management.Instrumentation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace Linux_Server_Securer_Connect
{
    public partial class Form2 : Form
    {
        private ManagementScope mScope;
        private Series sSin1;
        private Series sSin2;
        private System.Threading.Thread thread;

        private static decimal pTimeOld;
        private static decimal tStampOld;
        private static decimal pTimeNew;
        private static decimal tStampNew;

        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            pTimeOld = 0;
            tStampOld = 0;
            pTimeNew = 0;
            tStampNew = 0;

            chart1.Series.Clear();
            sSin1 = chart1.Series.Add("cpu");
            sSin1.ChartType = SeriesChartType.Pie;

            chart2.Series.Clear();
            sSin2 = chart2.Series.Add("mem");
            sSin2.ChartType = SeriesChartType.Pie;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                ConnectionOptions cConnectOption = new ConnectionOptions();
                cConnectOption.Username = textBox3.Text;
                cConnectOption.Password = textBox4.Text;

                mScope = new ManagementScope("\\\\" + textBox1.Text.ToString() + "\\root\\CIMV2", cConnectOption);
                mScope.Connect();

                if (mScope.IsConnected)
                {
                    label5.Text = "Status : Connected";

                    System.Threading.ThreadStart threadStart = new System.Threading.ThreadStart(recvSSHData);
                    thread = new System.Threading.Thread(threadStart);

                    thread.IsBackground = true;
                    thread.Start();
                }
                else
                {
                    label5.Text = "Status : disConnected";
                }
            }
            catch (Exception ex)
            {
                label5.Text = "Status : Error";
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void recvSSHData()
        {
            while (true)
            {
                try
                {
                    getCpuTime();
                    getOperatingInfo();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }

                System.Threading.Thread.Sleep(1000);
            }
        }

        private void getCpuTime()
        {
            try
            {
                decimal cpuTime = 0;

                ManagementPath mPath = new ManagementPath();
                mPath.RelativePath = "Win32_PerfRawData_PerfOS_Processor.Name='_Total'";

                ManagementObject mObject = new ManagementObject(mScope, mPath, null);
                mObject.Get();

                if (pTimeOld == 0 && tStampOld == 0)
                {
                    pTimeOld = Convert.ToDecimal(mObject.Properties["PercentProcessorTime"].Value);
                    tStampOld = Convert.ToDecimal(mObject.Properties["TimeStamp_Sys100NS"].Value);

                    pTimeNew = Convert.ToDecimal(mObject.Properties["PercentProcessorTime"].Value);
                    tStampNew = Convert.ToDecimal(mObject.Properties["TimeStamp_Sys100NS"].Value);

                    cpuTime = (1 - (pTimeNew / tStampNew)) * 100m;
                }
                else
                {
                    pTimeOld = pTimeNew;
                    tStampOld = tStampNew;

                    pTimeNew = Convert.ToDecimal(mObject.Properties["PercentProcessorTime"].Value);
                    tStampNew = Convert.ToDecimal(mObject.Properties["TimeStamp_Sys100NS"].Value);

                    cpuTime = (1 - ((pTimeNew - pTimeOld) / (tStampNew - tStampOld))) * 100m;
                }

                if (chart1.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate { UpdateCpuChart(cpuTime); });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void getOperatingInfo()
        {
            try
            {
                ObjectQuery mQuery = new ObjectQuery();
                mQuery.QueryString = "SELECT * FROM Win32_OperatingSystem";

                ManagementObjectSearcher mObjSearcher = new ManagementObjectSearcher(mScope, mQuery);

                foreach (ManagementObject mObject in mObjSearcher.Get())
                {
                    if (chart2.IsHandleCreated && listBox1.IsHandleCreated)
                    {
                        this.Invoke((MethodInvoker)delegate { UpdateOSInfo(mObject); });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private void UpdateOSInfo(ManagementObject mObject)
        {
            chart2.Series["mem"].Points.Clear();

            chart2.Series["mem"].Points.AddXY("여유", ((Convert.ToDecimal(mObject["FreePhysicalMemory"].ToString()) / Convert.ToDecimal(mObject["TotalVisibleMemorySize"].ToString())) * 100));
            chart2.Series["mem"].Points.AddXY("사용중", ((Convert.ToDecimal(mObject["TotalVisibleMemorySize"].ToString()) - Convert.ToDecimal(mObject["FreePhysicalMemory"].ToString())) / Convert.ToDecimal(mObject["TotalVisibleMemorySize"].ToString())) * 100);

            label10.Text = "전체 메모리 : " + (Convert.ToInt32(mObject["TotalVisibleMemorySize"].ToString()) / 1024).ToString();
            label11.Text = "여유 메모리 : " + (Convert.ToInt32(mObject["FreePhysicalMemory"].ToString()) / 1024).ToString();
            label12.Text = "사용 메모리 : " + ((Convert.ToInt32(mObject["TotalVisibleMemorySize"].ToString()) - Convert.ToInt32(mObject["FreePhysicalMemory"].ToString())) / 1024).ToString();

            if (listBox1.Items.Count == 0)
            {
                listBox1.Items.Add("OS : " + mObject["Caption"].ToString());
                listBox1.Items.Add("OS Version : " + mObject["Version"].ToString());
                listBox1.Items.Add("BuildNumber : " + mObject["BuildNumber"].ToString());
                listBox1.Items.Add("Server-Name : " + mObject["CSName"].ToString());
            }
        }

        private void UpdateCpuChart(decimal cpuTime)
        {
            chart1.Series["cpu"].Points.Clear();

            chart1.Series["cpu"].Points.AddXY("미사용", 100 - cpuTime);
            chart1.Series["cpu"].Points.AddXY("사용중", cpuTime);
        }
    }
}
