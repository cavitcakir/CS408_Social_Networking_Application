using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace server
{
    public partial class Form1 : Form
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        List<Socket> clientSockets = new List<Socket>();

        bool terminating = false;
        bool listening = false;

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }
        private void Form1_FormClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            listening = false;
            terminating = true;
            Environment.Exit(0);
        }
        private void Accept()
        {
            while (listening)
            {
                try
                {

                    Socket newClient = serverSocket.Accept();
                    Byte[] buffer = new Byte[64];
                    if (checkClient(newClient)){
                        buffer = Encoding.Default.GetBytes("authorized");
                        newClient.Send(buffer);
                        clientSockets.Add(newClient);
                        textBox_logs.AppendText("A client is connected.\n");

                        Thread receiveThread = new Thread(Receive);
                        receiveThread.Start();
                    }
                    else{
                        
                        buffer = Encoding.Default.GetBytes("not authorized");
                        newClient.Send(buffer);
                        newClient.Close();
                    }
                }
                catch
                {
                    if (terminating)
                    {
                        listening = false;
                    }
                    else
                    {
                        textBox_logs.AppendText("The socket stopped working.\n");
                    }

                }
            }
        }

        private bool checkClient(Socket thisClient)
        {
            try
            {
                Byte[] buffer = new Byte[64];
                thisClient.Receive(buffer);

                string incomingMessage = Encoding.Default.GetString(buffer);
                incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                if (incomingMessage == "cavit" || incomingMessage == "ceren")
                {
                    return true;
                }
                else
                {
                    textBox_logs.AppendText(incomingMessage + " is faking.\n");
                    return false;
                }
            }
            catch
            {
                throw;
            }
        }
        private void Receive()
        {
            Socket thisClient = clientSockets[clientSockets.Count() - 1];
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                        Byte[] buffer = new Byte[64];
                        thisClient.Receive(buffer);

                        string incomingMessage = Encoding.Default.GetString(buffer);
                        incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
                    if(incomingMessage == "")
                    {
                        connected = false;
                        textBox_logs.AppendText("A client has disconnected\n");
                    }
                    else
                        textBox_logs.AppendText("Client: " + incomingMessage + "\n");
                        
                }
                catch
                {
                    if (!terminating || !connected)
                    {
                        textBox_logs.AppendText("A client has disconnected\n");
                    }
                    thisClient.Close();
                    clientSockets.Remove(thisClient);
                    connected = false;
                }
            } 
        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;

            if (Int32.TryParse(textBox_port.Text, out serverPort))
            {
                try
                {
                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                    serverSocket.Bind(endPoint);
                    serverSocket.Listen(300);
                }
                catch (Exception)
                {
                    throw;
                }
               
                listening = true;
                button_listen.Enabled = false;

                Thread acceptThread = new Thread(Accept);
                acceptThread.Start();

                textBox_logs.AppendText("Started listening on port: " + serverPort + "\n");

            }
            else
            {
                textBox_logs.AppendText("Please check port number \n");
            }
        }
    }
}
