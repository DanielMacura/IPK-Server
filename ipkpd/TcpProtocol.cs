using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;


namespace ipkcpd;

public class TcpProtocol : IProtocolInterface
{
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private bool _listening = true;
    private const char Lf = (char)10;
    private List<Socket?> _clientList = new List<Socket?>();

    public async void Listen(string address, int port)
    {
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        _socket.Listen();
        while (_listening)
        {
            try
            {
                Console.WriteLine("Connecting");

                var client = await Task.Factory.FromAsync<Socket>(_socket.BeginAccept, _socket.EndAccept, null);
                Console.WriteLine("Connecting");
                _clientList.Add(client);

                AcceptClient(client);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

    }
    public void Stop()
    {
        _listening = false;
        foreach (var client in _clientList.Where(client => client != null))
        {
            if (!client!.Connected) continue;
            Console.WriteLine("Client connected");
            var bytes = Encoding.ASCII.GetBytes("BYE" + Lf);
            client.Send(bytes);
        }
        Thread.Sleep(500);
        Environment.Exit(0);
    }

    private enum ClientStates
    {
        Connected,
        Greeted,
        Disconnecting
    }


    private async void AcceptClient(Socket client)
    {
        Console.WriteLine("Connected");
        var buffer = BufferPool.Instance.Checkout();
        var clientState = ClientStates.Connected;
        try
        {
            await using var ns = new NetworkStream(client, true);
            while (_listening && client.Connected)
            {
                try
                {
                    var count = await ns.ReadAsync(buffer.Array.AsMemory(buffer.Offset, buffer.Count));
                    if (count == 0)
                    {
                        // Client disconnected normally.
                        Console.WriteLine("Client left");
                        break;
                    }
                    else
                    {
                        var error = OnDataRead(new ArraySegment<byte>(buffer.Array, buffer.Offset, count), ns, ref clientState);
                        if (error == true)
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
        }
        finally
        {
            BufferPool.Instance.CheckIn(buffer);
        }
    }

    private static bool OnDataRead(ArraySegment<byte> arraySegment, NetworkStream ns, ref ClientStates clientState)
    {
        var bytes = arraySegment.ToArray();
        var text = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        var result = "Incorrect solve";

        if (!text.EndsWith(Lf))
        {
            Console.Error.WriteLine("Client message not well formed, must end with Lf. Terminating connection...");
            SendReply(ns, "BYE" + Lf);
            clientState = ClientStates.Disconnecting;
            return true;    //error
        }

        if (text == "HELLO" + Lf)
        {
            if (clientState == ClientStates.Connected)
            {
                clientState = ClientStates.Greeted;
                Console.WriteLine("Client greeted.");

                SendReply(ns, "HELLO" + Lf);
                return false;   //no error
            }
            if (clientState != ClientStates.Connected)
            {
                Console.WriteLine("Client greeted in incorrect connection state.");
                SendReply(ns, "BYE" + Lf);
                clientState = ClientStates.Disconnecting;
                return true;    //error
            }
        }

        if (text.StartsWith("SOLVE "))
        {
            if (clientState == ClientStates.Greeted)
            {
                Console.WriteLine(text);
                var eval = new Evaluator();
                var problem = text[5..text.Trim().Length];

                result = eval.Evaluate(problem).ToString();
                Console.WriteLine(result);
                if (result != "")
                {
                    SendReply(ns, "RESULT "+ result.Trim() + Lf);
                    return false;   //no error
                }
                else
                {
                    SendReply(ns, "BYE" + Lf);
                    return true;   //error
                }
            }
            else
            {
                Console.WriteLine("Client asked for SOLVE in incorrect connection state.");
                SendReply(ns, "BYE" + Lf);
                clientState = ClientStates.Disconnecting;
                return true;    //error
            }

        }
        if (text == "BYE" + Lf)
        {
            SendReply(ns, "BYE" + Lf);
            clientState = ClientStates.Disconnecting;
            return true;    //error
        }

        Console.WriteLine("Client did not use IPK Calculator Protocol.");
        Console.WriteLine(result);

        SendReply(ns, "BYE" + Lf);
        clientState = ClientStates.Disconnecting;
        return true;   //error
    }

    private static void SendReply(Stream ns, string message)
    {
        var responseBytes = Encoding.ASCII.GetBytes(message);
        ns.Write(responseBytes);
        ns.Flush();
    }
}