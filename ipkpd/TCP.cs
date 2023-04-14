using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ipkcpd;

public class Tcp
{
    private const char Lf = (char)10;
    private bool _listening = true;
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    public async void Listen(string address, int port)
    {
        // ...
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        _socket.Listen();
        while (_listening)
        {
            try
            {
                Console.WriteLine("Connecting");

                var client = await Task.Factory.FromAsync<Socket>(_socket.BeginAccept, _socket.EndAccept, null);

                AcceptClient(client);
            }
            catch (Exception e)
            {
                // Log etc.
                Console.Error.WriteLine(e);
            }
        }
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
                        OnDataRead(new ArraySegment<byte>(buffer.Array, buffer.Offset, count), ns, ref clientState);
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

    private void OnDataRead(ArraySegment<byte> arraySegment, NetworkStream ns, ref ClientStates clientState)
    {
        var bytes = arraySegment.ToArray();
        var text = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        Console.WriteLine(text+"LF");
        var result = "Incorrect solve";
        if (text.Trim() == "HELLO")
        {
            if (clientState == ClientStates.Connected)
            {
                clientState = ClientStates.Greeted;
            }
            else if (clientState != ClientStates.Connected)
            {
                Console.WriteLine("Client greeted in incorrect connection state.");

            }
        }

        if (text.Trim().StartsWith("SOLVE "))
        {
            Console.WriteLine(text.Trim());
            Evaluator eval = new Evaluator();
            var problem = text.Trim()[5..text.Trim().Length];

            result = eval.Evaluate(problem).ToString();
            Console.WriteLine(result);

        }
        Console.WriteLine(result);

        var retBytes = Encoding.ASCII.GetBytes(result);
        ns.Write(retBytes);
        ns.Flush();

    }
}