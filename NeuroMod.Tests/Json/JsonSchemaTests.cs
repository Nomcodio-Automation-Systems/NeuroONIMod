using FluentAssertions;
using NeuroSdk.Json;
using NUnit.Framework;
using System.Collections.Generic;

namespace NeuroMod.Tests.Json;

/// <summary>
/// Tests for JsonSchema class
/// Tests schema creation, properties, type conversions, and validation constraints
/// </summary>
[TestFixture]
public class JsonSchemaTests
{
    /// <summary>
    /// Test that JsonSchema can be created with default constructor
    /// </summary>
    [Test]
    public void Constructor_Default_ShouldCreateInstance()
    {
        // Act
        JsonSchema schema = new();

        // Assert
        schema.Should().NotBeNull();
        schema.Type.Should().Be(JsonSchemaType.None);
    }

    /// <summary>
    /// Test that Type property converts to/from string correctly for String type
    /// </summary>
    [Test]
    public void Type_SetToString_ShouldConvertCorrectly()
    {
        // Arrange
        JsonSchema schema = new()
        {
            // Act
            Type = JsonSchemaType.String
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.String);
    }

    /// <summary>
    /// Test that Type property converts to/from string correctly for all types
    /// </summary>
    [Test]
    public void Type_AllTypes_ShouldConvertCorrectly()
    {
        // Arrange
        JsonSchemaType[] types =
        [
            JsonSchemaType.String,
            JsonSchemaType.Float,
            JsonSchemaType.Integer,
            JsonSchemaType.Boolean,
            JsonSchemaType.Object,
            JsonSchemaType.Array,
            JsonSchemaType.Null
        ];

        foreach (JsonSchemaType type in types)
        {
            JsonSchema schema = new()
            {
                // Act
                Type = type
            };

            // Assert
            schema.Type.Should().Be(type, $"{type} should convert correctly");
        }
    }

    /// <summary>
    /// Test that Properties dictionary is initialized when accessed
    /// </summary>
    [Test]
    public void Properties_WhenAccessed_ShouldBeInitialized()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        Dictionary<string, JsonSchema> properties = schema.Properties;

