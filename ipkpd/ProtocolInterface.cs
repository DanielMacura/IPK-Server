﻿using System.Net;
using System.Net.Sockets;

namespace ipkcpd;

public interface IProtocolInterface
{
    public async void Listen(string address, int port){}
    private enum ClientStates{
        Connected,
        Greeted,
        Disconnecting
    }
    private async void AcceptClient(Socket client){}

    private bool OnDataRead(ArraySegment<byte> arraySegment, NetworkStream ns, ref ClientStates clientState)
    {
        return false;
    }

    private bool OnDataRead(ArraySegment<byte> arraySegment, ref EndPoint remoteEndPoint)
    {
        return false;
    }
    private void SendReply(NetworkStream ns, string message){}
}