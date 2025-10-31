using System.Collections.Generic;

namespace NeuroMod.Tests.Mocks;

/// <summary>
/// Mock implementations for Unity and ONI game objects to support unit testing
/// </summary>

// Mock MinionIdentity for testing
public class MockMinionIdentity(string name = "TestDupe")
{
    public string Name { get; set; } = name;
    public MockAttributes Attributes { get; set; } = new MockAttributes();
    public MockSicknesses Sicknesses { get; set; } = new MockSicknesses();
    public MockEffects Effects { get; set; } = new MockEffects();

    public string GetProperName()
    {
        return Name;
    }

    public MockAttributes GetAttributes()
    {
        return Attributes;
    }

    public MockSicknesses GetSicknesses()
    {
        return Sicknesses;
    }

    public T? GetComponent<T>() where T : class
    {
        return typeof(T) == typeof(MockEffects) ? Effects as T : null;
    }
}

// Mock Attributes system
public class MockAttributes
{
    private readonly Dictionary<string, MockAttributeInstance> _attributes;

    public MockAttributes()
    {
        _attributes = new Dictionary<string, MockAttributeInstance>
        {
            ["Health"] = new MockAttributeInstance(100f, 100f),
            ["Calories"] = new MockAttributeInstance(4000f, 4000f),
            ["QualityOfLife"] = new MockAttributeInstance(0f, 100f),
            ["Oxygen"] = new MockAttributeInstance(100f, 100f)
        };
    }

    public MockAttributeInstance? Get(string attributeName)
    {
        return _attributes.ContainsKey(attributeName) ? _attributes[attributeName] : null;
    }

    public void SetAttributeValue(string attributeName, float current, float max)
    {
        if (_attributes.ContainsKey(attributeName))
        {
            _attributes[attributeName].SetValues(current, max);
        }
        else
        {
            _attributes[attributeName] = new MockAttributeInstance(current, max);
        }
    }
}

// Mock AttributeInstance
public class MockAttributeInstance(float currentValue, float maxValue)
{
    private float _currentValue = currentValue;
    private float _maxValue = maxValue;

    public float GetTotalValue()
    {
        return _currentValue;
    }

    public float GetMaxValue()
    {
        return _maxValue;
    }

    public void SetValues(float current, float max)
    {
        _currentValue = current;
        _maxValue = max;
    }
}

// Mock Sicknesses
public class MockSicknesses
{
    private readonly List<MockSickness> _sicknesses;

    public MockSicknesses()
    {
        _sicknesses = [];
    }

    public int Count => _sicknesses.Count;

    public void AddSickness(MockSickness sickness)
    {
        _sicknesses.Add(sickness);
    }

    public void ClearSicknesses()
    {
        _sicknesses.Clear();
    }

    public bool HasSickness(string sicknessType)
    {
        return _sicknesses.Exists(s => s.Type == sicknessType);
    }
}

// Mock Sickness
public class MockSickness(string type, float severity = 1.0f)
{
    public string Type { get; set; } = type;
    public float Severity { get; set; } = severity;
}

// Mock Effects
public class MockEffects
{
    private readonly List<MockEffect> _effects;

    public MockEffects()
    {
        _effects = [];
    }

    public void AddEffect(MockEffect effect)
    {
        _effects.Add(effect);
    }

    public void RemoveEffect(string effectType)
    {
        _effects.RemoveAll(e => e.Type == effectType);
    }

    public bool HasEffect(string effectType)
    {
        return _effects.Exists(e => e.Type == effectType);
    }

    public MockEffect? GetEffect(string effectType)
    {
        return _effects.Find(e => e.Type == effectType);
    }
}

// Mock Effect
public class MockEffect(string type, float duration = -1f, bool isTemperatureRelated = false)
{
    public string Type { get; set; } = type;
    public float Duration { get; set; } = duration;
    public bool IsTemperatureRelated { get; set; } = isTemperatureRelated;
}

// Mock NeuroActionHandler for testing actions
public class MockNeuroActionHandler
{
    public List<ExecutionResult> Results { get; private set; }
    public ExecutionResult? LastResult => Results.Count > 0 ? Results[Results.Count - 1] : null;

    public MockNeuroActionHandler()
    {
        Results = [];
    }

    public void HandleResult(ExecutionResult result)
    {
        Results.Add(result);
    }

    public void Clear()
    {
        Results.Clear();
    }
}

// Mock ExecutionResult for testing
public class ExecutionResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }

    public static ExecutionResult Success(string message, object? data = null)
    {
        return new ExecutionResult
        {
            IsSuccess = true,
            Message = message,
            Data = data
        };
    }

    public static ExecutionResult Failure(string message, object? data = null)
    {
        return new ExecutionResult
        {
            IsSuccess = false,
            Message = message,
            Data = data
        };
    }
}

// Mock BioData for testing
public class MockDuplicateBioData
{
    public float HealthPercentage { get; set; } = 1.0f;
    public float CaloriePercentage { get; set; } = 1.0f;
    public float StressPercentage { get; set; } = 0.0f;
    public float OxygenPercentage { get; set; } = 1.0f;
    public bool IsSick { get; set; } = false;
    public bool IsTemperatureComfortable { get; set; } = true;
    public bool HasSkillsData { get; set; } = true;

