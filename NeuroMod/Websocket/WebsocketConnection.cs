#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NativeWebSocket;
using NeuroMod;
using NeuroSdk.Messages.API;
using NeuroSdk.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace NeuroSdk.Websocket;

/// <summary>
/// Manages WebSocket connection to the Neuro SDK server
/// Handles connection lifecycle, message queuing, and automatic reconnection
/// </summary>
/// <pre>
/// The Unity runtime can host a single websocket connection component and the outbound queue is initialized.
/// </pre>
/// <post>
/// The component can establish a websocket connection, dispatch inbound commands, and flush queued outbound messages.
/// </post>
[PublicAPI]
public sealed class WebsocketConnection : MonoBehaviour
{
    #region Constants

    private const float RECONNECT_INTERVAL = 3f;
    private const string WEBSOCKET_URL_PARAM = "WebSocketURL=";
    private const string ENV_VAR_NAME = "NEURO_SDK_WS_URL";

    #endregion Constants

    #region Private Fields

    private static WebsocketConnection? _instance;
    private static WebSocket? _socket;

    #endregion Private Fields

    #region Public Properties

    /// <summary>
    /// Singleton instance of the WebSocket connection
    /// </summary>
    /// <pre>
    /// At most one live websocket connection component should own the singleton.
    /// </pre>
    /// <post>
    /// The current singleton connection instance is returned when available.
    /// </post>
    public static WebsocketConnection? Instance
    {
        get
        {
            return _instance;
        }
        private set => _instance = value;
    }

    /// <summary>
    /// The game identifier for messages
    /// </summary>
    public string game = null!;

    /// <summary>
    /// Queue for outgoing messages
    /// </summary>
    public MessageQueue messageQueue = null!;

    /// <summary>
    /// Handler for incoming commands
    /// </summary>
    public CommandHandler commandHandler = null!;

    /// <summary>
    /// Indicates if the WebSocket is currently connected
    /// </summary>
    public bool IsConnected => _socket?.State == WebSocketState.Open;

    /// <summary>
    /// Event triggered when connection is established
    /// </summary>
    public UnityEvent? onConnected;

    /// <summary>
    /// Event triggered when an error occurs
    /// </summary>
    public UnityEvent<string>? onError;

    /// <summary>
    /// Event triggered when connection is closed
    /// </summary>
    public UnityEvent<WebSocketCloseCode>? onDisconnected;

