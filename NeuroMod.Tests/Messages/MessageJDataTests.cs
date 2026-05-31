using FluentAssertions;
using NeuroSdk.Messages.API;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NeuroMod.Tests.Messages;

/// <summary>
/// Tests for the MessageJData struct
/// Tests creation, properties, and data handling
/// </summary>
public class MessageJDataTests
{
    /// <summary>
    /// Test that MessageJData can be created with null data
    /// </summary>
    [Test]
    public void Constructor_WithNull_ShouldCreateInstance()
    {
        // Act
        MessageJData messageData = new(null);

        // Assert
        messageData.Data.Should().BeNull();
    }

    /// <summary>
    /// Test that MessageJData can be created with JObject
    /// </summary>
    [Test]
    public void Constructor_WithJObject_ShouldStoreData()
    {
        // Arrange
        JObject jObject = new()
        {
            ["key"] = "value",
            ["number"] = 42
        };

        // Act
        MessageJData messageData = new(jObject);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data.Should().BeSameAs(jObject);
        messageData.Data!["key"]!.Value<string>().Should().Be("value");
        messageData.Data!["number"]!.Value<int>().Should().Be(42);
    }

    /// <summary>
    /// Test that MessageJData can be created with JArray
    /// </summary>
    [Test]
    public void Constructor_WithJArray_ShouldStoreData()
    {
        // Arrange
        JArray jArray = ["item1", "item2", "item3"];

        // Act
        MessageJData messageData = new(jArray);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data.Should().BeSameAs(jArray);
        ((JArray)messageData.Data!).Count.Should().Be(3);
        messageData.Data![0]!.Value<string>().Should().Be("item1");
    }

    /// <summary>
    /// Test that MessageJData can be created with JValue
    /// </summary>
    [Test]
    public void Constructor_WithJValue_ShouldStoreData()
    {
        // Arrange
        JValue jValue = new("test string");

        // Act
        MessageJData messageData = new(jValue);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data.Should().BeSameAs(jValue);
        messageData.Data!.Value<string>().Should().Be("test string");
    }

    /// <summary>
    /// Test that MessageJData preserves complex nested structures
    /// </summary>
    [Test]
    public void Constructor_WithNestedStructure_ShouldPreserveStructure()
    {
        // Arrange
        JObject nested = new()
        {
            ["outer"] = new JObject
            {
                ["inner"] = new JObject
                {
                    ["value"] = "deep"
                }
            },
            ["array"] = new JArray { 1, 2, 3 }
        };

        // Act
        MessageJData messageData = new(nested);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data!["outer"]!["inner"]!["value"]!.Value<string>().Should().Be("deep");
        ((JArray)messageData.Data!["array"]!).Count.Should().Be(3);
    }

    /// <summary>
    /// Test that MessageJData is a struct and creates copies
    /// </summary>
    [Test]
    public void StructBehavior_ShouldCreateIndependentCopies()
    {
        // Arrange
        JObject jObject = new() { ["value"] = 1 };
        MessageJData messageData1 = new(jObject);

        // Act - Struct copy
        MessageJData messageData2 = messageData1;

        // Assert - Both should reference the same JObject
        messageData1.Data.Should().BeSameAs(messageData2.Data);
    }

    /// <summary>
    /// Test that MessageJData with different data are not equal
    /// </summary>
    [Test]
    public void DifferentData_ShouldNotBeEqual()
    {
        // Arrange
        JObject jObject1 = new() { ["value"] = 1 };
        JObject jObject2 = new() { ["value"] = 2 };
        MessageJData messageData1 = new(jObject1);
        MessageJData messageData2 = new(jObject2);

        // Assert
        messageData1.Data.Should().NotBeSameAs(messageData2.Data);
    }

    /// <summary>
    /// Test that MessageJData can handle empty JObject
    /// </summary>
    [Test]
    public void Constructor_WithEmptyJObject_ShouldStoreEmptyObject()
    {
        // Arrange
        JObject emptyObject = [];

        // Act
        MessageJData messageData = new(emptyObject);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data.Should().BeSameAs(emptyObject);
        ((JObject)messageData.Data!).Count.Should().Be(0);
    }

    /// <summary>
    /// Test that MessageJData can handle empty JArray
    /// </summary>
    [Test]
    public void Constructor_WithEmptyJArray_ShouldStoreEmptyArray()
    {
        // Arrange
        JArray emptyArray = [];

        // Act
        MessageJData messageData = new(emptyArray);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data.Should().BeSameAs(emptyArray);
        ((JArray)messageData.Data!).Count.Should().Be(0);
    }

    /// <summary>
    /// Test that MessageJData can store boolean JValue
    /// </summary>
    [Test]
    public void Constructor_WithBooleanJValue_ShouldStoreCorrectly()
    {
        // Arrange
        JValue trueValue = new(true);
        JValue falseValue = new(false);

        // Act
        MessageJData messageTrueData = new(trueValue);
        MessageJData messageFalseData = new(falseValue);

        // Assert
        messageTrueData.Data!.Value<bool>().Should().BeTrue();
        messageFalseData.Data!.Value<bool>().Should().BeFalse();
    }

    /// <summary>
    /// Test that MessageJData can store numeric JValue
    /// </summary>
    [Test]
    public void Constructor_WithNumericJValue_ShouldStoreCorrectly()
    {
        // Arrange
        JValue intValue = new(42);
        JValue doubleValue = new(3.14159);

        // Act
        MessageJData messageIntData = new(intValue);
        MessageJData messageDoubleData = new(doubleValue);

        // Assert
        messageIntData.Data!.Value<int>().Should().Be(42);
        messageDoubleData.Data!.Value<double>().Should().BeApproximately(3.14159, 0.00001);
    }

    /// <summary>
    /// Test that default MessageJData has null data
    /// </summary>
    [Test]
    public void Default_ShouldHaveNullData()
    {
        // Act
        MessageJData messageData = default;

        // Assert
        messageData.Data.Should().BeNull();
    }

    /// <summary>
    /// Test that MessageJData preserves JSON parsing results
    /// </summary>
    [Test]
    public void Constructor_WithParsedJson_ShouldPreserveData()
    {
        // Arrange
        string json = "{\"name\":\"test\",\"value\":123,\"active\":true}";
        JObject parsed = JObject.Parse(json);

        // Act
        MessageJData messageData = new(parsed);

        // Assert
        messageData.Data.Should().NotBeNull();
        messageData.Data!["name"]!.Value<string>().Should().Be("test");
        messageData.Data!["value"]!.Value<int>().Should().Be(123);
        messageData.Data!["active"]!.Value<bool>().Should().BeTrue();
    }
}