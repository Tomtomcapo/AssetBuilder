using System;

namespace Common.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class IgnoreGenerationAttribute : Attribute
    {
        // This can be empty as it's just a marker attribute
    }
}