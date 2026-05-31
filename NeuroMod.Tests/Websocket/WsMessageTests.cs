using FluentAssertions;
using NeuroSdk.Websocket;
using NUnit.Framework;
using System;

namespace NeuroMod.Tests.Websocket;

/// <summary>
/// Tests for the WsMessage class
/// Tests message creation, properties, serialization, and equality
/// </summary>
public class WsMessageTests
{
    /// <summary>
    /// Test that WsMessage can be created with all required parameters
    /// </summary>
    [Test]
    public void Constructor_WithValidParameters_ShouldCreateMessage()
    {
        // Arrange
        string command = "test_command";
        object data = new { value = 42 };
        string game = "test_game";

        // Act
        WsMessage message = new(command, data, game);

        // Assert
        message.Should().NotBeNull();
        message.Command.Should().Be(command);
        message.Data.Should().BeSameAs(data);
        message.Game.Should().Be(game);
    }

    /// <summary>
    /// Test that WsMessage can be created with null data
    /// </summary>
    [Test]
    public void Constructor_WithNullData_ShouldCreateMessage()
    {
        // Arrange
        string command = "test_command";
        string game = "test_game";

        // Act
        WsMessage message = new(command, null, game);

        // Assert
        message.Should().NotBeNull();
        message.Command.Should().Be(command);
        message.Data.Should().BeNull();
        message.Game.Should().Be(game);
    }

    /// <summary>
    /// Test that WsMessage throws ArgumentNullException for null command
    /// </summary>
    [Test]
    public void Constructor_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        string? command = null;
        string game = "test_game";

