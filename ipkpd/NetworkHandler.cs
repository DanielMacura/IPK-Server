namespace ipkcpd;
public class NetworkHandler
{
    private readonly string _mode;
    private IProtocolInterface? _protocol = null;

    public NetworkHandler(string mode)
    {
        _mode = mode;
    }

    public void Start(string host, int port)
    {
        _protocol = _mode switch
        {
            "tcp" => new TcpProtocol(),
            "udp" => new UdpProtocol(),
            _ => _protocol
        };
        _protocol?.Listen(host, port);
    }

    public void Stop()
    {
        _protocol?.Stop();
    }
}