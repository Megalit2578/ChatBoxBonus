using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main()
    {
        // Start server
        TcpListener listener = new TcpListener(IPAddress.Any, 5000);
        listener.Start();
        
        _ = Task.Run(async () => {
            TcpClient c = await listener.AcceptTcpClientAsync();
            NetworkStream stream = c.GetStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            string? msg = await reader.ReadLineAsync();
            Console.WriteLine("Server received: " + msg);
            
            byte[] bytes = Encoding.UTF8.GetBytes("SERVER: Hello back\r\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
        });

        await Task.Delay(500);

        // Start client
        TcpClient client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", 5000);
        NetworkStream cstream = client.GetStream();
        StreamWriter writer = new StreamWriter(cstream, Encoding.UTF8) { AutoFlush = true };
        StreamReader creader = new StreamReader(cstream, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync("CLIENT: Hello server");
        string? resp = await creader.ReadLineAsync();
        Console.WriteLine("Client received: " + resp);
    }
}
