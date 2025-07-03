using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UniRx;
using Task = System.Threading.Tasks.Task;

public static partial class TaskExtensions
{
    public static async Task<int> WithCancellation(this Task<int> task, CancellationToken cancellationToken)
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

    public static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(() => tcs.TrySetResult(true)))
        {
            if (task != await Task.WhenAny(task, tcs.Task))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }
}

public class TcpGame : MonoBehaviour
{
    private TcpClient tcpClient;
    private NetworkStream stream;
    private Task sendTask;
    private Task receiveTask;
    private CancellationTokenSource cancellationTokenSource;
    private bool isConnected = false;
    private bool isConnecting;
    private IPAddress _ipAddress;

    public int Port = 1994;
    public int MaxRetries = 100;
    public int InitialDelay = 500; // 初始延迟时间（毫秒）

    private void Start()
    {
        MainThreadDispatcher.Initialize();
    }

    public void ConnectToServer(IPAddress address)
    {
        if (isConnected || isConnecting) return;
        Debug.Log("ConnectToServer: " + address.ToString());
        this._ipAddress = address;
        ConnectToServer();
    }

    async void ConnectToServer()
    {
        if (isConnecting)
        {
            LogError("正在连接中，避免重复调用");
            return;
        }

        if (isConnected)
        {
            LogError("已连接，避免重复调用");
            return;
        }
        cancellationTokenSource = new CancellationTokenSource();
        CancellationToken token = cancellationTokenSource.Token;
        await ReconnectAsync(token);
    }

    private async Task SendMessagesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && isConnected)
        {
            try
            {
                if (messageQueue.TryDequeue(out string message))
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    await stream.WriteAsync(data, 0, data.Length).WithCancellation(token);
                    Log("发送消息: " + message);
                }
                else
                {
                    await Task.Delay(100, token);
                }
            }
            catch (OperationCanceledException)
            {
                Log("发送任务被取消");
                break;
            }
            catch (IOException e)
            {
                LogError("发送消息时出错: " + e.Message);
                isConnected = false;
                await ReconnectAsync(token);
                break;
            }
            catch (Exception e)
            {
                LogError("未知错误: " + e.Message);
                isConnected = false;
                await ReconnectAsync(token);
                break;
            }
        }

        Log("发送任务结束");
    }

    private ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

    public void SendMessageToServer(string message)
    {
        if (!isConnected || stream == null)
        {
            LogError("无法发送消息: 连接未建立或流为空");
            return;
        }

        messageQueue.Enqueue(message);
    }

    private async Task ReceiveMessagesAsync(CancellationToken token)
    {
        byte[] buffer = new byte[1024];
        while (!token.IsCancellationRequested && isConnected)
        {
            try
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).WithCancellation(token);
                if (bytesRead == 0)
                {
                    Log("连接已关闭");
                    isConnected = false;
                    await ReconnectAsync(token);
                    break;
                }

                string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Log("收到消息: " + receivedMessage);
                MainThreadDispatcher.Post(msg => MessageBroker.Default.Publish(new Server2ClientEvt{Message = (string)msg}),receivedMessage);
            }
            catch (OperationCanceledException)
            {
                Log("接收任务被取消");
                break;
            }
            catch (IOException e)
            {
                LogError("接收消息时出错: " + e.Message);
                isConnected = false;
                await ReconnectAsync(token);
                break;
            }
            catch (Exception e)
            {
                LogError("未知错误: " + e.Message);
                isConnected = false;
                await ReconnectAsync(token);
                break;
            }
        }

        Log("接收任务结束");
    }

    private async Task ReconnectAsync(CancellationToken token)
    {
        if (isConnecting)
        {
            LogError("正在连接中，避免重复调用");
            return;
        }

        if (isConnected)
        {
            LogError("已连接，避免重复调用");
            return;
        }
        isConnecting = true;
        Log("重新连接服务器");
        if (stream != null)
        {
            stream.Close();
            stream.Dispose();
            stream = null;
        }

        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient.Dispose();
            tcpClient = null;
        }

        int retryCount = 0;

        while (!token.IsCancellationRequested && !isConnected && retryCount < MaxRetries)
        {
            try
            {
                tcpClient = new TcpClient();
                await Task.Run(() => { tcpClient.Connect(_ipAddress, Port); }, token);

                stream = tcpClient.GetStream();
                isConnected = true;

                Log("已连接到服务器");

                sendTask = SendMessagesAsync(token);
                receiveTask = ReceiveMessagesAsync(token);
            }
            catch (OperationCanceledException)
            {
                Log("重新连接任务被取消");
                break;
            }
            catch (SocketException e)
            {
                retryCount++;
                int delay = InitialDelay * (int)Math.Pow(2, retryCount - 1);
                LogError($"重新连接服务器失败: {e.Message}. 尝试次数: {retryCount}, 下次重试将在 {delay} 毫秒后...");

                if (retryCount >= MaxRetries)
                {
                    LogError("达到最大重试次数，停止重试");
                    break;
                }

                await Task.Delay(delay, token);
            }
            catch (Exception e)
            {
                LogError("未知错误: " + e.Message);
                break;
            }
        }

        Log($"连接操作结束：isConnected = {isConnected}, IsCancellationRequested = {token.IsCancellationRequested}, retryCount ={retryCount}");

        isConnecting = false;
    }

    private void CloseConnection()
    {
        if (stream != null)
        {
            stream.Close();
            stream.Dispose();
            stream = null;
        }

        if (tcpClient != null)
        {
            tcpClient.Close();
            tcpClient.Dispose();
            tcpClient = null;
        }

        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }

    void LogError(string message)
    {
        MainThreadDispatcher.Post((obj) => Debug.LogError("[TCP]"+message), null);
    }

    void Log(string message)
    {
        MainThreadDispatcher.Post((obj) => Debug.Log("[TCP]"+message), null);
    }

    void OnApplicationQuit()
    {
        StopConnect();
    }

    [ContextMenu("StopConnect")]
    private void StopConnect()
    {
        Debug.Log("[TCP]断开连接");
        isConnected = false;
        CloseConnection();
        Debug.Log("[TCP]客户端已断开连接");
    }
}

public class Server2ClientEvt
{
    public string Message;
}