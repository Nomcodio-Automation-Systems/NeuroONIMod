using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;

namespace NeuroMod.Tests.Randy;

/// <summary>
/// Smoke tests to ensure the Randy test configuration/contract remains stable.
/// These tests avoid Unity runtime and simply validate the presence and key values
/// within the `randy_test_config.yaml` used by integration tests.
/// </summary>
public class RandyContractTests
{
    private string? FindRandyConfig()
    {
        // Start from the test assembly directory and walk up to locate the config file
        string dir = TestContext.CurrentContext.TestDirectory;
        DirectoryInfo? cur = new DirectoryInfo(dir);

        while (cur != null)
        {
            string candidate = Path.Combine(cur.FullName, "randy_test_config.yaml");
            if (File.Exists(candidate)) return candidate;

            // Some repository layouts place the file under a NeuroMod subfolder
            candidate = Path.Combine(cur.FullName, "NeuroMod", "randy_test_config.yaml");
            if (File.Exists(candidate)) return candidate;

            cur = cur.Parent;
        }

        return null;
    }

    [Test]
    public void RandyConfig_FileExists()
    {
        string? path = FindRandyConfig();
        path.Should().NotBeNull("randy_test_config.yaml should exist in the repository for Randy tests");
        File.Exists(path!).Should().BeTrue();
    }

    [Test]
    public void RandyConfig_ContainsExpectedKeys()
    {
        string? path = FindRandyConfig();
        path.Should().NotBeNull();

        string content = File.ReadAllText(path!);

        content.Should().Contain("endpoint_url", "the Neuro endpoint must be present in the contract");
        content.Should().Contain("default_name:", "the duplicant default name must be present");
        content.Should().Contain("test_scenarios", "the list of test scenarios should be present");
        content.Should().Contain("command_prefix", "chat/command prefix should be present for test commands");
    }

    [Test]
    public void RandyConfig_HasRandyDefaults()
    {
        string? path = FindRandyConfig();
        path.Should().NotBeNull();

        string content = File.ReadAllText(path!);

        content.Should().Contain("default_name: \"Randy\"", "Randy should be the configured test duplicant name");
        content.Should().Contain("endpoint_url: \"ws://localhost:8080/ws\"", "The test endpoint should be the expected local ws url");
        content.Should().Contain("command_prefix: \"!randy\"", "The test command prefix for Randy must be '!randy'");
    }
}
