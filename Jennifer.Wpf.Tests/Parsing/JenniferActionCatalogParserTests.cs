using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Jennifer.Wpf.Parsing;
using NUnit.Framework;

namespace Jennifer.Wpf.Tests.Parsing;

/// <summary>
/// Tests Jennifer source-action parsing.
/// </summary>
public class JenniferActionCatalogParserTests
{
    [Test]
    /// <summary>
    /// Verifies that a source snippet yields the expected action metadata.
    /// </summary>
    /// <post>The parser extracts the action name, description, schema presence, and parameters.</post>
    public void ParseAllFromSource_ShouldExtractActionMetadata()
    {
        const string source = """
        public sealed class InspectAction
        {
            public override string Name => "inspect";
            protected override string Description => "Inspect the current target";
            protected override JsonSchema? Schema => new()
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["target"] = new JsonSchema { Type = JsonSchemaType.String }
                }
            };
        }
        """;

        var actions = JenniferActionCatalogParser.ParseAllFromSource(source);

        actions.Should().ContainSingle();
        var action = actions[0];
        action.Name.Should().Be("inspect");
        action.Description.Should().Be("Inspect the current target");
        action.HasSchema.Should().BeTrue();
        action.Parameters.Should().ContainSingle(p => p.Name == "target" && p.JsonType == "string");
    }

    [Test]
    /// <summary>
    /// Verifies that multiple actions in one file are all discovered.
    /// </summary>
    public void ParseAllFromSource_ShouldReturnAllActionsInFile()
    {
        const string source = """
        public class GetAction
        {
            public override string Name => "get_data";
            protected override string Description => "Get some data";
        }
        public class SetAction
        {
            public override string Name => "set_data";
            protected override string Description => "Set some data";
        }
        """;

        var actions = JenniferActionCatalogParser.ParseAllFromSource(source);

        actions.Should().HaveCount(2);
        actions.Should().Contain(a => a.Name == "get_data");
        actions.Should().Contain(a => a.Name == "set_data");
    }

    [Test]
    /// <summary>
    /// Verifies that directory parsing de-duplicates actions by name.
    /// </summary>
    /// <post>The parser keeps the most recent action metadata for duplicate action names.</post>
    public async Task ParseDirectoryAsync_ShouldDeduplicateByActionName()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(tempDirectory, "FirstAction.cs"),
                "public override string Name => \"inspect\";");
            await File.WriteAllTextAsync(
                Path.Combine(tempDirectory, "SecondAction.cs"),
                "public override string Name => \"inspect\"; protected override string Description => \"Updated\";");

            var actions = await JenniferActionCatalogParser.ParseDirectoryAsync(tempDirectory);

            actions.Should().ContainSingle();
            actions[0].Name.Should().Be("inspect");
            actions[0].Description.Should().Be("Updated");
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Test]
    public void ParseAllFromSource_ShouldReturnEmptyWhenNoActionNameExists()
    {
        var actions = JenniferActionCatalogParser.ParseAllFromSource("protected override string Description => \"No name\";");

        actions.Should().BeEmpty();
    }

    [Test]
    public void ParseAllFromSource_ShouldUseEmptyDescriptionAndFalseSchemaWhenMissing()
    {
        var actions = JenniferActionCatalogParser.ParseAllFromSource("public override string Name => \"inspect\";");

        actions.Should().ContainSingle();
        actions[0].Description.Should().BeEmpty();
        actions[0].HasSchema.Should().BeFalse();
    }

    [Test]
    public async Task ParseDirectoryAsync_ShouldReturnEmptyForMissingDirectory()
    {
        var actions = await JenniferActionCatalogParser.ParseDirectoryAsync(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        actions.Should().BeEmpty();
    }

    [Test]
    public void ParseAllFromSource_ShouldExtractRequiredAndEnumFields()
    {
        const string source = """
        public class SetScheduleAction
        {
            public override string Name => "set_schedule";
            protected override JsonSchema Schema => new()
            {
                Type = JsonSchemaType.Object,
                Required = new List<string>{ "schedule_type" },
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["schedule_type"] = new JsonSchema
                    {
                        Type = JsonSchemaType.String,
                        Enum = new List<object> { "default", "work_focused", "night_shift" }
                    }
                }
            };
        }
        """;

        var actions = JenniferActionCatalogParser.ParseAllFromSource(source);

        actions.Should().ContainSingle();
        var param = actions[0].Parameters.Should().ContainSingle().Subject;
        param.Name.Should().Be("schedule_type");
        param.IsRequired.Should().BeTrue();
        param.EnumValues.Should().BeEquivalentTo(new[] { "default", "work_focused", "night_shift" });
    }

    [Test]
    /// <summary>
    /// Verifies that schema-bearing actions without required fields can still be dispatched without parameters.
    /// </summary>
    /// <post>The parsed action reports optional parameters while still preserving its schema metadata.</post>
    public void ParseAllFromSource_ShouldAllowParameterlessDispatchWhenSchemaFieldsAreOptional()
    {
        const string source = """
        public class ListErrandsAction
        {
            public override string Name => "list_errands";
            protected override JsonSchema Schema => new()
            {
                Type = JsonSchemaType.Object,
                Properties = new Dictionary<string, JsonSchema>
                {
                    ["filter_type"] = new JsonSchema { Type = JsonSchemaType.String },
                    ["max_results"] = new JsonSchema { Type = JsonSchemaType.Integer }
                }
            };
        }
        """;

        var actions = JenniferActionCatalogParser.ParseAllFromSource(source);

        actions.Should().ContainSingle();
        JenniferDiscoveredAction action = actions[0];
        action.HasSchema.Should().BeTrue();
        action.Parameters.Should().HaveCount(2);
        action.HasRequiredParameters.Should().BeFalse();
        action.SupportsParameterlessDispatch.Should().BeTrue();
    }
}