using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ipkpd;

public class Tcp
{
    private const int BufSize = 8 * 1024;
    private const char Lf = (char)10;
    private State _state;
    private AsyncCallback? _recv;
    private NetworkStream? _stream;
    public bool ClientInitiatedExit;
    //public ArrayPool<Byte> BufferPool = ArrayPool<Byte>.Create();

    bool _listening = true;


    private Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    //private Socket _socket;
    private Socket _handler;
    static IPEndPoint _sender = new IPEndPoint(IPAddress.Any, 0);
    private EndPoint _epFrom = _sender;

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

   

    public void ListenTcp()
    {
        for(;;)
        {
            _state = new State();
            _handler =  _socket.Accept();
            Console.WriteLine("connected");

            _ = _handler.BeginReceiveFrom(_state.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv = ar =>
            {
                var so = ar.AsyncState as State;
                var bytes = _handler.EndReceiveFrom(ar, ref _epFrom);    //TODO add check for 0

                if (so == null) return;
                var message = Encoding.ASCII.GetString(so.Buffer, 0, bytes);

                Console.WriteLine(message);
                var data = Encoding.ASCII.GetBytes("replay");
                var bytesResponse = new byte[data.Length];
                //handler.Send(bytesResponse, SocketFlags.None);
                _handler.SendAsync(data, SocketFlags.None);
                if (message.Trim() is "BYE")
                {
                    if (ClientInitiatedExit)
                    {
                        ClientInitiatedExit = true;
                        //Environment.Exit(0);
                    }
                    else
                    {
                        SendTcp("BYE");
                        ClientInitiatedExit = true;
                        //Environment.Exit(0);
                    }
                }
                else if (message.Trim().StartsWith("SOLVE "))
                {
                    Evaluator eval = new Evaluator();
                    var result = eval.Evaluate(message.Trim().Substring(5, message.Length-5));
                }


                if (ClientInitiatedExit == false) _handler.BeginReceiveFrom(so.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv, so);
            }, _state);
            
        }
    }

    public void SendTcp(string message)
    {
        try
        {
            if (message.Trim() is "BYE") ClientInitiatedExit = true;
            var data = Encoding.ASCII.GetBytes(message + Lf);
            _stream?.Write(data, 0, data.Length);
        }
        catch (ArgumentNullException e)
        {
            Console.WriteLine("ArgumentNullException: {0}", e);
        }
    }

    public class State
    {
        public byte[] Buffer = new byte[BufSize];
    }
}