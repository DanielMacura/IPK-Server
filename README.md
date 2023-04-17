# IPK - second project - IOTA

  A simple server for the IPK Calculator Protocol written in C#.

## Executive summary of used protocols

- ### TCP

  Transmission Control Protocol is a transport protocol that is used on top of IP (Internet Protocol) to ensure reliable transmission of packets over the internet or other networks. TCP is a connection-oriented protocol, which means that it establishes and maintains a connection between the two parties until the data transfer is complete. TCP provides mechanisms to solve problems that arise from packet-based messaging, e.g. lost packets or out-of-order packets, duplicate packets, and corrupted packets. TCP achieves this by using sequence and acknowledgement numbers, checksums, flow control, error control, and congestion control.

- ### UDP

  User Datagram Protocol is a connectionless and unreliable protocol that provides a simple and efficient way to send and receive datagrams over an IP network. UDP does not guarantee delivery, order, or integrity of the data, but it minimizes the overhead and latency involved in transmitting data when compared to TCP. UDP is suitable for applications that require speed, simplicity, or real-time communication, such as streaming media, online gaming, voice over IP, or DNS queries.

## Implementation

This program is written using [clean code](https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29) conventions. The core of the communication is abstracted away and wrapped in multiple classes and may be simply extended for other protocols by implementing the protocol interface.

<img  src="./docs src/IPK-IOTA.drawio.svg">