    #endregion Public Properties

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance)
        {
            NeuroLogger.LogWarning("Destroying duplicate WebsocketConnection instance", "WebsocketConnection");
            Destroy(this);
            return;
        }

        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    private void Start()
    {
        NeuroLogger.Log("WebSocket component initialized, starting connection task...", "WebsocketConnection");
        _ = Task.Run(async () => await StartWs());
    }

    private void Update()
    {
        if (_socket?.State is not WebSocketState.Open)
        {
            return;
        }

        ProcessMessageQueue();

#if !UNITY_WEBGL || UNITY_EDITOR
        _socket.DispatchMessageQueue();
#endif
    }

    #endregion Unity Lifecycle

    #region Connection Management

    /// <summary>
    /// Attempts to reconnect to the WebSocket server after a delay
    /// </summary>
    private async Task Reconnect()
    {
        try
        {
            await Task.Yield();
            await Task.Delay(TimeSpan.FromSeconds(RECONNECT_INTERVAL));
            await StartWs();
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to reconnect: {ex.Message}", "WebsocketConnection");
        }
    }

    /// <summary>
    /// Starts the WebSocket connection
    /// </summary>
    private async Task StartWs()
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            NeuroLogger.Log("Starting WebSocket connection...", "WebsocketConnection");
            await CloseExistingConnection();
            string? websocketUrl = await GetWebsocketUrl();

            if (string.IsNullOrEmpty(websocketUrl))
            {
                NeuroLogger.LogError("Could not retrieve WebSocket URL", "WebsocketConnection");
                LogWebsocketUrlError();
                return;
            }

            NeuroLogger.Log($"Attempting to connect to: {websocketUrl}", "WebsocketConnection");
            await EstablishConnection(websocketUrl!);
            NeuroLogger.Log($"StartWs completed in {sw.ElapsedMilliseconds}ms", "WebsocketConnection");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to start WebSocket connection: {ex.Message}", "WebsocketConnection");
            NeuroLogger.LogException(ex, "WebsocketConnection.StartWs", "WebsocketConnection");
            onError?.Invoke(ex.Message);
        }
    }

    /// <summary>
    /// Closes existing WebSocket connection if present
    /// </summary>
    private async Task CloseExistingConnection()
    {
        try
        {
            if (_socket?.State is WebSocketState.Open or WebSocketState.Connecting)
            {
                await _socket.Close();
            }
        }
        catch (Exception ex)
        {
            NeuroLogger.LogWarning($"Error closing existing connection: {ex.Message}", "WebsocketConnection");
        }
    }

    /// <summary>
    /// Retrieves the WebSocket URL from various sources
    /// </summary>
    private async Task<string?> GetWebsocketUrl()
    {
        NeuroLogger.LogDebug("Attempting to get WebSocket URL...", "WebsocketConnection");

        // Try URL parameter first
        string? websocketUrl = GetUrlFromParameter();
        NeuroLogger.LogDebug($"URL parameter result: {websocketUrl ?? "null"}", "WebsocketConnection");

        if (!string.IsNullOrEmpty(websocketUrl))
        {
            return websocketUrl;
        }

        // Try web request
        websocketUrl = await GetUrlFromWebRequest();
        NeuroLogger.LogDebug($"Web request result: {websocketUrl ?? "null"}", "WebsocketConnection");

        if (!string.IsNullOrEmpty(websocketUrl))
        {
            return websocketUrl;
        }

        // Try environment variables
        websocketUrl = GetUrlFromEnvironment();
        NeuroLogger.LogDebug($"Environment variable result: {websocketUrl ?? "null"}", "WebsocketConnection");

        if (!string.IsNullOrEmpty(websocketUrl))
        {
            return websocketUrl;
        }

        // Final fallback: use ConfigManager setting
        websocketUrl = ConfigManager.Instance?.Config?.Neuro?.EndpointUrl;
        NeuroLogger.LogDebug($"ConfigManager result: {websocketUrl ?? "null"}", "WebsocketConnection");
        return websocketUrl;
    }

    /// <summary>
    /// Extracts WebSocket URL from URL parameters
    /// </summary>
    private string? GetUrlFromParameter()
    {
        try
        {
            if (Application.absoluteURL.IndexOf("?", StringComparison.Ordinal) == -1)
            {
                return null;
            }

            string[] urlSplits = Application.absoluteURL.Split('?');
            if (urlSplits.Length <= 1)
            {
                return null;
            }

            string[] urlParamSplits = urlSplits[1].Split([WEBSOCKET_URL_PARAM], StringSplitOptions.None);
            if (urlParamSplits.Length <= 1)
            {
                return null;
            }

            string? param = urlParamSplits[1].Split('&')[0];
            return string.IsNullOrEmpty(param) ? null : param;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogWarning($"Failed to parse URL parameter: {ex.Message}", "WebsocketConnection");
            return null;
        }
    }

    /// <summary>
    /// Attempts to get WebSocket URL from web request
    /// </summary>
    private async Task<string?> GetUrlFromWebRequest()
    {
        try
        {
            Uri uri = new(Application.absoluteURL);
            string requestUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/$env/{ENV_VAR_NAME}";

            using UnityWebRequest request = UnityWebRequest.Get(requestUrl);
            request.SendWebRequest();

            while (!request.isDone)
            {
                await UniTask.Yield();
            }

            return TryGetResult(request, out string result) ? result : null;
        }
        catch (Exception ex)
        {
            NeuroLogger.LogWarning($"Failed to get URL from web request: {ex.Message}", "WebsocketConnection");
            return null;
        }
    }

    /// <summary>
    /// Gets WebSocket URL from environment variables
    /// </summary>
    private string? GetUrlFromEnvironment()
    {
        try
        {
            return Environment.GetEnvironmentVariable(ENV_VAR_NAME, EnvironmentVariableTarget.Process) ??
                   Environment.GetEnvironmentVariable(ENV_VAR_NAME, EnvironmentVariableTarget.User) ??
                   Environment.GetEnvironmentVariable(ENV_VAR_NAME, EnvironmentVariableTarget.Machine);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogWarning($"Failed to get URL from environment: {ex.Message}", "WebsocketConnection");
            return null;
        }
    }

    /// <summary>
    /// Establishes WebSocket connection with the given URL
    /// </summary>
    private async Task EstablishConnection(string websocketUrl)
    {
        NeuroLogger.LogDebug($"Creating WebSocket for URL: {websocketUrl}", "WebsocketConnection");

        // WebSocket callbacks run on separate threads
        _socket = new WebSocket(websocketUrl);

            _socket.OnOpen += NeuroMod.Api.EventSubscriber.WrapWebsocketOpen("WebsocketConnection.OnOpen", () =>
            {
                NeuroLogger.Log("WebSocket connection opened successfully!", "WebsocketConnection");
                onConnected?.Invoke();
            });

            _socket.OnMessage += NeuroMod.Api.EventSubscriber.WrapWebsocketMessage("WebsocketConnection.OnMessage", bytes =>
            {
                string message = Encoding.UTF8.GetString(bytes);
                NeuroLogger.LogDebug($"Received message: {message}", "WebsocketConnection");
                _ = Task.Run(async () => await ReceiveMessage(message));
            });

            _socket.OnError += NeuroMod.Api.EventSubscriber.WrapWebsocketError("WebsocketConnection.OnError", error =>
            {
                NeuroLogger.LogError($"WebSocket error: {error}", "WebsocketConnection");
                onError?.Invoke(error);
                if (error != "Unable to connect to the remote server")
                {
                    NeuroLogger.LogError("WebSocket connection encountered an error!", "WebsocketConnection");
                    NeuroLogger.LogError(error, "WebsocketConnection");
                }
            });

            _socket.OnClose += NeuroMod.Api.EventSubscriber.WrapWebsocketClose("WebsocketConnection.OnClose", code =>
            {
                NeuroLogger.LogWarning($"WebSocket connection closed with code {code}", "WebsocketConnection");
                onDisconnected?.Invoke(code);
                if (code != WebSocketCloseCode.Abnormal)
                {
                    NeuroLogger.LogWarning($"WebSocket connection closed with code {code}!", "WebsocketConnection");
                }
                _ = Task.Run(async () => await Reconnect());
            });

        NeuroLogger.LogDebug("Attempting to connect WebSocket...", "WebsocketConnection");
        await _socket.Connect();
        NeuroLogger.LogDebug("WebSocket Connect() call completed", "WebsocketConnection");
    }

    #endregion Connection Management

    #region Message Processing

    /// <summary>
    /// Processes queued messages for sending
    /// </summary>
    private void ProcessMessageQueue()
    {
        while (messageQueue.Count > 0)
        {
            OutgoingMessageBuilder builder = messageQueue.Dequeue()!;
            _ = Task.Run(async () => await SendTask(builder));
        }
    }

    /// <summary>
    /// Sends a message asynchronously
    /// </summary>
    private async Task SendTask(OutgoingMessageBuilder builder)
    {
        try
        {
            string message = Jason.Serialize(builder.GetWsMessage());
            NeuroLogger.LogDebug($"Sending WebSocket message: {message}", "WebsocketConnection");
            Stopwatch sw = Stopwatch.StartNew();
            await _socket!.SendText(message);
            NeuroLogger.LogDebug($"Sent WebSocket message in {sw.ElapsedMilliseconds}ms", "WebsocketConnection");
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to send WebSocket message: {ex.Message}", "WebsocketConnection");
            NeuroLogger.LogException(ex, "WebsocketConnection.SendTask", "WebsocketConnection");
            messageQueue.Enqueue(builder);
        }
    }

    /// <summary>
    /// Processes incoming WebSocket messages
    /// </summary>
    private async Task ReceiveMessage(string msgData)
    {
        try
        {
            await UniTask.Yield();
            NeuroLogger.LogDebug($"Received WebSocket message: {msgData}", "WebsocketConnection");

            JObject message = JObject.Parse(msgData);
            string? command = message["command"]?.Value<string>();
            MessageJData data = new(message["data"]);

            if (command == null)
            {
                NeuroLogger.LogError("Received command that could not be deserialized", "WebsocketConnection");
                return;
            }

            commandHandler.Handle(command, data);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to process received message: {ex.Message}", "WebsocketConnection");
            NeuroLogger.LogException(ex, "WebsocketConnection.ReceiveMessage", "WebsocketConnection");
        }
    }

    #endregion Message Processing

    #region Public API

    /// <summary>
    /// Queues a message for sending
    /// </summary>
    /// <param name="messageBuilder">The message builder to queue</param>
    /// <pre>
    /// <paramref name="messageBuilder"/> contains a valid outgoing SDK message description.
    /// </pre>
    /// <post>
    /// The outgoing message has been appended to the transport queue.
    /// </post>
    public void Send(OutgoingMessageBuilder messageBuilder)
    {
        messageQueue.Enqueue(messageBuilder);
    }

    /// <summary>
    /// Sends a message immediately without queuing
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    /// <pre>
    /// <paramref name="messageBuilder"/> can be serialized immediately on the current websocket connection.
    /// </pre>
    /// <post>
    /// Immediate websocket delivery has been attempted without queueing the message.
    /// </post>
    public void SendImmediate(OutgoingMessageBuilder messageBuilder)
    {
        try
        {
            string message = Jason.Serialize(messageBuilder.GetWsMessage());

            if (_socket?.State is not WebSocketState.Open)
            {
                NeuroLogger.LogError($"WebSocket not open - failed to send immediate message: {message}", "WebsocketConnection");
                return;
            }

            NeuroLogger.LogDebug($"Sending immediate WebSocket message: {message}", "WebsocketConnection");
            _socket.SendText(message);
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to send immediate message: {ex.Message}", "WebsocketConnection");
            NeuroLogger.LogException(ex, "WebsocketConnection.SendImmediate", "WebsocketConnection");
        }
    }

    #endregion Public API

    #region Deprecated Methods

    /// <summary>
    /// Attempts to send a message via the singleton instance
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    /// <pre>
    /// The caller still relies on the deprecated singleton send path.
    /// </pre>
    /// <post>
    /// The message is forwarded through the live singleton instance when one exists.
    /// </post>
    [Obsolete("Use WebsocketConnection.Instance.Send instead")]
    public static void TrySend(OutgoingMessageBuilder messageBuilder)
    {
        if (Instance == null)
        {
            NeuroLogger.LogError("Cannot send message - WebsocketConnection instance is null", "WebsocketConnection");
            return;
        }
        Instance.Send(messageBuilder);
    }

    /// <summary>
    /// Attempts to send an immediate message via the singleton instance
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    /// <pre>
    /// The caller still relies on the deprecated singleton immediate-send path.
    /// </pre>
    /// <post>
    /// Immediate sending is forwarded through the live singleton instance when one exists.
    /// </post>
    [Obsolete("Use WebsocketConnection.Instance.SendImmediate instead")]
    public static void TrySendImmediate(OutgoingMessageBuilder messageBuilder)
    {
        if (Instance == null)
        {
            NeuroLogger.LogError("Cannot send immediate message - WebsocketConnection instance is null", "WebsocketConnection");
            return;
        }
        Instance.SendImmediate(messageBuilder);
    }

    #endregion Deprecated Methods

    #region Helper Methods

    /// <summary>
    /// Logs appropriate error message when WebSocket URL cannot be found
    /// </summary>
    private void LogWebsocketUrlError()
    {
        string errMessage = "Could not retrieve WebSocket URL.";
#if UNITY_EDITOR || !UNITY_WEBGL
        errMessage += $" You should set the {ENV_VAR_NAME} environment variable.";
#endif
#if UNITY_WEBGL
        errMessage += " You need to specify a WebSocketURL query parameter in the URL or open a local server that serves the NEURO_SDK_WS_URL environment variable. See the documentation for more information.";
#endif
        NeuroLogger.LogError(errMessage, "WebsocketConnection");
    }

    /// <summary>
    /// Attempts to extract result from Unity web request
    /// </summary>
    /// <param name="request">The Unity web request</param>
    /// <param name="result">The extracted result</param>
    /// <returns>True if result was successfully extracted</returns>
    private bool TryGetResult(UnityWebRequest request, out string result)
    {
        if (request is { isDone: true } && request.result == UnityWebRequest.Result.Success)
        {
            result = request.downloadHandler.text;
            return true;
        }

        result = "";
        return false;
    }

    #endregion Helper Methods
}