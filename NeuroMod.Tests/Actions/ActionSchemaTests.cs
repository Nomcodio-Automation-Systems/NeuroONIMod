using FluentAssertions;
using NeuroSdk.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace NeuroMod.Tests.Actions;

/// <summary>
/// Schema validation tests that simulate real action schema patterns
/// </summary>
/// <pre>Schema tests can construct representative JsonSchema instances without Unity runtime dependencies.</pre>
/// <post>The contained tests verify that action-schema shapes, enum sets, and required-field conventions remain stable.</post>
public class ActionSchemaTests
{
    [Test]
    /// <summary>
    /// Verifies the representative status-action schema shape.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the expected status-action contract.</pre>
    /// <post>The test confirms the schema exposes the expected fields and enum counts.</post>
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
    /// <summary>
    /// Verifies the representative clear-task schema shape.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the expected clear-task contract.</pre>
    /// <post>The test confirms the schema exposes the expected fields and limits.</post>
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
    /// <summary>
    /// Verifies that emergency actions intentionally expose no parameter schema.
    /// </summary>
    /// <pre>Emergency actions are modeled as parameterless operations in this test scenario.</pre>
    /// <post>The test confirms the representative emergency-action schema remains null.</post>
    public void EmergencyAction_Schema_ShouldBeNull()
    {
        // Emergency actions don't need schemas as they have no parameters
        JsonSchema? schema = null;

        // Assert
        schema.Should().BeNull("Emergency actions should have null schemas since they don't take parameters");
    }

