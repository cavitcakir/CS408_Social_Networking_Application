using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        Socket clientSocket;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void button_connect_Click(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_IP.Text;
            int portNum;
            string name = textBox_Name.Text;


            if (name != "" && name.Length <= 64)
            {
                if (Int32.TryParse(textBox_Port.Text, out portNum))
                {
                    try
                    {

                        clientSocket.Connect(IP, portNum);
                        RequestSend();
                        if (isAuthorized())
                        {
                            button_connect.Enabled = false;
                            connected = true;
                            logBox.AppendText("Connection established...\n");

                            Thread receiveThread = new Thread(Receive);
                            receiveThread.Start();
                        }
                        else
                        {
                            logBox.AppendText("Check your username.\n");
                        }

                    }
                    catch
                    {
                        logBox.AppendText("Could not connect to the server!\n");
                    }
                    

                }
                else
                {
                    logBox.AppendText("Check the port\n");
                }
            }
            else
            {
                logBox.AppendText("Check the name\n");
            }
        }

        private bool isAuthorized()
        {
            Byte[] buffer = new Byte[64];
            clientSocket.Receive(buffer);
            string incomingMessage = Encoding.Default.GetString(buffer);
            incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
            
            if (incomingMessage != "not authorized")
            {
                return true;
            }
            else
                return false;
        }
        private void RequestSend()
        {
            string name = textBox_Name.Text;
            Byte[] buffer = new Byte[64];
            buffer = Encoding.Default.GetBytes(name);
            clientSocket.Send(buffer);

        }
        private void Receive()
        {

            try
            {
                Byte[] buffer = new Byte[64];
                clientSocket.Receive(buffer);

                string incomingMessage = Encoding.Default.GetString(buffer);
                incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));

                logBox.AppendText("Server: " + incomingMessage + "\n");

            }
            catch
            {
                if (!terminating)
                {
                    logBox.AppendText("The server has disconnected\n");
                    button_connect.Enabled = true;
                }

                clientSocket.Close();
                connected = false;
            }

        }
    }

}
