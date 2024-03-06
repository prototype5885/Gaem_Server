using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
public static class Authentication
{
    public static async Task HandleConnectingClients()
    {
        try
        {
            Byte[] buffer = new Byte[1024];
            int bytesRead;
            while (true)
            {
                TcpClient tcpClient = await Server.tcpListener.AcceptTcpClientAsync(); // waits for a new client to join
                NetworkStream stream = tcpClient.GetStream(); // gets the stream after a new client is connected

                bytesRead = await stream.ReadAsync(new ArraySegment<byte>(buffer)); // reads the bytes the client sent
                CalculateLatency.receivedBytesPerSecond += bytesRead;
                Packet[] packets = PacketProcessor.ProcessBuffer(buffer, bytesRead); // Processes the received packet

                Packet packet = packets[0]; // newly connected client is supposed to only send a single login data packet, so assuming there is only 1 packet received
                if (packet.type == 1) // makes sure its really a login data packet
                {
                    IPEndPoint clientAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint); // Gets the IP address of the new client

                    LoginData loginData = JsonSerializer.Deserialize(packet.data, LoginDataContext.Default.LoginData);

                    bool loginOrRegister = loginData.loginOrRegister; // True if client wants to login, false if client wants to register register
                    string username = loginData.username;
                    string hashedPassword = loginData.password;

                    AuthenticationResult authenticationResult = new AuthenticationResult(); // creates the thing that holds value for login/register result, db index, playername
                    if (loginOrRegister == false) // Runs if client wants to register
                    {
                        authenticationResult = Database.RegisterUser(username, hashedPassword);
                        if (authenticationResult.result == 1) // runs if registration was successful
                        {
                            authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                        }
                    }
                    else if (loginOrRegister == true) // Runs if client wants to login
                    {
                        authenticationResult = Database.LoginUser(username, hashedPassword, Server.connectedPlayers);
                    }

                    if (authenticationResult.result == 1)
                    {
                        for (byte index = 0; index < Server.maxPlayers; index++) // look for a free slot
                        {
                            if (Server.connectedPlayers[index] == null)
                            {
                                Server.connectedPlayers[index] = new ConnectedPlayer
                                {
                                    databaseID = authenticationResult.dbIndex,
                                    tcpClient = tcpClient,
                                    tcpStream = tcpClient.GetStream(),
                                    tcpEndpoint = clientAddress,
                                    ipAddress = clientAddress.Address,
                                    tcpPort = clientAddress.Port,
                                    cancellationTokenSource = new CancellationTokenSource()
                                };
                                Server.connectedPlayers[index].playerName = authenticationResult.playerName;

                                InitialData initialData = new InitialData
                                {
                                    lr = authenticationResult.result, // value represents how the server responded to login, like if success or not
                                    i = index, // client's assigned id
                                    mp = Server.maxPlayers, // max player amount
                                    tr = Server.tickrate
                                };

                                string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                                await PacketProcessor.SendTcp(1, jsonData, Server.connectedPlayers[index].tcpStream); // Type 1 means servers sends initial data to the new client

                                _ = PacketProcessor.ReceiveTcpData(Server.connectedPlayers[index]);
                                break;
                            }
                        }
                    }
                    else // login failed
                    {
                        InitialData initialData = new InitialData
                        {
                            lr = authenticationResult.result, // value represents how the server responded to login, like if success or not
                            i = 0, // client's assigned id
                            mp = 0, // max player amount
                            tr = 0
                        };

                        string jsonData = JsonSerializer.Serialize(initialData, InitialDataContext.Default.InitialData);
                        //await SendTcp(1, jsonData, stream); // Type 1 means servers sends initial data to the new client
                        await PacketProcessor.SendTcp(1, jsonData, stream);
                        Thread.Sleep(1000); // need to wait before closing else the player wont receive the "authentication failed" message
                        stream.Close();
                    }
                }
            }
        }
        catch
        {
            Console.WriteLine("Error while accepting new client");
        }
    }
    public static ConnectedPlayer CheckAuthenticationOfUdpClient(EndPoint udpEndpoint) // checks if udp package sender is an actual player
    {
        IPEndPoint udpIpEndpoint = udpEndpoint as IPEndPoint;
        if (udpIpEndpoint == null) return null;

        foreach (ConnectedPlayer player in Server.connectedPlayers)
        {
            if (player == null) continue;

            if (player.ipAddress.Equals(udpIpEndpoint.Address) && player.udpEndpoint == null) // checks if udp packet sender is authenticated player
            {
                if (player.udpPort == 0) // if authenticated but its the first package, get its port and assign udp endpoint to authenticated player
                {
                    player.udpPort = udpIpEndpoint.Port;
                    player.udpEndpoint = udpEndpoint;
                }
                return player;
            }
            else if (player.udpEndpoint.Equals(udpEndpoint))
            {
                return player;
            }
        }
        return null;
    }
}