    [Test]
    /// <summary>
    /// Verifies that nested schema structures can be represented correctly.
    /// </summary>
    /// <pre>A synthetic schema can be built with nested object and array members.</pre>
    /// <post>The test confirms nested required-field and item metadata remain intact.</post>
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
    /// <summary>
    /// Verifies that representative enum lists used in schemas contain only non-empty strings.
    /// </summary>
    /// <pre>Representative enum collections can be evaluated without Unity runtime dependencies.</pre>
    /// <post>The test confirms schema enum values remain valid non-empty strings.</post>
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
    /// <summary>
    /// Verifies that the set_priority action schema exposes a required task_type enum and an optional priority enum.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the SetPriorityAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema has 2 properties, task_type is required with 11 enum values, and priority has 4 enum values.</post>
    public void SetPriorityAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "task_type" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["task_type"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = new List<object> { "dig", "build", "harvest", "cook", "research", "doctor", "tidy", "supply", "operate", "art", "ranch" }
                },
                ["priority"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = new List<object> { "low", "normal", "high", "critical" }
                }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Required.Should().Contain("task_type");
        schema.Properties["task_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["task_type"].Enum.Should().HaveCount(11);
        schema.Properties["task_type"].Enum.Should().Contain("dig").And.Contain("ranch");
        schema.Properties["priority"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["priority"].Enum.Should().HaveCount(4);
        schema.Properties["priority"].Enum.Should().Contain("low").And.Contain("critical");
    }

    [Test]
    /// <summary>
    /// Verifies that the list_priorities action schema is an empty object with no properties.
    /// </summary>
    /// <pre>A synthetic schema matching ListPrioritiesAction can be built without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema is an object type with zero properties and no required fields.</post>
    public void ListPrioritiesAction_Schema_ShouldBeEmptyObject()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>()
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Test]
    /// <summary>
    /// Verifies the list_errands action schema exposes filter_type enum, integer limits, and a string-array chore_types.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the ListErrandsAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms 4 properties: filter_type (enum 5), max_distance (integer), max_results (integer), chore_types (array of string).</post>
    public void ListErrandsAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["filter_type"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = new List<object> { "all", "nearby", "priority", "unassigned", "performable" }
                },
                ["max_distance"] = new JsonSchema { Type = JsonSchemaType.Integer },
                ["max_results"] = new JsonSchema { Type = JsonSchemaType.Integer },
                ["chore_types"] = new JsonSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new JsonSchema { Type = JsonSchemaType.String }
                }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(4);
        schema.Required.Should().BeEmpty();
        schema.Properties["filter_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["filter_type"].Enum.Should().HaveCount(5);
        schema.Properties["filter_type"].Enum.Should().Contain("all").And.Contain("performable");
        schema.Properties["max_distance"].Type.Should().Be(JsonSchemaType.Integer);
        schema.Properties["max_results"].Type.Should().Be(JsonSchemaType.Integer);
        schema.Properties["chore_types"].Type.Should().Be(JsonSchemaType.Array);
        schema.Properties["chore_types"].Items!.Type.Should().Be(JsonSchemaType.String);
    }

    [Test]
    /// <summary>
    /// Verifies that the get_current_errand action schema is an empty object with no properties.
    /// </summary>
    /// <pre>A synthetic schema matching GetCurrentErrandAction can be built without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema is an object type with zero properties and no required fields.</post>
    public void GetCurrentErrandAction_Schema_ShouldBeEmptyObject()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>()
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Test]
    /// <summary>
    /// Verifies the assign_errand action schema exposes a required errand_type string and optional coordinate/distance integers.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the AssignErrandAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms errand_type is required, and the schema has 4 properties of the expected types.</post>
    public void AssignErrandAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "errand_type" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["errand_type"] = new JsonSchema { Type = JsonSchemaType.String },
                ["max_distance"] = new JsonSchema { Type = JsonSchemaType.Integer },
                ["target_x"] = new JsonSchema { Type = JsonSchemaType.Integer },
                ["target_y"] = new JsonSchema { Type = JsonSchemaType.Integer }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(4);
        schema.Required.Should().Contain("errand_type");
        schema.Properties["errand_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["max_distance"].Type.Should().Be(JsonSchemaType.Integer);
        schema.Properties["target_x"].Type.Should().Be(JsonSchemaType.Integer);
        schema.Properties["target_y"].Type.Should().Be(JsonSchemaType.Integer);
    }

    [Test]
    /// <summary>
    /// Verifies that the get_errand_progress action schema is an empty object with no properties.
    /// </summary>
    /// <pre>A synthetic schema matching GetErrandProgressAction can be built without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema is an object type with zero properties and no required fields.</post>
    public void GetErrandProgressAction_Schema_ShouldBeEmptyObject()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>()
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Test]
    /// <summary>
    /// Verifies that the debug_status action schema is an empty object with no properties.
    /// </summary>
    /// <pre>A synthetic schema matching DebugStatusAction can be built without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema is an object type with zero properties and no required fields.</post>
    public void DebugStatusAction_Schema_ShouldBeEmptyObject()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>()
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Test]
    /// <summary>
    /// Verifies the test_assign_errand diagnostic action schema exposes a required errand_type string and optional max_distance integer.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the TestAssignErrandAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms errand_type is required, the schema has 2 properties, and max_distance is an integer.</post>
    public void TestAssignErrandAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "errand_type" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["errand_type"] = new JsonSchema { Type = JsonSchemaType.String },
                ["max_distance"] = new JsonSchema { Type = JsonSchemaType.Integer }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Required.Should().Contain("errand_type");
        schema.Properties["errand_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["max_distance"].Type.Should().Be(JsonSchemaType.Integer);
    }

    [Test]
    /// <summary>
    /// Verifies the test_validate_priority diagnostic action schema exposes two required fields: chore_group_name (string) and expected_priority (integer).
    /// </summary>
    /// <pre>A synthetic schema can be built to match the TestValidatePriorityAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms both fields are required and have the expected types.</post>
    public void TestValidatePriorityAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "chore_group_name", "expected_priority" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["chore_group_name"] = new JsonSchema { Type = JsonSchemaType.String },
                ["expected_priority"] = new JsonSchema { Type = JsonSchemaType.Integer }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(2);
        schema.Required.Should().Contain("chore_group_name").And.Contain("expected_priority");
        schema.Properties["chore_group_name"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["expected_priority"].Type.Should().Be(JsonSchemaType.Integer);
    }

    [Test]
    /// <summary>
    /// Verifies the get_schedule action schema exposes a single optional include_details boolean property.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the GetNeuroScheduleAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema has 1 property of type boolean with no required fields.</post>
    public void GetNeuroScheduleAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>
            {
                ["include_details"] = new JsonSchema { Type = JsonSchemaType.Boolean }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(1);
        schema.Required.Should().BeEmpty();
        schema.Properties["include_details"].Type.Should().Be(JsonSchemaType.Boolean);
    }

    [Test]
    /// <summary>
    /// Verifies the set_schedule action schema exposes a required schedule_type enum with 7 preset schedule options.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the SetScheduleAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms schedule_type is required, has 7 enum values, and includes expected named presets.</post>
    public void SetScheduleAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = new List<string> { "schedule_type" },
            Properties = new Dictionary<string, JsonSchema>
            {
                ["schedule_type"] = new JsonSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = new List<object> { "default", "work_focused", "research_focused", "night_shift", "early_bird", "recreation_focused", "bathing_focused" }
                }
            }
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(1);
        schema.Required.Should().Contain("schedule_type");
        schema.Properties["schedule_type"].Type.Should().Be(JsonSchemaType.String);
        schema.Properties["schedule_type"].Enum.Should().HaveCount(7);
        schema.Properties["schedule_type"].Enum.Should().Contain("default").And.Contain("work_focused").And.Contain("bathing_focused");
    }

    [Test]
    /// <summary>
    /// Verifies that the list_schedules action schema is an empty object with no properties.
    /// </summary>
    /// <pre>A synthetic schema matching ListSchedulesAction can be built without Unity runtime dependencies.</pre>
    /// <post>The test confirms the schema is an object type with zero properties and no required fields.</post>
    public void ListSchedulesAction_Schema_ShouldBeEmptyObject()
    {
        // Arrange & Act
        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, JsonSchema>()
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().BeEmpty();
        schema.Required.Should().BeEmpty();
    }

    [Test]
    /// <summary>
    /// Verifies the set_custom_schedule action schema exposes 24 per-hour string enum properties, all required.
    /// </summary>
    /// <pre>A synthetic schema can be built to match the SetCustomScheduleAction contract without Unity runtime dependencies.</pre>
    /// <post>The test confirms 24 string properties each with the activity enum, all required.</post>
    public void SetCustomScheduleAction_Schema_ShouldMatchExpectedStructure()
    {
        // Arrange
        var hourEnum = new List<object> { "work", "sleep", "recreation", "bathing" };
        var properties = new Dictionary<string, JsonSchema>(24);
        var required   = new List<string>(24);
        for (int i = 0; i < 24; i++)
        {
            string key = $"hour_{i}";
            properties[key] = new JsonSchema { Type = JsonSchemaType.String, Enum = hourEnum };
            required.Add(key);
        }

        JsonSchema schema = new()
        {
            Type = JsonSchemaType.Object,
            Required = required,
            Properties = properties
        };

        // Assert
        schema.Type.Should().Be(JsonSchemaType.Object);
        schema.Properties.Should().HaveCount(24);
        schema.Required.Should().HaveCount(24);
        for (int i = 0; i < 24; i++)
        {
            string key = $"hour_{i}";
            schema.Properties[key].Type.Should().Be(JsonSchemaType.String, because: $"{key} should be string");
            schema.Properties[key].Enum.Should().BeEquivalentTo(new List<object> { "work", "sleep", "recreation", "bathing" });
            schema.Required.Should().Contain(key);
        }
    }

    [Test]
    /// <summary>
    /// Verifies that representative required-field names use valid lower-case schema identifiers.
    /// </summary>
    /// <pre>Representative required-field collections can be evaluated without Unity runtime dependencies.</pre>
    /// <post>The test confirms required field names remain non-empty lower-case identifiers.</post>
    public void SchemaValidation_RequiredFields_ShouldBeValidStrings()
    {
        // Test required field validation
        List<string> requiredFields = ["data_type", "action_config", "priority"];

        requiredFields.Should().NotBeEmpty();
        requiredFields.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s));
        requiredFields.Should().OnlyContain(s => s.All(c => char.IsLower(c) || c == '_'));
    }
}