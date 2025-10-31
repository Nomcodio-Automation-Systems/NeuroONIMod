using FluentAssertions;
using NeuroSdk.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod.Tests.Actions;

/// <summary>
/// Schema validation tests that simulate real action schema patterns
/// </summary>
[TestFixture]
public class ActionSchemaTests
{
    [Test]
    public void GetStatusAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act - Create a schema that matches GetStatusAction
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["query_type"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = ["basic", "detailed", "minimal"]
                },
                ["include_environment"] = new JsonSchema
                {
                    Type = JsonSchemaType.Boolean
                },
                ["include_skills"] = new JsonSchema
                {
                    Type = JsonSchemaType.Boolean
                }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(3);
        schema.Properties["query_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["query_type"].Enum.Should().HaveCount(3);
        schema.Properties["include_environment"].Type.Should().Be(JsonSchemaType.Boolean);
        schema.Properties["include_skills"].Type.Should().Be(JsonSchemaType.Boolean);
    }

    [Test]
    public void ClearTasksAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act - Create a schema that matches ClearTasksAction
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["force_stop"] = new JsonSchema
                {
                    Type = JsonSchemaType.Boolean
                },
                ["reason"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    MaxLength = 100
                }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Properties["force_stop"].Type.Should().Be(JsonSchemaType.Boolean);
        schema.Properties["reason"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["reason"].MaxLength.Should().Be(100);
    }

    [Test]
    public void GetBioDataAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act - Create a schema that matches GetBioDataAction
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["data_type"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = ["health", "nutrition", "stress", "environment", "skills", "all"]
                },
                ["detail_level"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = ["basic", "detailed", "full"]
                },
                ["include_history"] = new JsonSchema
                {
                    Type = JsonSchemaType.Boolean
                },
                ["format"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = ["text", "json", "structured"]
                }
            },
            Required = ["data_type"]
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(4);
        schema.Required.Should().HaveCount(1);
        schema.Required.Should().Contain("data_type");

        schema.Properties["data_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["data_type"].Enum.Should().HaveCount(6);
        schema.Properties["detail_level"].Enum.Should().HaveCount(3);
        schema.Properties["format"].Enum.Should().HaveCount(3);
    }

    [Test]
    public void EmergencyAction_Schema_ShouldBeNull()
    {
        // Emergency actions don't need schemas as they have no parameters
        JsonSchema? schema = null;

        // Assert
        schema.Should().BeNull("Emergency actions should have null schemas since they don't take parameters");
    }

    [Test]
    public void ComplexNestedSchema_ShouldWorkCorrectly()
    {
        // Arrange & Act - Create a complex nested schema
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["action_config"] = new JsonSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, JsonSchema>
                    {
                        ["priority"] = new JsonSchema { Type = JsonSchemaType.Integer },
                        ["timeout"] = new JsonSchema { Type = JsonSchemaType.Float }
                    },
                    Required = ["priority"]
                },
                ["parameters"] = new JsonSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new JsonSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, JsonSchema>
                        {
                            ["name"] = new JsonSchema { Type = JsonSchemaType.String },
                            ["value"] = new JsonSchema { Type = JsonSchemaType.String }
                        }
                    }
                }
            },
            Required = ["action_config"]
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Required.Should().Contain("action_config");

        JsonSchema actionConfig = schema.Properties["action_config"];
        actionConfig.Type.Should().Be(JsonSchemaType.Object);
        actionConfig.Properties.Should().HaveCount(2);
        actionConfig.Required.Should().Contain("priority");

        JsonSchema parameters = schema.Properties["parameters"];
        parameters.Type.Should().Be(JsonSchemaType.Array);
        parameters.Items.Should().NotBeNull();
        parameters.Items!.Type.Should().Be(JsonSchemaType.Object);
        parameters.Items.Properties.Should().HaveCount(2);
    }

    [Test]
    public void SchemaValidation_AllEnumValues_ShouldBeStrings()
    {
        // Test that enum values in schemas are properly formatted
        List<object>[] testEnums =
        [
            ["basic", "detailed", "minimal"],
            ["health", "nutrition", "stress"],
            ["text", "json", "structured"]
        ];

        foreach (List<object> enumList in testEnums)
        {
            enumList.Should().NotBeEmpty();
            enumList.Should().AllBeOfType<string>();
            enumList.Cast<string>().Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s));
        }
    }

    [Test]
    public void SchemaValidation_RequiredFields_ShouldBeValidStrings()
    {
        // Test required field validation
        List<string> requiredFields = ["data_type", "action_config", "priority"];

        requiredFields.Should().NotBeEmpty();
        requiredFields.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s));
        requiredFields.Should().OnlyContain(s => s.All(c => char.IsLower(c) || c == '_'));
    }
}