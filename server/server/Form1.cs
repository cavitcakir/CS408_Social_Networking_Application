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
        Dictionary<string, Socket> clientSocketsDictionary = new Dictionary<string, Socket>(); // keeps (username->client) tuples
        List<string> connectedNames = new List<string>(); // keeps names of connected users
        List<string> registeredUsers = new List<string>(); // keeps names of registered users

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
            var path = Path.Combine(Directory.GetCurrentDirectory() ,"user_db.txt"); // checks current directory to find user_db.txt
            System.IO.StreamReader file =new  System.IO.StreamReader(path);
            while((line = file.ReadLine()) != null)
            {
                registeredUsers.Add(line); // adds all the names to the registered users hashset
            }
            file.Close();
        }
        private void send_message(Socket clientSocket,string message) // takes socket and message then sends the message to that socket
        {
            Byte[] buffer = new Byte[64];
            buffer = Encoding.Default.GetBytes(message);
            clientSocket.Send(buffer);
        }
        private string receiveOneMessage(Socket clientSocket) // this function receives only one message and returns it
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
                    string name =""; // we initialize name to empty string
                    Socket newClient = serverSocket.Accept(); // first we accept the new connection request
                    if (checkClient(newClient,ref name)){ // gets the name and check if name is registered
                        if (!clientSocketsDictionary.ContainsKey(name)) // checks if the user already connected
                        {
                            send_message(newClient, "authorized\n");
                            clientSocketsDictionary.Add(name, newClient);
                            connectedNames.Add(name);
                            textBox_logs.AppendText(name + " is connected.\n");
                            foreach (string clientName in connectedNames)
                            {
                                if (clientName != name) // check for to don't send it to sender client
                                {
                                    Socket tempSocket = clientSocketsDictionary[clientName]; // we got the socket
                                    send_message(tempSocket, (name + " is connected\n"));
                                }
                            }

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

        private bool checkClient(Socket thisClient, ref string name) // gets the name of user and returns that users registiration status
        {
            try
            {
                string incomingMessage = receiveOneMessage(thisClient); // get the name

                if (registeredUsers.Contains(incomingMessage)) // check if name is registered
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
            string name = connectedNames[connectedNames.Count() - 1]; // we got the username
            Socket thisClient = clientSocketsDictionary[name]; // we got the socket that related to the username
            bool connected = true;

            while (connected && !terminating)
            {
                try
                {
                    string incomingMessage = receiveOneMessage(thisClient); // if there are any messages we take it
                    if (incomingMessage == "DISCONNECTED") 
                    {
                        connected = false;
                        textBox_logs.AppendText(name + " has disconnected\n");
                    }
                    else
                    {
                        textBox_logs.AppendText(name + ": " + incomingMessage + "\n"); // append it to our log
                        foreach (string clientName in connectedNames)
                        {
                            if (clientName != name) // check for to don't send it to sender client
                            {
                                Socket tempSocket = clientSocketsDictionary[clientName]; // we got the socket
                                send_message(tempSocket, (name + ": "));
                                send_message(tempSocket, (incomingMessage + "\n")); // send name and message
                            }
                        }
                    }
                }
                catch
                {
                    foreach (string clientName in connectedNames)
                    {
                        if (clientName != name) // check for to don't send it to sender client
                        {
                            Socket tempSocket = clientSocketsDictionary[clientName]; // we got the socket
                            send_message(tempSocket, (name + " has disconnected\n"));
                        }
                    }
                    textBox_logs.AppendText(name +" has disconnected\n");
                    thisClient.Close();
                    connectedNames.Remove(name);
                    clientSocketsDictionary.Remove(name);
                    connected = false;
                }
            }
            if (!connected)
            {
                foreach (string clientName in connectedNames)
                {
                    if (clientName != name) // check for to don't send it to sender client
                    {
                        Socket tempSocket = clientSocketsDictionary[clientName]; // we got the socket
                        send_message(tempSocket, (name + " has disconnected\n"));
                    }
                }
                thisClient.Close();
                connectedNames.Remove(name);
                clientSocketsDictionary.Remove(name);
            }
        }

        private void button_listen_Click(object sender, EventArgs e)
        {
            int serverPort;
            readFile(); // reads the database
            if (Int32.TryParse(textBox_port.Text, out serverPort)) // if we can parse the input port number
            {
                if (serverPort <= 65535)
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
                    textBox_logs.AppendText("Port number should be less than 65535\n");
                }
            }
            else
            {
                textBox_logs.AppendText("Please check port number \n");
            }
        }
    }
}
