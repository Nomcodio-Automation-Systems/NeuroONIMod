using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Jennifer.Wpf.Contracts;
using Jennifer.Wpf.Parsing;
using NeuroMod.SystemTests.Contracts;
using NUnit.Framework;

namespace NeuroMod.SystemTests.Randy;

/// <summary>
/// Validates representative Randy-compatible messages against the test-only formal contract files.
/// </summary>
public class RandyFormalContractSchemaTests
{
    [Test]
    public void StartupPayload_ShouldMatchFormalRandyTestContract()
    {
        string json = JenniferRandyContractPayloadFactory.CreateStartupPayload("ONI");
        ValidateAgainstSchema("c2s-startup.schema.json", json);
    }

    [Test]
    public void ActionsRegisterPayload_ShouldMatchFormalRandyTestContract()
    {
        string json = JenniferRandyContractPayloadFactory.CreateActionsRegisterPayload(
            "ONI",
            [new JenniferDiscoveredAction { Name = "inspect", Description = "Inspect target", HasSchema = true }]);

        ValidateAgainstSchema("c2s-actions-register.schema.json", json);
    }

    [Test]
    public void ActionsForcePayload_ShouldMatchFormalRandyTestContract()
    {
        string json = JenniferRandyContractPayloadFactory.CreateActionsForcePayload(
            "ONI",
            ["inspect", "pickup"],
            "Current state",
            "Please act",
            "high",
            true);

        ValidateAgainstSchema("c2s-actions-force.schema.json", json);
    }

    [Test]
    public void ActionResultPayload_ShouldMatchFormalRandyTestContract()
    {
        string json = JenniferRandyContractPayloadFactory.CreateActionResultPayload("abc123", true, "OK");
        ValidateAgainstSchema("c2s-action-result.schema.json", json);
    }

    [Test]
    public void RandyReregisterMessage_ShouldMatchFormalRandyTestContract()
    {
        const string json = "{\"command\":\"actions/reregister_all\"}";
        ValidateAgainstSchema("s2c-actions-reregister-all.schema.json", json);
    }

    [Test]
    public void RandyActionMessage_ShouldMatchFormalRandyTestContract()
    {
        const string json = """
        {
          "command": "action",
          "data": {
            "id": "integration-1",
            "name": "inspect",
            "data": "{\"room\":\"lab\"}"
          }
        }
        """;

        ValidateAgainstSchema("s2c-action.schema.json", json);
    }

    [Test]
    public void ContractReadme_ShouldExplicitlyStateTestOnlyScope()
    {
        string readmePath = GetContractPath("README.md");
        string readme = File.ReadAllText(readmePath);

        readme.Should().Contain("Test-only");
        readme.Should().Contain("Not a long-term compatibility promise for Neuro itself.");
    }

    private static void ValidateAgainstSchema(string schemaFileName, string json)
    {
        string schemaPath = GetContractPath(schemaFileName);
        TestJsonContractValidator.Validate(schemaPath, json);
    }

    private static string GetContractPath(string fileName)
    {
        string workspaceRoot = FindWorkspaceRoot();
        string fullPath = Path.Combine(workspaceRoot, "tests", "contracts", "randy", fileName);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Expected Randy contract file was not found at '{fullPath}'.");
        }

        return fullPath;
    }

    private static string FindWorkspaceRoot()
    {
        DirectoryInfo? current = new(TestContext.CurrentContext.TestDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Put Neuro Into a Dupe.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Workspace root containing 'Put Neuro Into a Dupe.sln' could not be found.");
    }
}