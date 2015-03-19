using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;
using System.Management.Instrumentation;
using System.Collections.Specialized;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Timers;


namespace Arduino_Fan_Controller
{
    public partial class Form1 : Form
    {
        NotifyIcon taskbarIcon;

        Icon normalIcon;
        Icon errorIcon;

        Thread temperatureSensorWorkerThread;
        Thread arduinoCommsWorkerThread;

        UInt64 currentCPUtempInt = 0;
        string CPUtempString = "";

        int timerCounter = 0;

        MenuItem progNameMenuItem = new MenuItem("Arduino Fan Controller tray application");
        MenuItem quitMenuItem = new MenuItem("Quit");
        MenuItem currentCPUtemp = new MenuItem("Current CPU temp: 0");
        MenuItem arduinoCommsStatus = new MenuItem("Arduino Comm Status: Not OK");

        SerialPort arduinoSerialPort = new SerialPort("COM200", 115200);

        private static System.Timers.Timer aTimer;

        public Form1()
        {
            InitializeComponent();

            // load icons
            normalIcon = new Icon("goodtemp.ico");
            errorIcon = new Icon("badtemp.ico");

            // create icons
            taskbarIcon = new NotifyIcon();
            taskbarIcon.Icon = errorIcon;

            //show in taskbar
            taskbarIcon.Visible = true;

            // create contect menu for icon
            currentCPUtemp.Text = "Current CPU temp: " + Convert.ToString(currentCPUtempInt);
            ContextMenu contextmenu = new ContextMenu();
            contextmenu.MenuItems.Add(progNameMenuItem);
            contextmenu.MenuItems.Add(currentCPUtemp);
            contextmenu.MenuItems.Add(arduinoCommsStatus);
            contextmenu.MenuItems.Add(quitMenuItem);
            taskbarIcon.ContextMenu = contextmenu;

            // wire up quit button
            quitMenuItem.Click += quitMenuItem_Click;


            // hide the form
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;

            //stat worker threads
            temperatureSensorWorkerThread = new Thread(new ThreadStart(tempSensorThread));
            arduinoCommsWorkerThread = new Thread(new ThreadStart(arduinoCommsThread));
            temperatureSensorWorkerThread.Start();

            arduinoCommsWorkerThread.Start();

            aTimer = new System.Timers.Timer(1000);
            aTimer.Elapsed += CheckForActiveArduino;
            aTimer.Enabled = true;

        }

        void quitMenuItem_Click(object sender, EventArgs e)
        {
            temperatureSensorWorkerThread.Abort();
            arduinoCommsWorkerThread.Abort();
            taskbarIcon.Dispose();
            arduinoSerialPort.Dispose();
            this.Close();
        }

        public void tempSensorThread()
        {
            ManagementClass sensorDataClass = new ManagementClass(new ManagementScope("\\\\Andrew-XX\\root\\OpenHardwareMonitor"), new ManagementPath("Sensor"), null);
            try
            {
                while (true)
                {
                    ManagementObjectCollection sensorDataClassCollection = sensorDataClass.GetInstances();
                    foreach (ManagementObject obj in sensorDataClassCollection)
                    {
                        if (obj["identifier"].ToString() == "/intelcpu/0/temperature/6")
                        {
                            currentCPUtempInt = Convert.ToUInt64(obj["Value"]);
                        }
                    }
                    currentCPUtemp.Text = "Current CPU temp: " + Convert.ToString(currentCPUtempInt);
                    CPUtempString = Convert.ToString(currentCPUtempInt);
                    Thread.Sleep(500);
                }
            }
            catch (ThreadAbortException tbe)
            {
                sensorDataClass.Dispose();
            }
        }

        public void arduinoCommsThread()
        {
            try
            {
                char character1 = 'x';
                arduinoSerialPort.Open();
                string readString = "";
                string CPUTEMP = CPUtempString;
                while (true)
                {
                    if (timerCounter > 6)
                    {
                        arduinoCommsStatus.Text = "Arduino Comm Status: Not OK";
                        taskbarIcon.Icon = errorIcon;
                    }
                    try
                    {
                        if (arduinoSerialPort.BytesToRead > 0)
                        {
                            character1 = (char)arduinoSerialPort.ReadByte();
                            readString += character1;
                        }
                    }
                    catch (IOException ex)
                    {
                        // do nothing
                    }

                    if (character1 == '>')
                    {
                        if (String.Concat(CPUTEMP, ">") == readString)
                        {
                            arduinoCommsStatus.Text = "Arduino Comm Status: OK";
                            taskbarIcon.Icon = normalIcon;
                            timerCounter = 0;
                        }
                        else
                        {
                            arduinoCommsStatus.Text = "Arduino Comm Status: Not OK";
                            taskbarIcon.Icon = errorIcon;
                        }


                        readString = "";
                        character1 = 'x';
                        CPUTEMP = CPUtempString;
                        arduinoSerialPort.Write(CPUTEMP);
                        arduinoSerialPort.Write(">");
                    }

                    Thread.Sleep(100);
                }

            }
            catch (IOException ex)
            {
                arduinoSerialPort.Dispose();
            }
        }

        public void CheckForActiveArduino(Object source, ElapsedEventArgs e)
        {
            timerCounter++;
        }
    }
}