        // Assert
        properties.Should().NotBeNull();
        properties.Should().BeEmpty();
    }

    /// <summary>
    /// Test that Properties can be set and retrieved
    /// </summary>
    [Test]
    public void Properties_SetAndGet_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new();
        JsonSchema propertySchema = new() { Type = JsonSchemaType.String };

        // Act
        schema.Properties["testProperty"] = propertySchema;

        // Assert
        schema.Properties.Should().ContainKey("testProperty");
        schema.Properties["testProperty"].Type.Should().Be(JsonSchemaType.String);
    }

    /// <summary>
    /// Test that Enum list is initialized when accessed
    /// </summary>
    [Test]
    public void Enum_WhenAccessed_ShouldBeInitialized()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        List<object> enumValues = schema.Enum;

        // Assert
        enumValues.Should().NotBeNull();
        enumValues.Should().BeEmpty();
    }

    /// <summary>
    /// Test that Enum values can be added and retrieved
    /// </summary>
    [Test]
    public void Enum_AddValues_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        schema.Enum.Add("value1");
        schema.Enum.Add("value2");
        schema.Enum.Add("value3");

        // Assert
        schema.Enum.Should().HaveCount(3);
        schema.Enum.Should().ContainInOrder("value1", "value2", "value3");
    }

    /// <summary>
    /// Test that Required list is initialized when accessed
    /// </summary>
    [Test]
    public void Required_WhenAccessed_ShouldBeInitialized()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        List<string> required = schema.Required;

        // Assert
        required.Should().NotBeNull();
        required.Should().BeEmpty();
    }

    /// <summary>
    /// Test that Required fields can be added and retrieved
    /// </summary>
    [Test]
    public void Required_AddFields_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        schema.Required.Add("field1");
        schema.Required.Add("field2");

        // Assert
        schema.Required.Should().HaveCount(2);
        schema.Required.Should().ContainInOrder("field1", "field2");
    }

    /// <summary>
    /// Test that Items schema can be set for array type
    /// </summary>
    [Test]
    public void Items_SetSchema_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new() { Type = JsonSchemaType.Array };
        JsonSchema itemSchema = new() { Type = JsonSchemaType.String };

        // Act
        schema.Items = itemSchema;

        // Assert
        schema.Items.Should().NotBeNull();
        schema.Items!.Type.Should().Be(JsonSchemaType.String);
    }

    /// <summary>
    /// Test that string constraints can be set
    /// </summary>
    [Test]
    public void StringConstraints_SetValues_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.String,             // Act
            MinLength = 5,
            MaxLength = 100,
            Pattern = "^[a-z]+$"
        };

        // Assert
        schema.MinLength.Should().Be(5);
        schema.MaxLength.Should().Be(100);
        schema.Pattern.Should().Be("^[a-z]+$");
    }

    /// <summary>
    /// Test that numeric constraints can be set
    /// </summary>
    [Test]
    public void NumericConstraints_SetValues_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Float,             // Act
            Minimum = 0.0f,
            Maximum = 100.0f,
            ExclusiveMinimum = -1.0f,
            ExclusiveMaximum = 101.0f
        };

        // Assert
        schema.Minimum.Should().Be(0.0f);
        schema.Maximum.Should().Be(100.0f);
        schema.ExclusiveMinimum.Should().Be(-1.0f);
        schema.ExclusiveMaximum.Should().Be(101.0f);
    }

    /// <summary>
    /// Test that array constraints can be set
    /// </summary>
    [Test]
    public void ArrayConstraints_SetValues_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Array,             // Act
            MinItems = 1,
            MaxItems = 10,
            UniqueItems = true
        };

        // Assert
        schema.MinItems.Should().Be(1);
        schema.MaxItems.Should().Be(10);
        schema.UniqueItems.Should().BeTrue();
    }

    /// <summary>
    /// Test that Const value can be set
    /// </summary>
    [Test]
    public void Const_SetValue_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new()
        {
            // Act
            Const = "constantValue"
        };

        // Assert
        schema.Const.Should().Be("constantValue");
    }

    /// <summary>
    /// Test that Format can be set for string validation
    /// </summary>
    [Test]
    public void Format_SetValue_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.String,             // Act
            Format = "email"
        };

        // Assert
        schema.Format.Should().Be("email");
    }

    /// <summary>
    /// Test that complex nested schema can be created
    /// </summary>
    [Test]
    public void NestedSchema_ComplexStructure_ShouldWork()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["name"] = new JsonSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 50 },
                ["age"] = new JsonSchema { Type = JsonSchemaType.Integer, Minimum = 0, Maximum = 150 },
                ["tags"] = new JsonSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new JsonSchema { Type = JsonSchemaType.String }
                }
            },
            Required = ["name", "age"]
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(3);
        schema.Required.Should().HaveCount(2);

        schema.Properties["name"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["name"].MinLength.Should().Be(1);
        schema.Properties["name"].MaxLength.Should().Be(50);

        schema.Properties["age"].Type.Should().Be(JsonSchemaType.Integer);
        schema.Properties["age"].Minimum.Should().Be(0);
        schema.Properties["age"].Maximum.Should().Be(150);

        schema.Properties["tags"].Type.Should().Be(JsonSchemaType.Array);
        schema.Properties["tags"].Items.Should().NotBeNull();
        schema.Properties["tags"].Items!.Type.Should().Be(JsonSchemaType.String);
    }

    /// <summary>
    /// Test that JsonSchema None type is default
    /// </summary>
    [Test]
    public void Type_Default_ShouldBeNone()
    {
        // Arrange & Act
        JsonSchema schema = new();

        // Assert
        schema.Type.Should().Be(JsonSchemaType.None);
    }

    /// <summary>
    /// Test that multiple enum values of different types can be added
    /// </summary>
    [Test]
    public void Enum_MixedTypes_ShouldStore()
    {
        // Arrange
        JsonSchema schema = new();

        // Act
        schema.Enum.Add("string");
        schema.Enum.Add(42);
        schema.Enum.Add(true);

        // Assert
        schema.Enum.Should().HaveCount(3);
        schema.Enum[0].Should().Be("string");
        schema.Enum[1].Should().Be(42);
        schema.Enum[2].Should().Be(true);
    }

    /// <summary>
    /// Test that schema with no constraints has null constraint values
    /// </summary>
    [Test]
    public void Constraints_Default_ShouldBeNull()
    {
        // Arrange & Act
        JsonSchema schema = new();

        // Assert
        schema.MinLength.Should().BeNull();
        schema.MaxLength.Should().BeNull();
        schema.Pattern.Should().BeNull();
        schema.Minimum.Should().BeNull();
        schema.Maximum.Should().BeNull();
        schema.ExclusiveMinimum.Should().BeNull();
        schema.ExclusiveMaximum.Should().BeNull();
        schema.MinItems.Should().BeNull();
        schema.MaxItems.Should().BeNull();
        schema.UniqueItems.Should().BeNull();
        schema.Format.Should().BeNull();
        schema.Const.Should().BeNull();
    }

    /// <summary>
    /// Test that Items can be null for non-array schemas
    /// </summary>
    [Test]
    public void Items_NonArraySchema_CanBeNull()
    {
        // Arrange
        JsonSchema schema = new() { Type = JsonSchemaType.String };

        // Assert
        schema.Items.Should().BeNull();
    }

    /// <summary>
    /// Test that Properties can be replaced entirely
    /// </summary>
    [Test]
    public void Properties_Replace_ShouldWork()
    {
        // Arrange
        JsonSchema schema = new();
        schema.Properties["old"] = new JsonSchema();

        Dictionary<string, JsonSchema> newProperties = new()
        {
            ["new"] = new JsonSchema { Type = JsonSchemaType.String }
        };

        // Act
        schema.Properties = newProperties;

        // Assert
        schema.Properties.Should().NotContainKey("old");
        schema.Properties.Should().ContainKey("new");
    }
}