using FluentAssertions;
using NeuroSdk.Utilities;
using NUnit.Framework;

namespace NeuroMod.Tests.Utilities;

/// <summary>
/// Tests for the FormatString utility class
/// </summary>
[TestFixture]
public class FormatStringTests
{
    /// <summary>
    /// Test that FormatString can format a simple string with one parameter
    /// </summary>
    [Test]
    public void Format_SingleParameter_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "Hello {0}!";

        // Act
        string result = formatStr.Format("World");

        // Assert
        result.Should().Be("Hello World!");
    }

    /// <summary>
    /// Test that FormatString can format with multiple parameters
    /// </summary>
    [Test]
    public void Format_MultipleParameters_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "{0} {1} {2}";

        // Act
        string result = formatStr.Format("One", "Two", "Three");

        // Assert
        result.Should().Be("One Two Three");
    }

    /// <summary>
    /// Test that FormatString can format with numeric parameters
    /// </summary>
    [Test]
    public void Format_NumericParameters_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "Value: {0}, Count: {1}";

        // Act
        string result = formatStr.Format(42, 100);

        // Assert
        result.Should().Be("Value: 42, Count: 100");
    }

    /// <summary>
    /// Test that FormatString can format with mixed type parameters
    /// </summary>
    [Test]
    public void Format_MixedTypeParameters_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "Name: {0}, Age: {1}, Active: {2}";

        // Act
        string result = formatStr.Format("John", 30, true);

        // Assert
        result.Should().Be("Name: John, Age: 30, Active: True");
    }

    /// <summary>
    /// Test that FormatString can be created using implicit conversion from string
    /// </summary>
    [Test]
    public void ImplicitConversion_FromString_ShouldWork()
    {
        // Arrange & Act
        FormatString formatStr = "Test {0}";

        // Assert
        formatStr.Should().NotBeNull();
        formatStr.Format("Value").Should().Be("Test Value");
    }

    /// <summary>
    /// Test that FormatString handles empty format string
    /// </summary>
    [Test]
    public void Format_EmptyFormatString_ShouldReturnEmptyString()
    {
        // Arrange
        FormatString formatStr = "";

        // Act
        string result = formatStr.Format();

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Test that FormatString without placeholders returns original string
    /// </summary>
    [Test]
    public void Format_NoPlaceholders_ShouldReturnOriginalString()
    {
        // Arrange
        FormatString formatStr = "Hello World";

        // Act
        string result = formatStr.Format();

        // Assert
        result.Should().Be("Hello World");
    }

    /// <summary>
    /// Test that FormatString can format with repeated parameter references
    /// </summary>
    [Test]
    public void Format_RepeatedParameters_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "{0} and {0} again";

        // Act
        string result = formatStr.Format("Test");

        // Assert
        result.Should().Be("Test and Test again");
    }

    /// <summary>
    /// Test that FormatString can format with complex format specifiers
    /// </summary>
    [Test]
    public void Format_WithFormatSpecifiers_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "Price: {0:C2}, Percentage: {1:P1}";

        // Act - Note: Currency format depends on culture, so we'll just check it contains the values
        string result = formatStr.Format(123.456, 0.789);

        // Assert
        result.Should().Contain("123");
        result.Should().Contain("78");
    }

    /// <summary>
    /// Test that FormatString can be reused multiple times
    /// </summary>
    [Test]
    public void Format_MultipleInvocations_ShouldWorkCorrectly()
    {
        // Arrange
        FormatString formatStr = "Value: {0}";

        // Act
        string result1 = formatStr.Format("First");
        string result2 = formatStr.Format("Second");
        string result3 = formatStr.Format("Third");

        // Assert
        result1.Should().Be("Value: First");
        result2.Should().Be("Value: Second");
        result3.Should().Be("Value: Third");
    }

    /// <summary>
    /// Test that FormatString handles null parameters gracefully
    /// </summary>
    [Test]
    public void Format_NullParameter_ShouldHandleGracefully()
    {
        // Arrange
        FormatString formatStr = "Value: {0}";

        // Act
        string result = formatStr.Format((object?)null);

        // Assert
        result.Should().Be("Value: ");
    }

    /// <summary>
    /// Test that FormatString works with indexed placeholders in any order
    /// </summary>
    [Test]
    public void Format_OutOfOrderPlaceholders_ShouldFormatCorrectly()
    {
        // Arrange
        FormatString formatStr = "{2} {0} {1}";

        // Act
        string result = formatStr.Format("A", "B", "C");

        // Assert
        result.Should().Be("C A B");
    }
}