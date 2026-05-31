using FluentAssertions;
using NeuroSdk.Utilities;
using NUnit.Framework;
using System.Collections.Generic;

namespace NeuroMod.Tests.Utilities;

/// <summary>
/// Tests for the Jason serialization utility
/// </summary>
public class JasonTests
{
    /// <summary>
    /// Test that Jason can serialize a simple object
    /// </summary>
    [Test]
    public void Serialize_SimpleObject_ShouldReturnValidJson()
    {
        // Arrange
        var obj = new { name = "test", value = 42 };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"name\"");
        result.Should().Contain("\"test\"");
        result.Should().Contain("\"value\"");
        result.Should().Contain("42");
    }

    /// <summary>
    /// Test that Jason can serialize a complex nested object
    /// </summary>
    [Test]
    public void Serialize_NestedObject_ShouldReturnValidJson()
    {
        // Arrange
        var obj = new
        {
            outer = "value1",
            inner = new { nested = "value2", number = 123 }
        };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"outer\"");
        result.Should().Contain("\"inner\"");
        result.Should().Contain("\"nested\"");
        result.Should().Contain("123");
    }

    /// <summary>
    /// Test that Jason ignores null values during serialization
    /// </summary>
    [Test]
    public void Serialize_ObjectWithNullValues_ShouldIgnoreNulls()
    {
        // Arrange
        var obj = new { name = "test", nullValue = (string?)null, value = 42 };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"name\"");
        result.Should().Contain("\"value\"");
        result.Should().NotContain("\"nullValue\"");
    }

    /// <summary>
    /// Test that Jason can serialize arrays
    /// </summary>
    [Test]
    public void Serialize_Array_ShouldReturnValidJson()
    {
        // Arrange
        var obj = new { items = new[] { "item1", "item2", "item3" } };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"items\"");
        result.Should().Contain("\"item1\"");
        result.Should().Contain("\"item2\"");
        result.Should().Contain("\"item3\"");
    }

    /// <summary>
    /// Test that Jason can serialize dictionaries
    /// </summary>
    [Test]
    public void Serialize_Dictionary_ShouldReturnValidJson()
    {
        // Arrange
        Dictionary<string, object> obj = new()
        {
            ["key1"] = "value1",
            ["key2"] = 42,
            ["key3"] = true
        };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"key1\"");
        result.Should().Contain("\"value1\"");
        result.Should().Contain("\"key2\"");
        result.Should().Contain("42");
        result.Should().Contain("\"key3\"");
        result.Should().Contain("true");
    }

    /// <summary>
    /// Test that Jason can serialize null
    /// </summary>
    [Test]
    public void Serialize_Null_ShouldReturnNullString()
    {
        // Arrange
        object? obj = null;

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().Be("null");
    }

    /// <summary>
    /// Test that Jason can serialize empty object
    /// </summary>
    [Test]
    public void Serialize_EmptyObject_ShouldReturnEmptyJsonObject()
    {
        // Arrange
        var obj = new { };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().Be("{}");
    }

    /// <summary>
    /// Test that Jason can serialize strings with special characters
    /// </summary>
    [Test]
    public void Serialize_StringWithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var obj = new { message = "Test \"quoted\" and\nnewline" };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\\\"quoted\\\"");
        result.Should().Contain("\\n");
    }

    /// <summary>
    /// Test that Jason can serialize boolean values
    /// </summary>
    [Test]
    public void Serialize_BooleanValues_ShouldSerializeCorrectly()
    {
        // Arrange
        var obj = new { isTrue = true, isFalse = false };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("\"isTrue\":true");
        result.Should().Contain("\"isFalse\":false");
    }

    /// <summary>
    /// Test that Jason can serialize numeric types correctly
    /// </summary>
    [Test]
    public void Serialize_NumericTypes_ShouldSerializeCorrectly()
    {
        // Arrange
        var obj = new
        {
            intValue = 42,
            longValue = 9999999999L,
            doubleValue = 3.14159,
            floatValue = 2.71828f
        };

        // Act
        string result = Jason.Serialize(obj);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("42");
        result.Should().Contain("9999999999");
        result.Should().Contain("3.14159");
        result.Should().Contain("2.71828");
    }
}