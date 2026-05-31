using FluentAssertions;
using Jennifer.Wpf.Automation;
using NUnit.Framework;
using System.IO;
using System.Text.Json;

namespace Jennifer.Wpf.Tests.Automation;

/// <summary>
/// Tests Jennifer automation plan loading and normalization.
/// </summary>
public class JenniferAutomationPlanLoaderTests
{
    [Test]
    /// <summary>
    /// Verifies that legacy aliases and unsupported priorities are normalized during plan loading.
    /// </summary>
    /// <post>The loader returns a trimmed plan with valid action names and supported priorities.</post>
    public void LoadFromJson_ShouldNormalizeAliasesAndPriority()
    {
        const string json = """
        {
          "name": "  Smoke Test  ",
          "description": "  Basic automation  ",
          "endpoint": " ws://localhost:8000 ",
          "game": " NeuroGame ",
          "autoRespond": true,
          "steps": [
            {
              "name": "  Inspect  ",
              "action": " inspect ",
              "priority": "urgent",
              "query": " scan room ",
              "resultMessage": " done "
            }
          ]
        }
        """;

        JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromJson(json);

        plan.Name.Should().Be("Smoke Test");
        plan.Description.Should().Be("Basic automation");
        plan.Endpoint.Should().Be("ws://localhost:8000");
        plan.GameName.Should().Be("NeuroGame");
        plan.AutoRespond.Should().BeTrue();
        plan.Steps.Should().ContainSingle();
        plan.Steps[0].Name.Should().Be("Inspect");
        plan.Steps[0].ActionName.Should().Be("inspect");
        plan.Steps[0].Priority.Should().Be("low");
        plan.Steps[0].Query.Should().Be("scan room");
        plan.Steps[0].ResultMessage.Should().Be("done");
    }

    [Test]
    /// <summary>
    /// Verifies that invalid steps without an action name are skipped.
    /// </summary>
    /// <post>The loaded plan contains only executable Jennifer automation steps.</post>
    public void LoadFromJson_ShouldSkipStepsWithoutActionNames()
    {
        const string json = """
        {
          "steps": [
            { "name": "Missing action" },
            { "actionName": "inspect" }
          ]
        }
        """;

        JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromJson(json);

        plan.Steps.Should().ContainSingle();
        plan.Steps[0].ActionName.Should().Be("inspect");
        plan.Steps[0].Name.Should().Be("inspect");
    }

      [Test]
      public void LoadFromFile_ShouldLoadPlanFromDisk()
      {
        string tempFile = Path.GetTempFileName();

        try
        {
          File.WriteAllText(tempFile, "{ \"name\": \"Disk Plan\", \"steps\": [{ \"actionName\": \"inspect\" }] }");

          JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromFile(tempFile);

          plan.Name.Should().Be("Disk Plan");
          plan.Steps.Should().ContainSingle();
        }
        finally
        {
          File.Delete(tempFile);
        }
      }

      [Test]
      public void LoadFromJson_ShouldThrowForEmptyJson()
      {
        System.Action action = () => JenniferAutomationPlanLoader.LoadFromJson("   ");

        action.Should().Throw<ArgumentException>();
      }

      [Test]
      public void LoadFromJson_ShouldThrowForInvalidJson()
      {
        System.Action action = () => JenniferAutomationPlanLoader.LoadFromJson("{ invalid json }");

        action.Should().Throw<JsonException>();
      }

      [Test]
      public void LoadFromJson_ShouldUseDefaultPlanNameAndEmptyStepsWhenMissing()
      {
        JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromJson("{ \"description\": \"  \" }");

        plan.Name.Should().Be("Untitled Jennifer plan");
        plan.Description.Should().BeEmpty();
        plan.Steps.Should().BeEmpty();
      }

      [Test]
      public void LoadFromJson_ShouldAcceptCommentsAndTrailingCommas()
      {
        const string json = """
        {
          // comment supported by loader
          "name": "Commented",
          "steps": [
          { "actionName": "inspect", },
          ],
        }
        """;

        JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromJson(json);

        plan.Name.Should().Be("Commented");
        plan.Steps.Should().ContainSingle();
      }
}