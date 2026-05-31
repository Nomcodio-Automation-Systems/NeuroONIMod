#nullable enable

using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using NeuroSdk.Actions;
using NeuroSdk.Json;
using NeuroSdk.Messages.Outgoing;
using NeuroSdk.Websocket;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NeuroMod.Integration.Api;

namespace NeuroSdk.Examples;

/// <summary>
/// Demonstrates a simple Tic Tac Toe integration that uses Neuro actions to let the remote player choose moves.
/// </summary>
/// <pre>The scene contains nine board cells as child transforms plus a reset button.</pre>
/// <post>The component manages the board state, announces turns to Neuro, and exposes a play action when it is the remote side's turn.</post>
public class TicTacToe : MonoBehaviour
{
    [SerializeField]
    private readonly GameObject resetButton = null!;

    private bool _playerTurn = true;

    /// <summary>
    /// Announces the start of a new Tic Tac Toe game to Neuro.
    /// </summary>
    /// <pre>The scene has been initialized and the Neuro API client is available.</pre>
    /// <post>The remote side has received initial context describing the new game and turn ownership.</post>
    private void Start()
    {
        ApiClient.Instance.SendContext("A Tic Tac Toe game has started. You are playing as O.", true);
    }

    [UsedImplicitly]
    /// <summary>
    /// Handles a local player move and, if the game continues, prompts Neuro to play the next move.
    /// </summary>
    /// <param name="cell">The board cell selected by the local player.</param>
    /// <pre><paramref name="cell"/> is one of the board cells and it is currently the local player's turn.</pre>
    /// <post>The local move has been applied when legal and either the reset path or Neuro turn-prompt path has been triggered.</post>
    public void PlayerPlayInCell(GameObject cell)
    {
        if (!_playerTurn)
        {
            return;
        }

        if (cell.transform.GetChild(0).gameObject.activeSelf || cell.transform.GetChild(1).gameObject.activeSelf)
        {
            return;
        }

        _playerTurn = false;

        cell.transform.GetChild(0).gameObject.SetActive(true);
        ApiClient.Instance.SendContext($"Your opponent played an X in the {cell.name} cell.", false);

        if (!CheckWin())
        {
            ActionWindow.Create(gameObject)
                .SetForce(0, "It is your turn. Please place an O.", "", false)
                .AddAction(new PlayOAction(this))
                .Register();
        }
        else
        {
            EnableReset();
        }
    }

    /// <summary>
    /// Applies the remote player's move and advances the game state.
    /// </summary>
    /// <param name="cell">The board cell selected by Neuro.</param>
    /// <pre><paramref name="cell"/> is an empty board cell chosen by the validated Neuro action.</pre>
    /// <post>The remote move has been applied and either the local player's turn resumes or the reset path has been triggered.</post>
    public void BotPlayInCell(GameObject cell)
    {
        cell.transform.GetChild(1).gameObject.SetActive(true);
        if (!CheckWin())
        {
            _playerTurn = true;
        }
        else
        {
            EnableReset();
        }
    }

