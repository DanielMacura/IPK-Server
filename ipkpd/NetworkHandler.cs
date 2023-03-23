using System.Net;

namespace ipkpd;

public class NetworkHandler
{
    private readonly string _mode;
    private UdpSocket? _clientUdpSocket;
    private Tcp? _clientTcp;

    public NetworkHandler(string mode)
    {
        _mode = mode;
    }

    public void Start(string host, int port)
    {
        switch (_mode)
        {
            case "tcp":
            {
                _clientTcp = new Tcp();
                _clientTcp.Stream(host, port);
                _clientTcp.ListenTcp();
                break;
            }
            case "udp":
            {
                var s = new UdpSocket();
                s.Server(IPAddress.Loopback.ToString(), port);

                _clientUdpSocket = new UdpSocket();
                _clientUdpSocket.Client(host, port);
                break;
            }
        }
    }

    public void SendMessage(string message)
    {
        switch (_mode)
        {
            case "tcp":
            {
                _clientTcp?.SendTcp(message);
                break;
            }

            case "udp":
            {
                _clientUdpSocket?.Send(message);
                break;
            }
        }
    }
}