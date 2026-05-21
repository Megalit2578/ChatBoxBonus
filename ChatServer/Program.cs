using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServer
{
    class Program
    {
        private static ConcurrentDictionary<string, StreamWriter> connectedClients = new ConcurrentDictionary<string, StreamWriter>();
        private static string uploadsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uploads");

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("🚀 Starting Chat Server...");

            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            int port = 5000;
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            
            try
            {
                listener.Start();
                Console.WriteLine($"✅ Server started on port {port}. Waiting for connections...");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = HandleConnectionAsync(client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Server error: {ex.Message}");
            }
        }

        private static async Task HandleConnectionAsync(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                
                // Read the first line manually to avoid buffering too much if it's a file stream
                using var ms = new MemoryStream();
                byte[] buffer = new byte[1];
                while (await stream.ReadAsync(buffer, 0, 1) > 0)
                {
                    if (buffer[0] == '\n') break;
                    if (buffer[0] != '\r') ms.WriteByte(buffer[0]);
                }

                // Decode the accumulated bytes as UTF-8
                string header = Encoding.UTF8.GetString(ms.ToArray()).Trim('\uFEFF'); // Trim BOM if present
                
                if (header.StartsWith("USER:"))
                {
                    await HandleTextClientAsync(client, stream, header.Substring(5).Trim());
                }
                else if (header.StartsWith("FILE_UPLOAD:"))
                {
                    // FILE_UPLOAD:userName:fileName:fileSize
                    string[] parts = header.Split(new[] { ':' }, 4);
                    if (parts.Length == 4)
                    {
                        await HandleFileUploadAsync(client, stream, parts[1], parts[2], long.Parse(parts[3]));
                    }
                    else
                    {
                        client.Close();
                    }
                }
                else if (header.StartsWith("FILE_DOWNLOAD:"))
                {
                    // FILE_DOWNLOAD:fileId
                    string fileId = header.Substring(14).Trim();
                    await HandleFileDownloadAsync(client, stream, fileId);
                }
                else
                {
                    client.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Connection error: {ex.Message}");
                client.Close();
            }
        }

        private static async Task HandleTextClientAsync(TcpClient client, NetworkStream stream, string userName)
        {
            try
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                
                int count = 1;
                string originalName = userName;
                while (connectedClients.ContainsKey(userName))
                {
                    userName = $"{originalName}_{count++}";
                }

                connectedClients.TryAdd(userName, writer);
                Console.WriteLine($"[+] {userName} has joined the chat.");
                await BroadcastMessageAsync("SERVER", $"📢 {userName} has joined the chat.");

                while (client.Connected)
                {
                    string? message = await reader.ReadLineAsync();
                    if (message == null) break;

                    Console.WriteLine($"[{userName}]: {message}");
                    await BroadcastMessageAsync(userName, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Client error ({userName}): {ex.Message}");
            }
            finally
            {
                if (connectedClients.TryRemove(userName, out _))
                {
                    Console.WriteLine($"[-] {userName} has left the chat.");
                    await BroadcastMessageAsync("SERVER", $"📢 {userName} has left the chat.");
                }
                client.Close();
            }
        }

        private static async Task HandleFileUploadAsync(TcpClient client, NetworkStream stream, string userName, string fileName, long fileSize)
        {
            string fileId = Guid.NewGuid().ToString() + Path.GetExtension(fileName);
            string savePath = Path.Combine(uploadsDir, fileId);

            Console.WriteLine($"[⬇️] Receiving file '{fileName}' ({fileSize} bytes) from {userName}...");

            try
            {
                using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    
                    while (totalRead < fileSize && (read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, fileSize - totalRead))) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, read);
                        totalRead += read;
                    }
                }

                Console.WriteLine($"[✅] File '{fileName}' received and saved as {fileId}.");
                await BroadcastRawMessageAsync($"FILE_OFFER:{userName}:{fileName}:{fileSize}:{fileId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ File upload failed for {fileName}: {ex.Message}");
                if (File.Exists(savePath)) File.Delete(savePath);
            }
            finally
            {
                client.Close();
            }
        }

        private static async Task HandleFileDownloadAsync(TcpClient client, NetworkStream stream, string fileId)
        {
            string filePath = Path.Combine(uploadsDir, fileId);
            
            Console.WriteLine($"[⬆️] Client requesting download of {fileId}...");

            try
            {
                if (File.Exists(filePath))
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                    {
                        await fs.CopyToAsync(stream);
                        await stream.FlushAsync();
                    }
                    Console.WriteLine($"[✅] File {fileId} sent successfully.");
                }
                else
                {
                    Console.WriteLine($"❌ File {fileId} not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ File download failed for {fileId}: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }

        private static async Task BroadcastMessageAsync(string senderName, string message)
        {
            string formattedMessage = $"MSG:{senderName}:{message}";
            await BroadcastRawMessageAsync(formattedMessage);
        }

        private static async Task BroadcastRawMessageAsync(string rawMessage)
        {
            foreach (var kvp in connectedClients)
            {
                StreamWriter writer = kvp.Value;
                try
                {
                    await writer.WriteLineAsync(rawMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Broadcast error to {kvp.Key}: {ex.Message}");
                }
            }
        }
    }
}
