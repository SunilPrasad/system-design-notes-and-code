using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

// ----------------------------------------------------------
// Windows WSAPoll() P/Invoke
// ----------------------------------------------------------
public static class WinSock
{
    public const short POLLRDNORM = 0x0100; // Normal data available

    [StructLayout(LayoutKind.Sequential)]
    public struct WSAPOLLFD
    {
        public IntPtr fd;      // SOCKET handle
        public short events;   // Requested events
        public short revents;  // Returned events
    }

    [DllImport("Ws2_32.dll", SetLastError = true)]
    public static extern int WSAPoll(
        [In, Out] WSAPOLLFD[] fdArray,
        uint nfds,
        int timeout
    );
}

// ----------------------------------------------------------
// MAIN EVENT LOOP SERVER
// ----------------------------------------------------------
class Program
{
    static void Main()
    {
        // Create listener
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 5000));
        listener.Listen(1024);

        Console.WriteLine("WSAPoll Single-Threaded Event Loop Server running on 127.0.0.1:5000");
        Console.WriteLine("Open multiple terminals and run:  telnet 127.0.0.1 5000");
        Console.WriteLine();

        // All sockets (listener + clients)
        var allSockets = new List<Socket> { listener };

        var buffer = new byte[4096];
        int timeoutMs = 1000; // 1 second

        while (true)
        {
            // Create WSAPOLLFD array matching socket count
            var pollArr = new WinSock.WSAPOLLFD[allSockets.Count];

            for (int i = 0; i < allSockets.Count; i++)
            {
                pollArr[i].fd = allSockets[i].Handle;
                pollArr[i].events = WinSock.POLLRDNORM;
                pollArr[i].revents = 0;
            }

            // Block until at least one socket is ready
            int ready = WinSock.WSAPoll(pollArr, (uint)pollArr.Length, timeoutMs);

            if (ready == SOCKET_ERROR)
            {
                Console.WriteLine("WSAPoll failed. Error: " + Marshal.GetLastWin32Error());
                continue;
            }

            if (ready == 0)
            {
                // timeout — no activity
                continue;
            }

            // Handle all events
            for (int i = 0; i < pollArr.Length; i++)
            {
                if ((pollArr[i].revents & WinSock.POLLRDNORM) == 0)
                    continue;

                Socket socket = allSockets[i];

                // Listener socket → new client
                if (socket == listener)
                {
                    Socket client = listener.Accept();
                    allSockets.Add(client);
                    Console.WriteLine($"[ACCEPT] Client: {client.RemoteEndPoint}");
                }
                else
                {
                    // Read from client
                    int bytesRead;

                    try
                    {
                        bytesRead = socket.Receive(buffer);
                    }
                    catch
                    {
                        Console.WriteLine($"[ERROR] Closing client: {socket.RemoteEndPoint}");
                        allSockets.Remove(socket);
                        socket.Close();
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        Console.WriteLine($"[CLOSE] {socket.RemoteEndPoint}");
                        allSockets.Remove(socket);
                        socket.Close();
                        break;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    Console.WriteLine($"[RECV] {socket.RemoteEndPoint}: {text}");

                    // Echo response
                    string response = $"You said: {text}\r\n";
                    socket.Send(Encoding.UTF8.GetBytes(response));
                }
            }
        }
    }

    const int SOCKET_ERROR = -1;
}
