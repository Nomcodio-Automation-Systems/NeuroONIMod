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
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace NeuroSdk.Websocket;

/// <summary>
/// Manages WebSocket connection to the Neuro SDK server
/// Handles connection lifecycle, message queuing, and automatic reconnection
/// </summary>
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
    public static WebsocketConnection? Instance
    {
        get
        {
            if (!_instance)
            {
                Debug.LogWarning("Accessed WebsocketConnection.Instance without an instance being present");
            }
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
            Debug.Log("Destroying duplicate WebsocketConnection instance");
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
            Debug.LogError($"Failed to reconnect: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts the WebSocket connection
    /// </summary>
    private async Task StartWs()
    {
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
        }
        catch (Exception ex)
        {
            NeuroLogger.LogError($"Failed to start WebSocket connection: {ex.Message}", "WebsocketConnection");
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
            Debug.LogWarning($"Error closing existing connection: {ex.Message}");
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
            Debug.LogWarning($"Failed to parse URL parameter: {ex.Message}");
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
            Debug.LogWarning($"Failed to get URL from web request: {ex.Message}");
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
            Debug.LogWarning($"Failed to get URL from environment: {ex.Message}");
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

        _socket.OnOpen += () =>
        {
            NeuroLogger.Log("WebSocket connection opened successfully!", "WebsocketConnection");
            onConnected?.Invoke();
        };
        _socket.OnMessage += bytes =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            NeuroLogger.LogDebug($"Received message: {message}", "WebsocketConnection");
            _ = Task.Run(async () => await ReceiveMessage(message));
        };
        _socket.OnError += error =>
        {
            NeuroLogger.LogError($"WebSocket error: {error}", "WebsocketConnection");
            onError?.Invoke(error);
            if (error != "Unable to connect to the remote server")
            {
                Debug.LogError("WebSocket connection encountered an error!");
                Debug.LogError(error);
            }
        };
        _socket.OnClose += code =>
        {
            NeuroLogger.LogWarning($"WebSocket connection closed with code {code}", "WebsocketConnection");
            onDisconnected?.Invoke(code);
            if (code != WebSocketCloseCode.Abnormal)
            {
                Debug.LogWarning($"WebSocket connection closed with code {code}!");
            }
            _ = Task.Run(async () => await Reconnect());
        };

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
            Debug.Log($"Sending WebSocket message: {message}");
            await _socket!.SendText(message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send WebSocket message: {ex.Message}");
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
            Debug.Log($"Received WebSocket message: {msgData}");

            JObject message = JObject.Parse(msgData);
            string? command = message["command"]?.Value<string>();
            MessageJData data = new(message["data"]);

            if (command == null)
            {
                Debug.LogError("Received command that could not be deserialized");
                return;
            }

            commandHandler.Handle(command, data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to process received message: {ex.Message}");
            Debug.LogError(ex);
        }
    }

    #endregion Message Processing

    #region Public API

    /// <summary>
    /// Queues a message for sending
    /// </summary>
    /// <param name="messageBuilder">The message builder to queue</param>
    public void Send(OutgoingMessageBuilder messageBuilder)
    {
        messageQueue.Enqueue(messageBuilder);
    }

    /// <summary>
    /// Sends a message immediately without queuing
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    public void SendImmediate(OutgoingMessageBuilder messageBuilder)
    {
        try
        {
            string message = Jason.Serialize(messageBuilder.GetWsMessage());

            if (_socket?.State is not WebSocketState.Open)
            {
                Debug.LogError($"WebSocket not open - failed to send immediate message: {message}");
                return;
            }

            Debug.Log($"Sending immediate WebSocket message: {message}");
            _socket.SendText(message);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to send immediate message: {ex.Message}");
        }
    }

    #endregion Public API

    #region Deprecated Methods

    /// <summary>
    /// Attempts to send a message via the singleton instance
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    [Obsolete("Use WebsocketConnection.Instance.Send instead")]
    public static void TrySend(OutgoingMessageBuilder messageBuilder)
    {
        if (Instance == null)
        {
            Debug.LogError("Cannot send message - WebsocketConnection instance is null");
            return;
        }
        Instance.Send(messageBuilder);
    }

    /// <summary>
    /// Attempts to send an immediate message via the singleton instance
    /// </summary>
    /// <param name="messageBuilder">The message builder to send</param>
    [Obsolete("Use WebsocketConnection.Instance.SendImmediate instead")]
    public static void TrySendImmediate(OutgoingMessageBuilder messageBuilder)
    {
        if (Instance == null)
        {
            Debug.LogError("Cannot send immediate message - WebsocketConnection instance is null");
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
        Debug.LogError(errMessage);
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