using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ipkcpd;


public class UdpProtocol : IProtocolInterface
{
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    bool _listening = true;
    private EndPoint _remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);


    public async void Listen(string address, int port)
    {
        //_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        while (_listening)
        {
            try
            {
                Console.WriteLine("Connecting");
                var buffer = BufferPool.Instance.Checkout();
                var count = _socket.ReceiveFrom(buffer.Array ?? throw new InvalidOperationException(), buffer.Offset, buffer.Count, SocketFlags.None, ref _remoteEndPoint);

                if (count > 0)
                {
                    OnDataRead(new ArraySegment<byte>(buffer.Array, buffer.Offset, count), ref _remoteEndPoint);
                }
            }
            catch (Exception e)
            {
                await Console.Error.WriteAsync(e.ToString());
            }
        }
    }

    public void Stop()
    {
        _listening = false;
    }


    private void OnDataRead(ArraySegment<byte> arraySegment, ref EndPoint remoteEndPoint)
    {
        var receiveBytes = arraySegment.ToArray();
        var message = Encoding.ASCII.GetString(receiveBytes, 2, receiveBytes.Length - 2);
        Console.WriteLine("message" +message);
        var eval = new Evaluator();

        var result = eval.Evaluate(message).ToString();
        Console.WriteLine(result);

        SendReply(_remoteEndPoint, result, result == null, _socket);
    }

    private static void SendReply(EndPoint clientEndPoint, string? message, bool errorCode, Socket socket)
    {
        switch (message)
        {
            case { Length: > 255 }:
                message = "Calculated result over 255 bytes.";
                errorCode = true;
                break;
            case "" or null:
                Console.WriteLine("Null case");
                message = "Could not return answer.";
                errorCode = true;
                break;
        }

        var resultBytes = Encoding.ASCII.GetBytes(message);
        Console.WriteLine("trying to send {0}", message);
        var returnBytes = new byte[resultBytes.Length + 3];

        returnBytes[0] = 1;
        returnBytes[1] = errorCode switch
        {
            true => 1,
            false => 0
        };
        returnBytes[2] = (byte)resultBytes.Length;
        resultBytes.CopyTo(returnBytes, 3);


        var returnBytesCount = socket.SendTo(returnBytes, clientEndPoint);
        Console.WriteLine(returnBytesCount);
    }
}