using System.Net;
using System.Net.Sockets;
using System.Text;

//Source
//https://gist.github.com/darkguy2008/413a6fea3a5b4e67e5e0d96f750088a9
//

namespace ipkpd;

public class UdpSocket
{
    private const int BufSize = 8 * 1024;
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private readonly State _state = new();
    static IPEndPoint _sender = new IPEndPoint(IPAddress.Any, 0);
    private EndPoint _epFrom = _sender;
    private AsyncCallback? _recv;

    public void Server(string address, int port)
    {
        _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        Console.WriteLine("listning");

        Receive();
    }

    public void Client(string address, int port)
    {
        //var ipHostInfo = Dns.GetHostEntry(address);
        //var ipAddress = ipHostInfo.AddressList[0].MapToIPv4();
        _socket.Connect(address, port);
        Receive();
    }

    public void Send(string text)
    {
        if (text.Length > 255-3)
        {
            Console.Error.Write("ERROR: Calculated result over 255 bytes.");
            return;
        }
        var data = Encoding.ASCII.GetBytes(text);
        Console.WriteLine("trying to send {0}", text);
        var bytes = new byte[data.Length + 3];

        bytes[0] = 1;
        bytes[1] = 0;
        bytes[2] = (byte)data.Length;
        data.CopyTo(bytes, 3);

        _socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, ar =>
        {
            _ = _socket.EndSend(ar);
        }, _state);
    }

    private void Receive()
    {
        EndPoint lastConnected = null;
        for (;;)
        {
            if (_socket.Poll(-1, SelectMode.SelectRead))
            {
                _=_socket.BeginReceiveFrom(_state.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv = ar =>
                {
                    var so = ar.AsyncState as State;
                    var bytes = _socket.EndReceiveFrom(ar, ref _epFrom);        //TODO add check for 0

                    
                    int opcode = so.Buffer[0];
                    int payloadLength = so.Buffer[1];
                    var message = Encoding.ASCII.GetString(so.Buffer, 2, bytes - 2);
                    Console.WriteLine("Received");
                    var truncatedToNLength = new string(message.Take(payloadLength).ToArray());
                    if (opcode is 0)
                    {
                        var eval = new Evaluator();
                        var result = eval.Evaluate(truncatedToNLength);
                        Console.WriteLine("From {0}", _epFrom.ToString());
                        //_clientUdpSocket = new UdpSocket();
                        //_clientUdpSocket.Client(_epFrom.ToString().Split(':')[0], Int32.Parse(_epFrom.ToString().Split(':')[1]));
                        //_clientUdpSocket.Send(result.ToString());
                        //Client(_epFrom.ToString().Split(':')[0], Int32.Parse(_epFrom.ToString().Split(':')[1]));
                        if (!_socket.Connected || lastConnected!=_epFrom)
                        {
                            lastConnected = _epFrom;
                            _socket.Connect(_epFrom.ToString().Split(':')[0],
                                Int32.Parse(_epFrom.ToString().Split(':')[1]));
                        }
                        Send(result.ToString());
                    }
                    else
                    {
                        Console.Error.WriteLine("Incorrect UPD packet received. Opcode is not 1 - receive.");
                    }
                    _socket.BeginReceiveFrom(so.Buffer, 0, BufSize, SocketFlags.None, ref _epFrom, _recv, so);
                }, _state);

            }
        }
        
            
        
    }

    public class State
    {
        public byte[] Buffer = new byte[BufSize];
    }
}