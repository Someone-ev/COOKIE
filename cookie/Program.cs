using System.Net;
using System.Net.Sockets;
using System.Text;

namespace cookie;

class Program
{
    static void Main(string[] args)
    {
        TcpListener localServer = new TcpListener(IPAddress.Loopback, 8080);
        localServer.Start();

        Console.WriteLine("Login client started.");
        Console.WriteLine("Waiting for login from HTML...");

        while (true)
        {
            TcpClient browserClient = localServer.AcceptTcpClient();
            NetworkStream browserStream = browserClient.GetStream();

            StreamReader reader = new StreamReader(browserStream, Encoding.UTF8);
            StreamWriter writer = new StreamWriter(
                browserStream,
                new UTF8Encoding(false),
                1024,
                true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };

            string? requestLine = reader.ReadLine();

            int contentLength = 0;
            string? line;

            while (!string.IsNullOrEmpty(line = reader.ReadLine()))
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    contentLength = int.Parse(line.Split(':')[1].Trim());
                }
            }

            if (requestLine != null && requestLine.StartsWith("OPTIONS"))
            {
                writer.WriteLine("HTTP/1.1 204 No Content");
                writer.WriteLine("Access-Control-Allow-Origin: *");
                writer.WriteLine("Access-Control-Allow-Methods: POST, OPTIONS");
                writer.WriteLine("Access-Control-Allow-Headers: Content-Type");
                writer.WriteLine();
                browserClient.Close();
                continue;
            }

            char[] bodyBuffer = new char[contentLength];
            reader.ReadBlock(bodyBuffer, 0, contentLength);

            string body = new string(bodyBuffer);

            string username = "";
            string password = "";

            foreach (string part in body.Split('&'))
            {
                string[] pair = part.Split('=', 2);

                if (pair.Length != 2)
                    continue;

                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                string value = Uri.UnescapeDataString(pair[1].Replace("+", " "));

                if (key == "username")
                    username = value;

                if (key == "password")
                    password = value;
            }

            Console.WriteLine("Login received from HTML.");
            Console.WriteLine("Connecting to main server...");

            string serverResponse;

            try
            {
                using TcpClient serverClient = new TcpClient();

                serverClient.Connect("192.168.1.236", 5000);

                NetworkStream serverStream = serverClient.GetStream();

                string message = $"LOGIN|{username}|{password}";
                byte[] data = Encoding.UTF8.GetBytes(message);

                serverStream.Write(data, 0, data.Length);

                byte[] responseBuffer = new byte[1024];
                int bytesRead = serverStream.Read(
                    responseBuffer,
                    0,
                    responseBuffer.Length
                );

                serverResponse = Encoding.UTF8.GetString(
                    responseBuffer,
                    0,
                    bytesRead
                );

                Console.WriteLine("Server response: " + serverResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection error: " + ex.Message);
                serverResponse = "SERVER_ERROR";
            }

            byte[] responseBytes = Encoding.UTF8.GetBytes(serverResponse);

            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine("Content-Type: text/plain; charset=utf-8");
            writer.WriteLine("Access-Control-Allow-Origin: *");
            writer.WriteLine("Content-Length: " + responseBytes.Length);
            writer.WriteLine();
            writer.Flush();
            browserStream.Write(responseBytes, 0, responseBytes.Length);
            browserStream.Flush();

            writer.Dispose();
            reader.Dispose();
            browserClient.Close();
        }
    }
}