        // Act & Assert
        System.Action act = () => new WsMessage(command!, null, game);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("command");
    }

    /// <summary>
    /// Test that WsMessage throws ArgumentNullException for null game
    /// </summary>
    [Test]
    public void Constructor_WithNullGame_ShouldThrowArgumentNullException()
    {
        // Arrange
        string command = "test_command";
        string? game = null;

        // Act & Assert
        System.Action act = () => new WsMessage(command, null, game!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("game");
    }

    /// <summary>
    /// Test that WsMessage equality works correctly
    /// </summary>
    [Test]
    public void Equals_WithSameValues_ShouldReturnTrue()
    {
        // Arrange
        string command = "test_command";
        object data = new { value = 42 };
        string game = "test_game";

        WsMessage message1 = new(command, data, game);
        WsMessage message2 = new(command, data, game);

        // Act & Assert
        message1.Equals(message2).Should().BeTrue();
    }

    /// <summary>
    /// Test that WsMessage inequality works correctly
    /// </summary>
    [Test]
    public void Equals_WithDifferentCommands_ShouldReturnFalse()
    {
        // Arrange
        WsMessage message1 = new("command1", null, "game");
        WsMessage message2 = new("command2", null, "game");

        // Act & Assert
        message1.Equals(message2).Should().BeFalse();
    }

    /// <summary>
    /// Test that WsMessage Equals returns false for null
    /// </summary>
    [Test]
    public void Equals_WithNull_ShouldReturnFalse()
    {
        // Arrange
        WsMessage message = new("command", null, "game");

        // Act & Assert
        message.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Test that WsMessage Equals returns false for different type
    /// </summary>
    [Test]
    public void Equals_WithDifferentType_ShouldReturnFalse()
    {
        // Arrange
        WsMessage message = new("command", null, "game");
        object otherType = "string";

        // Act & Assert
        message.Equals(otherType).Should().BeFalse();
    }

    /// <summary>
    /// Test that WsMessage GetHashCode returns consistent values
    /// </summary>
    [Test]
    public void GetHashCode_WithSameValues_ShouldReturnSameHash()
    {
        // Arrange
        string command = "test_command";
        object data = new { value = 42 };
        string game = "test_game";

        WsMessage message1 = new(command, data, game);
        WsMessage message2 = new(command, data, game);

        // Act
        int hash1 = message1.GetHashCode();
        int hash2 = message2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    /// <summary>
    /// Test that WsMessage GetHashCode returns different values for different messages
    /// </summary>
    [Test]
    public void GetHashCode_WithDifferentValues_ShouldReturnDifferentHash()
    {
        // Arrange
        WsMessage message1 = new("command1", null, "game");
        WsMessage message2 = new("command2", null, "game");

        // Act
        int hash1 = message1.GetHashCode();
        int hash2 = message2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    /// <summary>
    /// Test that WsMessage ToString returns a readable representation
    /// </summary>
    [Test]
    public void ToString_ShouldReturnReadableRepresentation()
    {
        // Arrange
        WsMessage message = new("test_command", new { value = 42 }, "test_game");

        // Act
        string result = message.ToString();

        // Assert
        result.Should().Contain("WsMessage");
        result.Should().Contain("test_command");
        result.Should().Contain("test_game");
    }

    /// <summary>
    /// Test that WsMessage properties are read-only
    /// </summary>
    [Test]
    public void Properties_ShouldBeReadOnly()
    {
        // Arrange
        // IDE0059: Unn�tige Zuweisung eines Werts zu "message".
        // Die Variable "message" wird nur f�r typeof(WsMessage) verwendet, nicht f�r die Tests.
        // Fix: Entferne die unn�tige Zuweisung.

        // Assert - Properties should not have setters
        typeof(WsMessage).GetProperty("Command")!.CanWrite.Should().BeFalse();
        typeof(WsMessage).GetProperty("Game")!.CanWrite.Should().BeFalse();
        typeof(WsMessage).GetProperty("Data")!.CanWrite.Should().BeFalse();
    }

    /// <summary>
    /// Test that WsMessage handles complex data objects
    /// </summary>
    [Test]
    public void Constructor_WithComplexData_ShouldStoreData()
    {
        // Arrange
        var complexData = new
        {
            nested = new { value = 1 },
            array = new[] { 1, 2, 3 },
            text = "test"
        };

        // Act
        WsMessage message = new("command", complexData, "game");

        // Assert
        message.Data.Should().BeSameAs(complexData);
    }

    /// <summary>
    /// Test that WsMessage handles empty strings
    /// </summary>
    [Test]
    public void Constructor_WithEmptyStrings_ShouldCreateMessage()
    {
        // Arrange
        string command = "";
        string game = "";

        // Act
        WsMessage message = new(command, null, game);

        // Assert
        message.Command.Should().BeEmpty();
        message.Game.Should().BeEmpty();
    }

    /// <summary>
    /// Test that WsMessage Equals handles null data correctly
    /// </summary>
    [Test]
    public void Equals_WithBothNullData_ShouldReturnTrue()
    {
        // Arrange
        WsMessage message1 = new("command", null, "game");
        WsMessage message2 = new("command", null, "game");

        // Act & Assert
        message1.Equals(message2).Should().BeTrue();
    }

    /// <summary>
    /// Test that WsMessage Equals handles one null data correctly
    /// </summary>
    [Test]
    public void Equals_WithOneNullData_ShouldReturnFalse()
    {
        // Arrange
        WsMessage message1 = new("command", new { value = 1 }, "game");
        WsMessage message2 = new("command", null, "game");

        // Act & Assert
        message1.Equals(message2).Should().BeFalse();
    }

    /// <summary>
    /// Test that WsMessage handles special characters in strings
    /// </summary>
    [Test]
    public void Constructor_WithSpecialCharacters_ShouldStoreCorrectly()
    {
        // Arrange
        string command = "test_\"command\"_\n_with_special";
        string game = "test_'game'_\t_chars";

        // Act
        WsMessage message = new(command, null, game);

        // Assert
        message.Command.Should().Be(command);
        message.Game.Should().Be(game);
    }

    /// <summary>
    /// Test that WsMessage GetHashCode is consistent across multiple calls
    /// </summary>
    [Test]
    public void GetHashCode_MultipleCalls_ShouldReturnSameValue()
    {
        // Arrange
        WsMessage message = new("command", new { value = 42 }, "game");

        // Act
        int hash1 = message.GetHashCode();
        int hash2 = message.GetHashCode();
        int hash3 = message.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
        hash2.Should().Be(hash3);
    }
}