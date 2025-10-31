using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroSdk;

[PublicAPI]
public static partial class NeuroSdkSetup
{
    /// <summary>
    /// Use this only if you haven't already added the NeuroSdk prefab in your scenes.
    /// </summary>
    /// <param name="game"></param>
    public static void Initialize(string game)
    {
        GameObject obj = new("NeuroSdk");
        WebsocketConnection connection = obj.AddComponent<WebsocketConnection>();
        connection.game = game;
        connection.messageQueue = obj.AddComponent<MessageQueue>();
        connection.commandHandler = obj.AddComponent<CommandHandler>();
        obj.AddComponent<NeuroActionHandler>();
    }
}