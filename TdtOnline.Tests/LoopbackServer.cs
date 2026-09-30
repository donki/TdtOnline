using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TdtOnline.Tests;

/// <summary>
/// Servidor HTTP minimo en 127.0.0.1 y un puerto libre, dentro del propio proceso: contesta siempre
/// lo mismo. Sirve para probar la descarga de listas sin salir del equipo (nada de red externa).
/// </summary>
internal sealed class LoopbackServer : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();

    public LoopbackServer(string body, int status = 200)
    {
        _listener.Start();
        Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/lista";
        _ = ServeAsync(Encoding.UTF8.GetBytes(body), status);
    }

    public string Url { get; }

    public int Requests { get; private set; }

    private async Task ServeAsync(byte[] body, int status)
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var stream = client.GetStream();
                var buffer = new byte[8192];
                var request = new StringBuilder();
                while (!request.ToString().Contains("\r\n\r\n"))
                {
                    var read = await stream.ReadAsync(buffer, _stop.Token);
                    if (read == 0)
                        break;
                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                Requests++;
                var head = $"HTTP/1.1 {status} X\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token);
                await stream.WriteAsync(body, _stop.Token);
            }
        }
        catch (Exception) when (_stop.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
    }
}
