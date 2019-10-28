using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
        Dictionary<string, Socket> clientSocketsDictionary = new Dictionary<string, Socket>();
        List<string> connectedNames = new List<string>();
        List<string> registeredUsers = new List<string>();


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

        private void readFile()
        {
            string line;
            var path = Path.Combine(Directory.GetCurrentDirectory() ,"user_db.txt");
            System.IO.StreamReader file =new  System.IO.StreamReader(path);
            while((line = file.ReadLine()) != null)
            {
                registeredUsers.Add(line);
            }
            file.Close();
        }
        private void send_message(Socket clientSocket,string message)
        {
            Byte[] buffer = new Byte[64];
            buffer = Encoding.Default.GetBytes(message);
            clientSocket.Send(buffer);
        }
        private string receiveOneMessage(Socket clientSocket) // this function receives only one message
        {
            Byte[] buffer = new Byte[64];
            clientSocket.Receive(buffer);
            string incomingMessage = Encoding.Default.GetString(buffer);
            incomingMessage = incomingMessage.Substring(0, incomingMessage.IndexOf("\0"));
            return incomingMessage;
        }

        private void Accept()
        {
            while (listening)
            {
                try
                {
                    string name ="";
                    Socket newClient = serverSocket.Accept();
                    if (checkClient(newClient,ref name)){
                        if (!clientSocketsDictionary.ContainsKey(name))
                        {
                            send_message(newClient, "authorized\n");
                            clientSocketsDictionary.Add(name, newClient);
                            connectedNames.Add(name);
                            textBox_logs.AppendText(name + " is connected.\n");

                            Thread receiveThread = new Thread(Receive);
                            receiveThread.Start();
                        }
                        else
                        {
                            textBox_logs.AppendText(name + " is trying to connect again\n");
                            send_message(newClient, "already connected");
                            newClient.Close();
                        }
                    }
                    else{
                        textBox_logs.AppendText(name + " is trying to connect but not registered\n");
                        send_message(newClient, "not authorized");
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

        private bool checkClient(Socket thisClient, ref string name)
        {
            try
            {
                string incomingMessage = receiveOneMessage(thisClient);

                if (registeredUsers.Contains(incomingMessage))
                {
                    name = incomingMessage;
                    return true;
                }
                else
                {
                    name = incomingMessage;
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
            string name = connectedNames[connectedNames.Count() - 1];
            Socket thisClient = clientSocketsDictionary[name];
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    string incomingMessage = receiveOneMessage(thisClient);
                    if (incomingMessage == "")
                    {
                        connected = false;
                        textBox_logs.AppendText(name + " has disconnected\n");
                    }
                    else
                    {
                        textBox_logs.AppendText(name + ": " + incomingMessage + "\n");
                        foreach (string clientName in connectedNames)
                        {
                            if (clientName != name)
                            {
                                Socket tempSocket = clientSocketsDictionary[clientName];
                                send_message(tempSocket, (name + ": "));
                                send_message(tempSocket, (incomingMessage + "\n"));
                            }
                        }
                    }
                }
                catch
                {
                    if (!terminating || !connected)
                    {
                        textBox_logs.AppendText(name +" has disconnected\n");
                    }
                    thisClient.Close();
                    connectedNames.Remove(name);
                    clientSocketsDictionary.Remove(name);
                    connected = false;
                }
            }
            if (!connected)
            {
                thisClient.Close();
                connectedNames.Remove(name);
                clientSocketsDictionary.Remove(name);
            }
        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;
            readFile();
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
