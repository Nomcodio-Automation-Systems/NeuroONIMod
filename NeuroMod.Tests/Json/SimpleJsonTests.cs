using FluentAssertions;
using NeuroSdk.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod.Tests.Json;

/// <summary>
/// Comprehensive tests for JSON schema functionality
/// </summary>
[TestFixture]
public class SimpleJsonTests
{
    [Test]
    public void JsonSchema_ShouldCreateValidInstance()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = []
        };

        // Assert
        schema.Should().NotBeNull();
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().NotBeNull();
    }

    [Test]
    public void JsonSchemaType_ShouldHaveCorrectValues()
    {
        // Assert
        JsonSchemaType.Object.Should().NotBe(JsonSchemaType.String);
        JsonSchemaType.String.Should().NotBe(JsonSchemaType.Boolean);
        JsonSchemaType.Boolean.Should().NotBe(JsonSchemaType.Float);
        JsonSchemaType.Float.Should().NotBe(JsonSchemaType.Array);
        JsonSchemaType.Array.Should().NotBe(JsonSchemaType.Object);
    }

    [Test]
    public void JsonSchema_WithProperties_ShouldWorkCorrectly()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["test"] = new JsonSchema { Type = JsonSchemaType.String },
                ["value"] = new JsonSchema { Type = JsonSchemaType.Float }
            }
        };

        // Assert
        schema.Properties.Should().HaveCount(2);
        schema.Properties["test"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["value"].Type.Should().Be(JsonSchemaType.Float);
    }

    [Test]
    public void JsonSchemaType_AllValues_ShouldBeDistinct()
    {
        // Arrange
        List<JsonSchemaType> allValues = [.. Enum.GetValues(typeof(JsonSchemaType)).Cast<JsonSchemaType>()];

        // Assert
        allValues.Should().HaveCountGreaterThan(5);
        allValues.Distinct().Should().HaveCount(allValues.Count);
    }

    [Test]
    public void JsonSchema_WithEnum_ShouldSupportEnumValues()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.String,
            Enum = ["option1", "option2", "option3"]
        };

        // Assert
        schema.Enum.Should().NotBeNull();
        schema.Enum.Should().HaveCount(3);
        schema.Enum.Should().Contain("option1");
        schema.Enum.Should().Contain("option2");
        schema.Enum.Should().Contain("option3");
    }

    [Test]
    public void JsonSchema_WithRequired_ShouldSupportRequiredFields()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = ["field1", "field2"]
        };

        // Assert
        schema.Required.Should().NotBeNull();
        schema.Required.Should().HaveCount(2);
        schema.Required.Should().Contain("field1");
        schema.Required.Should().Contain("field2");
    }

    [Test]
    public void JsonSchema_WithMaxLength_ShouldSupportStringConstraints()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.String,
            MaxLength = 100
        };

        // Assert
        schema.MaxLength.Should().Be(100);
    }

    [Test]
    public void JsonSchema_NestedProperties_ShouldWorkCorrectly()
    {
        // Arrange & Act
        JsonSchema nestedSchema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["nested_field"] = new JsonSchema { Type = JsonSchemaType.Boolean }
            }
        };

        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["simple_field"] = new JsonSchema { Type = JsonSchemaType.String },
                ["complex_field"] = nestedSchema
            }
        };

        // Assert
        schema.Properties.Should().HaveCount(2);
        schema.Properties["simple_field"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["complex_field"].Type.Should().Be(JsonSchemaType.Object);
        schema.Properties["complex_field"].Properties.Should().HaveCount(1);
        schema.Properties["complex_field"].Properties["nested_field"].Type.Should().Be(JsonSchemaType.Boolean);
    }

    [Test]
    public void JsonSchema_ArrayType_ShouldSupportArrays()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Array,
            Items = new JsonSchema { Type = JsonSchemaType.String }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Array);
        schema.Items.Should().NotBeNull();
        schema.Items.Type.Should().Be(JsonSchemaType.String);
    }
}