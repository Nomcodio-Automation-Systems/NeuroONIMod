using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;

namespace NeuroMod.Tests.Utilities
{
    /// <summary>
    /// Tests for utility components that don't require Unity dependencies
    /// </summary>
    [TestFixture]
    public class UtilityTests
    {
        [Test]
        [Ignore("Requires Unity runtime - Assembly.GetTypes() tries to load Unity-dependent types")]
        public void NeuroSdkUtilities_ShouldHaveValidNamespace()
        {
            // Verify NeuroSdk.Utilities namespace exists and contains classes
            Assembly assembly = Assembly.GetAssembly(typeof(NeuroSdk.Json.JsonSchema));

            assembly.Should().NotBeNull();

            System.Collections.Generic.List<Type> value = [.. assembly.GetTypes().Where(t => t.Namespace == "NeuroSdk.Utilities")];
            System.Collections.Generic.List<Type> utilityTypes = value;

            utilityTypes.Should().NotBeEmpty("NeuroSdk.Utilities namespace should contain utility classes");
        }

        [Test]
        [Ignore("Requires Unity runtime - Assembly.GetTypes() tries to load Unity-dependent types")]
        public void NeuroSdkNamespaces_ShouldBeCorrectlyDefined()
        {
            // Verify all NeuroSdk namespaces are accessible
            Assembly assembly = Assembly.GetAssembly(typeof(NeuroSdk.Json.JsonSchema));

            assembly.Should().NotBeNull();

            System.Collections.Generic.List<Type> neuroSdkTypes = [.. assembly.GetTypes().Where(t => t.Namespace != null && t.Namespace.StartsWith("NeuroSdk"))];

            neuroSdkTypes.Should().NotBeEmpty();
            neuroSdkTypes.Should().Contain(t => t.Namespace == "NeuroSdk.Json");
            neuroSdkTypes.Should().Contain(t => t.Namespace == "NeuroSdk.Websocket");
            neuroSdkTypes.Should().Contain(t => t.Namespace == "NeuroSdk.Utilities");
        }

        [Test]
        [Ignore("Requires Unity runtime - Assembly.GetTypes() tries to load Unity-dependent types")]
        public void ONIModNamespace_ShouldBeCorrectlyDefined()
        {
            // Verify ONIMod namespace is accessible
            Assembly assembly = Assembly.GetAssembly(typeof(NeuroMod.DuplicateBioData));

            assembly.Should().NotBeNull();

            System.Collections.Generic.List<Type> oniModTypes = [.. assembly.GetTypes().Where(t => t.Namespace == "ONIMod")];

            oniModTypes.Should().NotBeEmpty();
            oniModTypes.Should().Contain(t => t.Name == "DuplicateBioData");
            oniModTypes.Should().Contain(t => t.Name == "BioDataAnalyzer");
        }

        [Test]
        public void StaticClasses_ShouldBeProperlyMarked()
        {
            // Test that static classes are correctly marked as static
            Type[] staticTypes =
            [
                typeof(NeuroMod.BioDataAnalyzer)
            ];

            foreach (Type? type in staticTypes)
            {
                type.IsStatic().Should().BeTrue($"{type.Name} should be static");
                type.IsAbstract.Should().BeTrue($"{type.Name} should be abstract (static classes are abstract)");
                type.IsSealed.Should().BeTrue($"{type.Name} should be sealed (static classes are sealed)");
            }
        }

        [Test]
        [Ignore("Requires Unity runtime - Assembly.GetTypes() tries to load Unity-dependent types")]
        public void ActionClasses_ShouldHaveCorrectBaseTypes()
        {
            // Test action class inheritance without instantiating them
            Assembly assembly = Assembly.GetAssembly(typeof(NeuroMod.GetStatusAction));

            System.Collections.Generic.List<Type> actionTypes = [.. assembly.GetTypes()
                .Where(t => t.Name.EndsWith("Action") &&
                           !t.IsAbstract &&
                           (t.Namespace?.Contains("Integration") == true || t.Namespace?.Contains("Examples") == true) && // Include both Integration and Examples
                           !t.Name.Contains("OutgoingMessage") && // Exclude message classes
                           !t.Name.Contains("IncomingMessage"))];

            actionTypes.Should().NotBeEmpty();

            foreach (Type? actionType in actionTypes)
            {
                // Verify they inherit from appropriate base classes
                bool hasCorrectBase = IsNeuroActionType(actionType);

                hasCorrectBase.Should().BeTrue($"{actionType.Name} should inherit from a NeuroAction base class");
            }
        }

        private bool IsNeuroActionType(Type type)
        {
            Type currentType = type;
            while (currentType != null && currentType != typeof(object))
            {
                if (currentType.Name.Contains("NeuroAction") ||
                    currentType.Name.Contains("BaseNeuroAction"))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        [Test]
        public void PublicTypes_ShouldBeAccessible()
        {
            // Test that public types are accessible from tests
            Type[] publicTypes =
            [
                typeof(NeuroSdk.Json.JsonSchema),
                typeof(NeuroSdk.Json.JsonSchemaType),
                typeof(NeuroSdk.Websocket.ExecutionResult),
                typeof(NeuroMod.DuplicateBioData),
                typeof(NeuroMod.BioDataAnalyzer)
            ];

            foreach (Type? type in publicTypes)
            {
                type.Should().NotBeNull($"{type.Name} should be accessible");
                type.IsPublic.Should().BeTrue($"{type.Name} should be public");
            }
        }
    }
}

// Extension methods for testing static classes
public static class TypeExtensions
{
    public static bool IsStatic(this Type type)
    {
        return type.IsAbstract && type.IsSealed;
    }
}