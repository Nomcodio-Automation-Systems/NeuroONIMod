using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Websocket;
using UnityEngine;

namespace NeuroSdk;

[PublicAPI]
/// <summary>
/// Provides helper methods for setting up the Neuro SDK runtime components in a scene.
/// </summary>
/// <pre>The SDK prefab has not already been added to the active scene when manual initialization is requested.</pre>
/// <post>The required SDK GameObject and core components are created in the scene.</post>
public static partial class NeuroSdkSetup
{
    /// <summary>
    /// Initializes the Neuro SDK components in the current scene.
    /// </summary>
    /// <param name="game">The game identifier assigned to the created websocket connection.</param>
    /// <pre><paramref name="game"/> identifies the current integration target and the SDK prefab is not already present.</pre>
    /// <post>A new NeuroSdk GameObject exists with websocket, message queue, command handler, and action handler components.</post>
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