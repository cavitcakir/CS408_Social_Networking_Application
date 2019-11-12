using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace server
{
    public partial class Form1 : Form
    {
        Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Dictionary<string, Socket> clientSocketsDictionary = new Dictionary<string, Socket>(); // keeps (username->client) tuples
        List<string> connectedNames = new List<string>(); // keeps names of connected users
        List<string> registeredUsers = new List<string>(); // keeps names of registered users

        List<Tuple<string,string>> friendRequests = new List<Tuple<string, string>>(); // (inviter,invitee)
        List<Tuple<string, string>> notification_sent = new List<Tuple<string, string>>(); // (inviter,invitee)
        List<Tuple<string, string>> notification_approve = new List<Tuple<string, string>>(); // (invitee,inviter)
        List<Tuple<string, string>> notification_rejected = new List<Tuple<string, string>>(); // (invitee,inviter)
        List<Tuple<string, string>> friendDatabase = new List<Tuple<string, string>>(); // (inviter,invitee)


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
                            textBox_logs.ScrollToCaret();
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
                            textBox_logs.ScrollToCaret();
                            send_message(newClient, "already connected");
                            newClient.Close();
                        }
                    }
                    else{
                        textBox_logs.AppendText(name + " is trying to connect but not registered\n");
                        textBox_logs.ScrollToCaret();
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
                        textBox_logs.ScrollToCaret();
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
            catch (Exception ex)
            {
                textBox_logs.AppendText("Fail: " + ex.ToString() + "\n");
                textBox_logs.ScrollToCaret();
                throw;
            }
        }

        private void SendNotification()
        {
            while (!terminating)
            {
                //List<Tuple<string, string>> friendRequests = new List<Tuple<string, string>>(); // (inviter,invitee)
                //List<Tuple<string, string>> notification_approve = new List<Tuple<string, string>>(); // (invitee,inviter)
                //List<Tuple<string, string>> notification_rejected = new List<Tuple<string, string>>(); // (invitee,inviter)
                //Socket inviteeSocket = clientSocketsDictionary[invitee];
                //Socket inviterSocket = clientSocketsDictionary[inviter];
                if (friendRequests.Count != 0)
                {
                    for (int i = 0; i < friendRequests.Count; i++)
                    {
                        var newInvite = friendRequests[i];
                        string inviter = newInvite.Item1;
                        string invitee = newInvite.Item2;
                        if (clientSocketsDictionary.ContainsKey(invitee) && !notification_sent.Contains(newInvite))
                        {
                            textBox_logs.AppendText(inviter + invitee + "\n");
                            Thread.Sleep(5000);
                            Socket inviteeSocket = clientSocketsDictionary[invitee];
                            send_message(inviteeSocket, "R-Q-S-T-D-SEC-KEY" + inviter);
                            notification_sent.Add(Tuple.Create(inviter,invitee));
                        }
                    }
                }
                if (notification_approve.Count != 0)
                {
                    for (int i = 0; i < notification_approve.Count; i++)
                    { 
                        var newNotification = notification_approve[i];
                        string inviter = newNotification.Item2;
                        string invitee = newNotification.Item1;
                        if (clientSocketsDictionary.ContainsKey(inviter))
                        {
                            Thread.Sleep(1000);
                            Socket inviterSocket = clientSocketsDictionary[inviter];
                            send_message(inviterSocket, "A-C-P-T-D-SEC-KEY" + invitee);
                            notification_approve.Remove(newNotification);
                            //notification_sent.Remove(Tuple.Create(inviter, invitee));
                            friendRequests.Remove(Tuple.Create(inviter, invitee));
                        }
                    }
                }
                if (notification_rejected.Count != 0)
                {
                    for (int i = 0; i < notification_rejected.Count; i++)
                    {
                        var newNotification = notification_approve[i];
                        string inviter = newNotification.Item2;
                        string invitee = newNotification.Item1;
                        if (clientSocketsDictionary.ContainsKey(inviter))
                        {
                            Thread.Sleep(1000);
                            Socket inviterSocket = clientSocketsDictionary[inviter];
                            send_message(inviterSocket, "R-J-C-T-D-SEC-KEY" + invitee);
                            notification_rejected.Remove(newNotification);
                            //notification_sent.Remove(Tuple.Create(inviter, invitee));
                            friendRequests.Remove(Tuple.Create(inviter, invitee));
                        }
                    }
                }
            }
        }
        private void Receive()
        {
            string name = connectedNames[connectedNames.Count() - 1]; // we got the username
            Socket thisClient = clientSocketsDictionary[name]; // we got the socket that related to the username
            bool connected = true;
            bool flag = false;
            while (connected && !terminating)
            {
                try
                {
                    string incomingMessage = receiveOneMessage(thisClient); // if there are any messages we take it
                    if (incomingMessage == "D-I-S-C-O-N-N-E-C-T-E-D-SEC-KEY") 
                    {
                        connected = false;
                        textBox_logs.AppendText(name + " has disconnected\n");
                        textBox_logs.ScrollToCaret();
                    }
                    else if (incomingMessage.Contains("I-N-V-SEC-KEY"))
                    {
                        string invitee = incomingMessage.Substring(13);
                        string inviter = name;
                        if(invitee == inviter)
                        {
                            send_message(thisClient, "You cannot be friend with yourself :(\n");
                            textBox_logs.AppendText(name + " trying to add himself :( " + "\n");
                            textBox_logs.ScrollToCaret();
                        }
                        else if (friendDatabase.Contains(Tuple.Create(inviter, invitee)) || friendDatabase.Contains(Tuple.Create(invitee, inviter)))
                        {
                            send_message(thisClient, "You are already friends with " + inviter+ "\n");
                            textBox_logs.AppendText(name + " trying to add existing friend " + invitee + "\n");
                            textBox_logs.ScrollToCaret();
                        }
                        else
                        {
                            friendRequests.Add(Tuple.Create(inviter, invitee));
                            textBox_logs.AppendText(name + " send friend request to "+ invitee +"\n");
                            textBox_logs.ScrollToCaret();
                        }
                    }
                    else if (incomingMessage.Contains("A-C-P-T-SEC-KEY"))
                    {
                        string inviter = incomingMessage.Substring(15);
                        string invitee = name;
                        notification_approve.Add(Tuple.Create(invitee, inviter));
                        friendDatabase.Add(Tuple.Create(inviter, invitee));
                        textBox_logs.AppendText(name + " accepted friend request of " + inviter + "\n");
                        textBox_logs.ScrollToCaret();
                    }
                    else if (incomingMessage.Contains("R-J-C-T-SEC-KEY"))
                    {
                        string inviter = incomingMessage.Substring(15);
                        string invitee = name;
                        notification_rejected.Add(Tuple.Create(invitee, inviter));
                        textBox_logs.AppendText(name + " rejected friend request of " + inviter + "\n");
                        textBox_logs.ScrollToCaret();
                    }
                    else
                    {
                        textBox_logs.AppendText(name + ": " + incomingMessage + "\n"); // append it to our log
                        textBox_logs.ScrollToCaret();
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
                    flag = true;
                    foreach (string clientName in connectedNames)
                    {
                        if (clientName != name) // check for to don't send it to sender client
                        {
                            Socket tempSocket = clientSocketsDictionary[clientName]; // we got the socket
                            send_message(tempSocket, (name + " has disconnected\n"));
                        }
                    }
                    textBox_logs.AppendText(name +" has disconnected\n");
                    textBox_logs.ScrollToCaret();
                    thisClient.Close();
                    connectedNames.Remove(name);
                    clientSocketsDictionary.Remove(name);
                    connected = false;
                }
            }
            if (!connected && !flag)
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
                if (serverPort <= 65535 && serverPort>=0)
                {
                    try
                    {
                        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);
                        serverSocket.Bind(endPoint);
                        serverSocket.Listen(300);
                    }
                    catch (Exception ex)
                    {
                        textBox_logs.AppendText("Fail: " + ex.ToString() + "\n");
                        textBox_logs.ScrollToCaret();
                    }

                    listening = true;
                    button_listen.Enabled = false;
                    button_listen.Text = "Listening";
                    button_listen.BackColor = Color.Green;

                    Thread acceptThread = new Thread(Accept);
                    acceptThread.Start();

                    Thread sendNotification = new Thread(SendNotification);
                    sendNotification.Start();

                    textBox_logs.AppendText("Started listening on port: " + serverPort + "\n");
                    textBox_logs.ScrollToCaret();
                }
                else
                {
                    textBox_logs.AppendText("Port number should be between 0 and 65535\n");
                    textBox_logs.ScrollToCaret();
                }
            }
            else
            {
                textBox_logs.AppendText("Please check port number \n");
                textBox_logs.ScrollToCaret();
            }
        }
    }
}