    public MockDuplicateBioData()
    {
    }

    public MockDuplicateBioData(MockMinionIdentity? minion)
    {
        // Initialize from minion data
        if (minion?.Attributes != null)
        {
            MockAttributeInstance? healthAttr = minion.Attributes.Get("Health");
            if (healthAttr != null)
            {
                HealthPercentage = healthAttr.GetMaxValue() > 0 ? healthAttr.GetTotalValue() / healthAttr.GetMaxValue() : 0f;
            }

            MockAttributeInstance? calorieAttr = minion.Attributes.Get("Calories");
            if (calorieAttr != null)
            {
                CaloriePercentage = calorieAttr.GetMaxValue() > 0 ? calorieAttr.GetTotalValue() / calorieAttr.GetMaxValue() : 0f;
            }

            MockAttributeInstance? stressAttr = minion.Attributes.Get("QualityOfLife");
            if (stressAttr != null)
            {
                StressPercentage = stressAttr.GetMaxValue() > 0 ? stressAttr.GetTotalValue() / stressAttr.GetMaxValue() : 0f;
            }

            MockAttributeInstance? oxygenAttr = minion.Attributes.Get("Oxygen");
            if (oxygenAttr != null)
            {
                OxygenPercentage = oxygenAttr.GetMaxValue() > 0 ? oxygenAttr.GetTotalValue() / oxygenAttr.GetMaxValue() : 0f;
            }
        }

        if (minion?.Sicknesses != null)
        {
            IsSick = minion.Sicknesses.Count > 0;
        }

        if (minion?.Effects != null)
        {
            IsTemperatureComfortable = !minion.Effects.HasEffect("HotTemperature") && !minion.Effects.HasEffect("ColdTemperature");
        }
    }

    public void SetCriticalHealth()
    {
        HealthPercentage = 0.2f;
    }

    public void SetStarvation()
    {
        CaloriePercentage = 0.1f;
    }

    public void SetCriticalStress()
    {
        StressPercentage = 0.9f;
    }

    public void SetSick(bool sick = true)
    {
        IsSick = sick;
    }

    public void SetTemperatureHazard(bool hazard = true)
    {
        IsTemperatureComfortable = !hazard;
    }
}

// Mock Time for Unity Time simulation
public static class MockTime
{
    public static float Time { get; set; } = 0f;

    public static void AdvanceTime(float seconds)
    {
        Time += seconds;
    }

    public static void Reset()
    {
        Time = 0f;
    }
}

// Helper class for creating common test scenarios
public static class TestScenarios
{
    public static MockMinionIdentity CreateHealthyDupe(string name = "TestDupe")
    {
        MockMinionIdentity minion = new(name);
        minion.Attributes.SetAttributeValue("Health", 100f, 100f);
        minion.Attributes.SetAttributeValue("Calories", 4000f, 4000f);
        minion.Attributes.SetAttributeValue("QualityOfLife", 0f, 100f);
        minion.Attributes.SetAttributeValue("Oxygen", 100f, 100f);
        return minion;
    }

    public static MockMinionIdentity CreateCriticalHealthDupe(string name = "CriticalDupe")
    {
        MockMinionIdentity minion = new(name);
        minion.Attributes.SetAttributeValue("Health", 25f, 100f);
        minion.Attributes.SetAttributeValue("Calories", 4000f, 4000f);
        minion.Attributes.SetAttributeValue("QualityOfLife", 0f, 100f);
        minion.Attributes.SetAttributeValue("Oxygen", 100f, 100f);
        return minion;
    }

    public static MockMinionIdentity CreateStarvingDupe(string name = "StarvingDupe")
    {
        MockMinionIdentity minion = new(name);
        minion.Attributes.SetAttributeValue("Health", 100f, 100f);
        minion.Attributes.SetAttributeValue("Calories", 500f, 4000f);
        minion.Attributes.SetAttributeValue("QualityOfLife", 0f, 100f);
        minion.Attributes.SetAttributeValue("Oxygen", 100f, 100f);
        return minion;
    }

    public static MockMinionIdentity CreateStressedDupe(string name = "StressedDupe")
    {
        MockMinionIdentity minion = new(name);
        minion.Attributes.SetAttributeValue("Health", 100f, 100f);
        minion.Attributes.SetAttributeValue("Calories", 4000f, 4000f);
        minion.Attributes.SetAttributeValue("QualityOfLife", 85f, 100f);
        minion.Attributes.SetAttributeValue("Oxygen", 100f, 100f);
        return minion;
    }

    public static MockMinionIdentity CreateSickDupe(string name = "SickDupe")
    {
        MockMinionIdentity minion = new(name);
        minion.Attributes.SetAttributeValue("Health", 80f, 100f);
        minion.Attributes.SetAttributeValue("Calories", 3000f, 4000f);
        minion.Attributes.SetAttributeValue("QualityOfLife", 20f, 100f);
        minion.Attributes.SetAttributeValue("Oxygen", 100f, 100f);
        minion.Sicknesses.AddSickness(new MockSickness("FoodPoisoning", 0.5f));
        return minion;
    }
}