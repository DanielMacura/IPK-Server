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
    private Socket handler;
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
            }
        }
    }

    private enum clientStates
    {
        Connected,
        Greeted,
        Disconnecting
    }


    private async void AcceptClient(Socket client)
    {
        Console.WriteLine("Connected");
        var buffer = BufferPool.Instance.Checkout();
        var clientState = clientStates.Connected;
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

    private void OnDataRead(ArraySegment<byte> arraySegment, NetworkStream ns, ref clientStates clientState)
    {
        var bytes = arraySegment.ToArray();
        var text = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        Console.WriteLine(text+"LF");
        var result = "Incorrect solve";
        if (text.Trim() == "HELLO")
        {
            if (clientState == clientStates.Connected)
            {
                clientState = clientStates.Greeted;
            }
            else if (clientState != clientStates.Connected)
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

    public async void Server(string address, int port)
    {
        //_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        _socket.Listen();
        //await _socket.ConnectAsync(address, port);

        Console.WriteLine("listning");
        /*
        var handler = await _socket.AcceptAsync();
        while (true)
        {
            // Receive message.
            var buffer = new byte[1_024];
            var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);

            var eom = "<|EOM|>";
            if (response.IndexOf(eom) > -1 /)
            {
                Console.WriteLine(
                    $"Socket server received message: \"{response.Replace(eom, "")}\"");

                var ackMessage = "<|ACK|>";
                var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(echoBytes, 0);
                Console.WriteLine(
                    $"Socket server sent acknowledgment: \"{ackMessage}\"");

                break;
            }
            // Sample output:
            //    Socket server received message: "Hi friends 👋!"
            //    Socket server sent acknowledgment: "<|ACK|>"
        }
        */

    }

    public void Client(string address, int port)
    {
        //var ipHostInfo = Dns.GetHostEntry(address);
        //var ipAddress = ipHostInfo.AddressList[0].MapToIPv4();
        _socket.Connect(address, port);
        ListenTcp();
    }

    public void Stream(string host, int port)
    {
        var ipAddress = IPAddress.TryParse(host, out _)
            ? IPAddress.Parse(host)
            : Dns.GetHostEntry(host).AddressList[0].MapToIPv4();
        var client = new TcpClient(ipAddress.ToString(), port);
        _stream = client.GetStream();

    }

    public void ListenTcp()
    {
        for(;;)
        {
            _state = new State();
            handler =  _socket.Accept();
            Console.WriteLine("connected");

            _ = handler.BeginReceiveFrom(_state.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv = ar =>
            {
                var so = ar.AsyncState as State;
                var bytes = handler.EndReceiveFrom(ar, ref _epFrom);    //TODO add check for 0

                if (so == null) return;
                var message = Encoding.ASCII.GetString(so.Buffer, 0, bytes);

                Console.WriteLine(message);
                var data = Encoding.ASCII.GetBytes("replay");
                var bytesResponse = new byte[data.Length];
                //handler.Send(bytesResponse, SocketFlags.None);
                handler.SendAsync(data, SocketFlags.None);
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


                if (ClientInitiatedExit == false) handler.BeginReceiveFrom(so.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv, so);
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