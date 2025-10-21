using System;
using System.Collections.Generic;

namespace MoonlightAI.Tests.TestData;

/// <summary>
/// A sample class for testing code analysis.
/// </summary>
public class SampleClass
{
    /// <summary>
    /// A public property with getter and setter.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A public read-only property.
    /// </summary>
    public int Count { get; private set; }

    private string _privateField = string.Empty;

    /// <summary>
    /// A public method that returns a string.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <returns>A formatted string.</returns>
    public string GetFormattedValue(string value)
    {
        return $"Value: {value}";
    }

    /// <summary>
    /// An async public method.
    /// </summary>
    /// <param name="delay">Delay in milliseconds.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task<bool> ProcessAsync(int delay = 100)
    {
        await Task.Delay(delay);
        return true;
    }

    private void PrivateMethod()
    {
        // Private method should not be in public analysis
    }

    /// <summary>
    /// A protected method.
    /// </summary>
    protected virtual void ProtectedMethod()
    {
    }
}

/// <summary>
/// An internal class that should not appear in public-only results.
/// </summary>
internal class InternalClass
{
    public string InternalProperty { get; set; } = string.Empty;
}

/// <summary>
/// A public static class.
/// </summary>
public static class StaticHelper
{
    /// <summary>
    /// A static helper method.
    /// </summary>
    public static string GetHelp() => "Help text";
}
