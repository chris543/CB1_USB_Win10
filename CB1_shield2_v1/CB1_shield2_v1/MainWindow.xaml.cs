using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading;
namespace CB1_shield2_v1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        byte[] mBuffer = new byte[25];
        bool mBufferRdy = true;
        int lastDataLength;
        SerialPort mSerialPort = new SerialPort();
        string[] baudRateList = { "4800", "9600", "14400", "19200", "28800", "38400", "57600", "115200" };
        public MainWindow()
        {
            InitializeComponent();
            string[] COMPort = System.IO.Ports.SerialPort.GetPortNames();
            if (COMPort == null)
            {
                MessageBox.Show("There are no COM port");
                this.Close();
            }
            else
            {
                BTN_portOpen.IsEnabled = true;
                BTN_portClose.IsEnabled = false;
                ConnectStatus.Text = "Disconnect";

            }
            cbb_comport.ItemsSource = COMPort;
            cbb_baudrate.ItemsSource = baudRateList;


        }

        private void cbb_comport_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //ConnectStatus.Text = (sender as ComboBox).SelectedItem as String;// cbb_comport.Text;
            //mSerialPort.PortName = cbb_comport.Text;
            mSerialPort.PortName = (sender as ComboBox).SelectedItem as String;
        }

        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //mSerialPort.BaudRate = Convert.ToInt16(cbb_baudrate.Text);
            mSerialPort.BaudRate = Convert.ToInt32((sender as ComboBox).SelectedItem as String);

        }

        private void BTN_portClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                mSerialPort.Close();
                BTN_portOpen.IsEnabled = true;
                BTN_portClose.IsEnabled = false;
                ConnectStatus.Text = "Disconnect";

            }
            catch (Exception)
            {

                throw;
            }
        }

        private void BTN_portOpen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                mSerialPort.Handshake = Handshake.None;
                mSerialPort.Parity = Parity.None;
                mSerialPort.DataBits = 8;
                mSerialPort.StopBits = StopBits.One;
                mSerialPort.ReadTimeout = 200;
                mSerialPort.WriteTimeout = 50;
                mSerialPort.Open();

                mSerialPort.DataReceived += MSerialPort_DataReceived;
                BTN_portOpen.IsEnabled = false;
                BTN_portClose.IsEnabled = true;
                ConnectStatus.Text = cbb_comport.SelectedItem + " " + cbb_baudrate.SelectedItem + " Connected";

            }
            catch (Exception)
            {

                throw;
            }
        }

        private void MSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //var received_data = mSerialPort.ReadExisting();
            //mSerialPort.BytesToRead;
            //Debug.WriteLine(mSerialPort.BytesToRead);
            int bytes = mSerialPort.BytesToRead;
            byte[] buffer = new byte[bytes];
            mSerialPort.Read(buffer, 0, bytes);

            //Array.Copy(buffer, 0, mBuffer, 0, bytes);
            int last = Array.LastIndexOf<byte>(buffer, 0x55);
            Debug.WriteLine(bytes);
            Debug.WriteLine(last);
            Debug.WriteLine(BitConverter.ToString(buffer));

            if ((last == -1) & (mBufferRdy))
            {
                return;
            }
            if (!mBufferRdy)
            {
                Array.Copy(buffer, 0, mBuffer, lastDataLength, bytes);
                Debug.WriteLine("To execute " + BitConverter.ToString(mBuffer));
                Debug.WriteLine("---run---");

                passDataToExecute(mBuffer, bytes + lastDataLength);

                Array.Clear(mBuffer, 0, mBuffer.Length);
                mBufferRdy = true;

            }
            else if ((last + 1) == bytes)
            {
                Array.Copy(buffer, 0, mBuffer, 0, bytes);
                lastDataLength = bytes;
                Debug.WriteLine("Save last data");
                mBufferRdy = false;
                return;
            }
            else if (bytes < (last + buffer[last + 1]+3))
            {
                Array.Copy(buffer, 0, mBuffer, 0, bytes);
                lastDataLength = bytes;
                Debug.WriteLine("Save last data");
                mBufferRdy = false;
                return;
            }
            else
            {
                Debug.WriteLine("---run---");
                passDataToExecute(buffer, bytes);
            }

        }
        private void passDataToExecute(byte[] data, int datalength)
        {
            for (int i = 0; i < datalength; i++)
            {
                if (data[i] == 0x55)
                {
                    byte lsb, hsb;
                    int functionCodeDataLength = 0;
                    byte functionCode;

                    functionCode = data[i + 2];
                    switch (functionCode)
                    {
                        case 0xEB:
                            functionCodeDataLength = 6;

                            byte[] executeData = new byte[functionCodeDataLength];
                            Array.Copy(data, i, executeData, 0, functionCodeDataLength);

                            if (executeData[executeData.Length - 1] == (byte)calculateCheckSum(executeData))
                            {
                                Debug.WriteLine("Action light sensor 0xEB");

                                lsb = executeData[3];
                                hsb = executeData[4];
                                int lux = (((hsb & 0xFF) << 8) | (lsb & 0xff));
                                Debug.WriteLine(lux);
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    TBL_luxValue.Text = lux.ToString();

                                }));
                                i += functionCodeDataLength - 1;
                            }
                            break;
                        case 0xE8:
                            functionCodeDataLength = 5;

                            Debug.WriteLine("Action button 0xE8");

                            i += functionCodeDataLength - 1;
                            break;
                        case 0xF0:
                            functionCodeDataLength = 8;

                            byte[] ultraExecuteData = new byte[functionCodeDataLength];
                            Array.Copy(data, i, ultraExecuteData, 0, functionCodeDataLength);

                            Debug.WriteLine("Action ultrasonic 0xF0 0x80");
                            if (ultraExecuteData[3] == 0x80)
                            {
                                lsb = ultraExecuteData[4];
                                hsb = ultraExecuteData[5];
                                byte cs = (byte)((hsb << 7) + lsb);
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    TBL_ultrasonicValue.Text = cs.ToString();

                                }));
                            }

                            break;
                    }
                    //Debug.WriteLine("Hex: {0:X}",functionCode);
                    //check checksum first
                    //Debug.WriteLine("Execute Action");
                }
            }
        }
        private void Execute_Action()
        {
            CheckData_isValid();
        }

        private void CheckData_isValid()
        {

        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            TBL_motor0.Text = SLD_motor0Silder.Value.ToString();
            //try
            //{
            //    mSerialPort.Write("hello");
            //}
            //catch (Exception)
            //{

            //    throw;
            //}
        }

        private void SLD_motor0Silder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //byte[] data = { 0x55, 0x23 };
            byte LSB = 0, HSB = 0;
            byte[] data = { 0x77, 0x03, 0xE3, LSB, HSB, 0x00 };

            data[3] = (byte)(Convert.ToByte(SLD_motor0Silder.Value) & 0x7F);
            data[4] = (byte)(Convert.ToByte(SLD_motor0Silder.Value) >> 7);
            data[data.Length - 1] = (byte)calculateCheckSum(data);


            mSerialPort.Write(data, 0, data.Length);
        }

        private int calculateCheckSum(byte[] data)
        {
            int checkSum = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                checkSum = checkSum + data[i];
            }
            return checkSum;
        }
    }
}