    /// <summary>
    /// Evaluates the board for a win or tie condition and reports the outcome to Neuro.
    /// </summary>
    /// <returns><see langword="true"/> when the game has ended; otherwise <see langword="false"/>.</returns>
    /// <pre>The first nine child transforms represent the board cells and each cell exposes X and O markers as its first two children.</pre>
    /// <post>When the game has ended, the appropriate result message has been sent to Neuro.</post>
    private bool CheckWin()
    {
        if (CheckLine(0, 0, 1, 2) || CheckLine(0, 3, 4, 5) || CheckLine(0, 6, 7, 8) ||
            CheckLine(0, 0, 3, 6) || CheckLine(0, 1, 4, 7) || CheckLine(0, 2, 5, 8) ||
            CheckLine(0, 0, 4, 8) || CheckLine(0, 2, 4, 6))
        {
                    ApiClient.Instance.SendContext("You lost. Better luck next time.", false);
            return true;
        }

        if (CheckLine(1, 0, 1, 2) || CheckLine(1, 3, 4, 5) || CheckLine(1, 6, 7, 8) ||
            CheckLine(1, 0, 3, 6) || CheckLine(1, 1, 4, 7) || CheckLine(1, 2, 5, 8) ||
            CheckLine(1, 0, 4, 8) || CheckLine(1, 2, 4, 6))
        {
                    ApiClient.Instance.SendContext("You won. Congratulations.", false);
            return true;
        }

        if (transform.Cast<Transform>().Take(9).All(c => c.GetChild(0).gameObject.activeSelf || c.GetChild(1).gameObject.activeSelf))
        {
                    ApiClient.Instance.SendContext("It's a tie. No one won.", false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether the supplied line of cells is occupied by the same player marker.
    /// </summary>
    /// <param name="player">The marker index, where 0 is X and 1 is O.</param>
    /// <param name="c1">The first cell index.</param>
    /// <param name="c2">The second cell index.</param>
    /// <param name="c3">The third cell index.</param>
    /// <returns><see langword="true"/> when all three cells contain the selected player's marker.</returns>
    /// <pre>The supplied cell indices refer to valid board children.</pre>
    /// <post>The result reflects whether the selected player currently owns the full line.</post>
    private bool CheckLine(int player, int c1, int c2, int c3)
    {
        return transform.GetChild(c1).GetChild(player).gameObject.activeSelf &&
               transform.GetChild(c2).GetChild(player).gameObject.activeSelf &&
               transform.GetChild(c3).GetChild(player).gameObject.activeSelf;
    }

    /// <summary>
    /// Reveals the reset button when the game ends.
    /// </summary>
    /// <pre>The reset button reference points to the scene object used for restarting the game.</pre>
    /// <post>The reset button is active in the scene.</post>
    private void EnableReset()
    {
        resetButton.SetActive(true);
    }

    [UsedImplicitly]
    /// <summary>
    /// Clears the board and starts a new game.
    /// </summary>
    /// <pre>The scene contains the board cells and reset button used by this example.</pre>
    /// <post>The board has been cleared, the reset button hidden, Neuro has been notified of the new game, and the local turn flag has been reset.</post>
    public void ResetBoard()
    {
        resetButton.SetActive(false);

        foreach (Transform cell in transform)
        {
            cell.GetChild(0).gameObject.SetActive(false);
            cell.GetChild(1).gameObject.SetActive(false);
        }

        ApiClient.Instance.SendContext("A new Tic Tac Toe game has started. You are playing as O.", true);

        _playerTurn = true;
    }
}

/// <summary>
/// Action that lets Neuro choose a Tic Tac Toe cell for the O move.
/// </summary>
/// <pre>The Tic Tac Toe example is awaiting a remote move and can enumerate the currently empty cells.</pre>
/// <post>The action validates the chosen cell against the live board state and applies the move when executed.</post>
public class PlayOAction(TicTacToe ticTacToe) : NeuroAction<GameObject>
{
    private readonly TicTacToe _ticTacToe = ticTacToe;

    /// <summary>
    /// Gets the protocol name for the Tic Tac Toe move action.
    /// </summary>
    public override string Name => "play";

    /// <summary>
    /// Gets the human-readable description for the Tic Tac Toe move action.
    /// </summary>
    protected override string Description => "Place an O in the specified cell.";

    /// <summary>
    /// Gets the JSON schema that restricts moves to currently available cells.
    /// </summary>
    protected override JsonSchema Schema => new()
    {
        Type = JsonSchemaType.Object,
        Required = ["cell"],
        Properties = new Dictionary<string, JsonSchema>
        {
            ["cell"] = QJS.Enum(GetAvailableCells())
        }
    };

    /// <summary>
    /// Validates the requested Tic Tac Toe move against the current board state.
    /// </summary>
    /// <param name="actionData">The incoming request payload.</param>
    /// <param name="cell">Receives the selected board cell when validation succeeds.</param>
    /// <returns>The validation result for the move request.</returns>
    /// <pre><paramref name="actionData"/> contains a <c>cell</c> field naming one of the currently empty cells.</pre>
    /// <post>On success <paramref name="cell"/> contains the matching board cell game object.</post>
    protected override ExecutionResult Validate(ActionJData actionData, out GameObject? cell)
    {
        string? desiredCell = actionData.Data?["cell"]?.Value<string>();
        if (string.IsNullOrEmpty(desiredCell))
        {
            cell = null;
            return ExecutionResult.Failure(Strings.ActionFailedMissingRequiredParameter.Format("cell"));
        }

        string[] cells = [.. GetAvailableCells()];
        if (!cells.Contains(desiredCell))
        {
            cell = null;
            return ExecutionResult.Failure(Strings.ActionFailedInvalidParameter.Format("cell"));
        }

        cell = _ticTacToe.transform.Find(desiredCell)?.gameObject;
        return ExecutionResult.Success();
    }

    /// <summary>
    /// Applies the validated remote move to the board.
    /// </summary>
    /// <param name="cell">The validated board cell to play in.</param>
    /// <returns>A completed task after the move has been applied.</returns>
    /// <pre><paramref name="cell"/> was produced by successful validation against the current board state.</pre>
    /// <post>The Tic Tac Toe example has applied the remote move to the selected cell.</post>
    protected override UniTask ExecuteAsync(GameObject? cell)
    {
        _ticTacToe.BotPlayInCell(cell!);
        return UniTask.CompletedTask;
    }

    /// <summary>
    /// Enumerates the names of the currently empty board cells.
    /// </summary>
    /// <returns>The names of all cells that can currently receive a move.</returns>
    /// <pre>The first nine child transforms of the board represent the playable cells.</pre>
    /// <post>Only cells without an active X or O marker are returned.</post>
    private IEnumerable<string> GetAvailableCells()
    {
        for (int i = 0; i < 9; i++)
        {
            if (!_ticTacToe.transform.GetChild(i).GetChild(0).gameObject.activeSelf &&
                !_ticTacToe.transform.GetChild(i).GetChild(1).gameObject.activeSelf)
            {
                yield return _ticTacToe.transform.GetChild(i).name;
            }
        }
    }
}