The program begins by parsing command line arguments. A callback-based option parser called [Mono.Options](https://www.nuget.org/packages/Mono.Options) is used.

``` c#
var p =  new OptionSet{
    {"h=|host=",  "IP address of AaaS provider.",
        v => host = v},
    {"p=|port=",  "port of connection",
        (int v)  => port = v},
    {"m=|mode=",  "connection mode.",
        v => _mode = v},
    {"v=|verbose=", "verbosity of the server.",
        v => _verbosity = Convert.ToBoolean(v)},
    {"i|help",  "show this message and exit",
        v => showHelp = v !=  null}};
_ = p.Parse(args);
```

Upon completion of argument parsing, a check is conducted to assess if any required information is missing. In such a case, the user is promptly notified and the program terminates. Conversely, if all necessary arguments are present, the program proceeds by starting the server.

``` c#
_handler = new NetworkHandler(_mode);
_handler.Start(host, port);
```

### Network Handler Class

The class acts as a wrapper for the underlaying [Tcp](#tcp-class) and [Udp](#udp-class) Protocols and helps abstract away more of the connection details. The bellow provided snippet showcases its two classes.

``` c#
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
```

### TCP communication

When a NetworkHandler is instantiated with the TCP mode, it creates a new instance of the [TcpProtocol class](#tcp-class) and invokes its Listen method, which takes in the provided host and port as parameters. This process initializes the server and clients are able to begin communicating.

The users are now able to communicate, as demonstrated in the diagram below.

<img  src="./docs src/ipkcp tcp.svg">

#### TcpProtocol Class

The class implements methods from IProtocolInterface.

- `Listen(string address, int port)`: A public method that takes two arguments, the IP address or hostname of the server and the port number. It creates a new socket, binds it with the provided details and begins listening. Then while `_listening`, the server tries to accept an incoming connection using the `BeginAccept` and `EndAccept` methods asynchronously with `Task.Factory.FromAsync` adhering to the Asynchronous Programming Model pattern. The client is then added to the client list later used for gracefully ending connections. The `AcceptClient` method is called. 

    ``` c#
    _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
    _socket.Listen();
    while (_listening)
    {
        var client = await Task.Factory.FromAsync<Socket>(_socket.BeginAccept, _socket.EndAccept, null);
        _clientList.Add(client);
        AcceptClient(client);
    }
    ```

- `AcceptClient(Socket client)`: The private method first checks-out a buffer from the buffer pool and sets the client state to connected. A network stream `ns` is then created from the socket, since tcp traffic is streamable. While `_listening` the server tries to read from the stream and store the read bytes into the buffer. 
  
    ``` c#
    var clientState = ClientStates.Connected;
    await using var ns = new NetworkStream(client, true);
    while (_listening)
    {
        await ns.ReadAsync(buffer.Array.AsMemory(buffer.Offset, buffer.Count));
        OnDataRead(new ArraySegment<byte>(buffer.Array, buffer.Offset, count), ns, ref clientState)
    }
    ```

    If some bytes are read, the `OnDataRead` method is called.

- `OnDataRead(ArraySegment<byte> arraySegment, NetworkStream ns, ref ClientStates clientState)`: The private method decodes the provided bytes into a string which is the checked according to the IPK Calculator Protocol. Depending on the client state (connected/greeted/disconnecting) parsed syntactic constructs convey different semantic meaning. `HELLO`, `BYE` and `SOLVE` messages are correctly responded to. The `SOLVE` command triggers the creation of a new instance of the Evaluator class and calls the `Evaluate` method which returns the integer result or null on an evaluation error. These responses are properly formatted and sent back to the client using the `SendReply` method.

- `SendReply(Stream ns, string message)`: The private method sends the responses to the client using the network stream.

    ``` c#
    var responseBytes = Encoding.ASCII.GetBytes(message);
    ns.Write(responseBytes);
    ns.Flush();
    ```

- `Stop()`: This public method gracefully terminates all connected clients by sending the `BYE` message, the method then sleeps for a short period of time to allow any clients trying to reply to the `BYE` message to do so. The server then exits.

    ``` c#
    _listening = false;
    foreach (var client in _clientList.Where(client => client != null))
    {
        if (!client!.Connected) continue;
        var bytes = Encoding.ASCII.GetBytes("BYE" + Lf);
        client.Send(bytes);
    }
    Thread.Sleep(500);
    Environment.Exit(0);
    ```

#### UDP Class

The class implements methods from IProtocolInterface. Many of the methods are similar to the [TcpProtocol class](#tcp-class), for the sake of time, only the differences shall be shown.

- `Listen(string address, int port)`: Compared to the tcp implementation, since udp is a stateless protocol, there is no need for a client variable, client list and individual client handling, .

    ```c# {1,3}
    _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
    while (_listening)
    {
        var buffer = BufferPool.Instance.Checkout();
        _socket.ReceiveFrom(buffer.Array, buffer.Offset, buffer.Count, SocketFlags.None, ref _remoteEndPoint);
        OnDataRead(new ArraySegment<byte>(buffer.Array, buffer.Offset, count), ref _remoteEndPoint);
    }
    ```

- `OnDataRead(ArraySegment<byte> arraySegment, ref EndPoint remoteEndPoint)`: The main differences compared to the tcp implementation are that no state checking is present, messages are directly fed to the evaluator and the responses are then sent using the `SendReply` method.

- `SendReply(EndPoint clientEndPoint, string? message, bool errorCode, Socket socket)`: This private method first checks wether the message length is below 255 bytes. Following that, the ASCII message is converted into a byte array which is shifted 3 bytes to the right and the header bytes are set according to IPKCP. The resulting array is sent using the socket's `SendTo()` method, to which the array and `clientEndPoint` is passed.

## Tests

### TCP Tests

Input marked in code tags. Server response in plain text.

- Simple handshake
  <pre>
  <font style="background-color:hsl(30,80%,90%); color:black">Client:   HELLO</font>
  <font style="background-color:hsl(220, 80%, 90%); color:black">Server:   HELLO</font>
  </pre>

- Incorrect handshake - case sensitive
  <pre>
  Client:   ello
  Server:   BYE
  </pre>

- Simple exchange
  <pre>
  Client:   HELLO
  Server:   HELLO
  Client:   SOLVE (+ 1 2)
  Server:   3
  Client:   BYE
  Server:   BYE
  </pre>

- Multiple exchanges

  1. `HELLO`
  2. HELLO
  3. `SOLVE (+ 1 2)`
  4. 3
  5. `SOLVE (* 1 2 3 4 5)`
  6. 120
  7. `SOLVE (- 2 2)`
  8. 0
  9. `BYE`
  10. BYE

- Incorrect exchange - 1

  1. `HELLO`
  1. HELLO
  2. `SOLVE (1 2)`
  3. BYE

- Incorrect exchange - 2

  1. `HELLO`
  1. HELLO
  2. `SOLVE (+)`
  3. BYE

- SIGN INT

  1. `HELLO`
  2. HELLO
  3. `SOLVE (+ 1 2)`
  4. 3
  5. `Ctrl + C`
  6. BYE

### UDP Tests

Input marked in code tags. Server response in plain text.

- Simple exchange

  1. `(+ 1 1 1 1)`
  2. OK:4

- Multiple exchanges

  1. `(+ 1 1 1 1)`
  2. OK:4
  3. `(* 1 1 1 1)`
  4. OK:1
  5. `(/ 10 2)`
  6. OK:5

- Incorrect exchange

  1. `(+)`
  2. ERR:Could not parse the message
  3. `(1 2)`
  4. ERR:Could not parse the message
  5. `()`
  6. ERR:Could not parse the message

- Message too long

  1. `(+ 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1 1)`
  2. ERROR: Message over 255 bytes.
  3. `(+ 1 1)`
  4. OK:2

## Extra features

- The software has been updated with a new command line flag, `--help`, which can be used to display the help information. This flag can be invoked by executing the command `./ipkcpc --help` or `./ipkcpc -i`.
  
    ```
    Usage: ipkcpd [OPTIONS]
    Ipkcpd is a server conforming to the IPK Calculator Protocol.

    Options:
        -h, --host=VALUE           IP address of server.
        -p, --port=VALUE           connection port
        -m, --mode=VALUE           connection mode.
        -v, --verbose=VALUE        verbosity of the server.
        -i, --help                 show this message and exit
    ```

- Additionally, the command line argument order is arbitrary. Users can now pass the flags in any order they prefer when executing the software. For instance, the command `./ipkcpc -m UDP -p 2023 -H localhost` is a valid input and will be processed correctly by the software.
- The server may internally execute calculations with arbitrarily large numbers thanks to `BitInteger`.

## References

Microsoft, 2023, *UdpClient Class*, accessed 21 March 2023, <https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient?view=net-7.0>

Microsoft, 2023, *TcpClient Class*, accessed 21 March 2023, <https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient?view=net-7.0>

Wojtek Lukaszuk, 2023, *Clean code*, accessed 21 March 2023, <https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29>

Alemar, 2023, *UDPSocket.cs*, accessed 21 March 2023, <https://gist.github.com/darkguy2008/413a6fea3a5b4e67e5e0d96f750088a9>

Wikipedia, 2023, *User Datagram Protocol*, accessed 21 March 2023, <https://en.wikipedia.org/wiki/User_Datagram_Protocol>

Wikipedia, 2023, *Transmission Control Protocol*, accessed 21 March 2023, <https://en.wikipedia.org/wiki/Transmission_Control_Protocol>