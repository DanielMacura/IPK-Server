using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ipkpd;

public class Tcp
{
    private const int BufSize = 8 * 1024;
    private const char Lf = (char)10;
    private readonly State _state = new();
    private AsyncCallback? _recv;
    private NetworkStream? _stream;
    public bool ClientInitiatedExit;


    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private Socket handler;
    static IPEndPoint _sender = new IPEndPoint(IPAddress.Any, 0);
    private EndPoint _epFrom = _sender;

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

    public async Task ListenAsync()
    {
        
    }
    public void ListenTcp()
    {
        for(;;)
        {
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