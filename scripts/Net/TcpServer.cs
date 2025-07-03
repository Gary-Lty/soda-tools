using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
public static partial class TaskExtensions
{
    public static async Task<TcpClient> WithCancellation(this Task<TcpClient> task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        return await task;
    }
}

public class Client2ServerEvt
{
    public string Message;
}
public class TcpServer : MonoBehaviour
{
    private TcpListener tcpListener;
    private Task listenTask;
    private CancellationTokenSource cancellationTokenSource;
    private bool isRunning = true;

    // 存储所有连接的客户端
    private List<TcpClient> clients = new List<TcpClient>();

    public void StartServer()
    {
        gameObject.SetActive(true);
        MainThreadDispatcher.Initialize();
        // 初始化取消令牌源
        cancellationTokenSource = new CancellationTokenSource();

        // 启动服务器
        Thread serverThread = new Thread(new ThreadStart(RunServer));
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    private async void RunServer()
    {
        // 设置监听的IP地址和端口
        IPAddress ip = IPAddress.Any; // 本地IP
        int port = 1994; // 端口号
        isRunning = true;
        while (isRunning)
        {
            try
            {
                if (tcpListener != null && tcpListener.Server.IsBound)
                {
                    tcpListener.Stop();
                }

                tcpListener = new TcpListener(ip, port);
                tcpListener.Start();

                Log($"服务器已启动，等待客户端连接...");

                // 开启一个任务来监听客户端连接
                listenTask = ListenForClientsAsync(cancellationTokenSource.Token);
                await listenTask; // 等待监听任务结束

                // 等待服务器任务结束
            }
            catch (OperationCanceledException)
            {
                Log("服务器运行任务已取消");
                break;
            }
            catch (Exception e)
            {
                LogError($"启动服务器失败: {e.Message}");
                LogError("尝试重新启动服务器...");
            }

            await Task.Delay(5000); // 等待5秒后重试
        }
        Log("服务器运行结束");
    }

    private async Task ListenForClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 非阻塞，直到有客户端连接，并响应取消信号
                TcpClient client = await tcpListener.AcceptTcpClientAsync().WithCancellation(cancellationToken);
                Log("客户端已连接");

                lock (clients)
                {
                    clients.Add(client); // 将新客户端加入列表
                }

                // 处理客户端消息
                _ = HandleClientCommunicationAsync(client, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log("监听任务已取消");
                break;
            }
            catch (ObjectDisposedException)
            {
                Log("TcpListener 已被释放");
                break;
            }
            catch (Exception e)
            {
                LogError("监听客户端时出错: " + e.Message);
                break;
            }
        }
    }

    private async Task HandleClientCommunicationAsync(TcpClient client, CancellationToken cancellationToken)
    {
        NetworkStream stream = client.GetStream();

        byte[] buffer = new byte[1024];
        int bytesRead;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 非阻塞，读取客户端发送的消息
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead == 0)
                {
                    // 客户端断开连接
                    LogError("客户端断开连接");
                    break;
                }

                // 将接收到的字节数组转换为字符串
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Log($"服务器转发消息: {message}");
                MainThreadDispatcher.Post((v)=> MessageBroker.Default.Publish(new Client2ServerEvt{Message =  message}),null );
                // 广播消息给所有客户端
                // BroadcastMessage(message, client);
            }
            catch (OperationCanceledException)
            {
                Log("客户端通信任务已取消");
                break;
            }
            catch (Exception e)
            {
                LogError($"处理客户端通信时出错: {e.Message}");
                break;
            }
        }

        // 移除断开的客户端
        lock (clients)
        {
            clients.Remove(client);
        }

        // 关闭客户端连接
        client.Close();
    }

    /// <summary>
    /// 广播消息给所有客户端
    /// </summary>
    private void BroadcastMessage(string message, TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);

        lock (clients)
        {
            foreach (var client in clients)
            {
                if (client != sender && client.Connected) // 不发送给原始发送者
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                    }
                    catch (Exception e)
                    {
                        LogError($"广播消息失败: {e.Message}");
                    }
                }
            }
        }
    }

    public void SendMessageToClient(string message)
    {
        if (!isRunning)
        {
            Debug.Log("服务器未在运行中。");
            return;
        }
        byte[] data = Encoding.UTF8.GetBytes(message);

        lock (clients)
        {
            if (clients.Count <= 0)
            {
                Debug.Log("没有正在连接中的客户端。");
            }
            foreach (var client in clients)
            {
                if (client.Connected)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                    }
                    catch (Exception e)
                    {
                        LogError($"广播消息失败: {e.Message}");
                    }
                }
            }
        }
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    [ContextMenu("StopServer")]
    public void StopServer()
    {
        Debug.Log("[TCP]停止服务器");
        // 停止服务器
        isRunning = false;

        // 取消所有任务
        cancellationTokenSource?.Cancel();

        if (tcpListener != null)
        {
            tcpListener.Stop();
        }
        Debug.Log("[TCP]服务器已停止1");

        // 关闭所有客户端连接
        lock (clients)
        {
            foreach (var client in clients)
            {
                client.Close();
            }

            clients.Clear();
        }
        Debug.Log("[TCP]服务器已停止2");

        // 确保取消令牌源被释放
        cancellationTokenSource?.Dispose();
        cancellationTokenSource = null;
        Debug.Log("[TCP]服务器已停止3");
    }

    void LogError(string message)
    {
        MainThreadDispatcher.Post((obj) => Debug.LogError("[TCP]"+message), null);
    }

    void Log(string message)
    {
        MainThreadDispatcher.Post((obj) => Debug.Log("[TCP]"+message), null);
    }
}



