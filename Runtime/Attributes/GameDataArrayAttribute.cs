using System;

namespace Common.Data.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class GameDataArrayAttribute : Attribute
    {
        public string ArrayName { get; }
        
        public GameDataArrayAttribute(string arrayName)
        {
            ArrayName = arrayName;
        }
    }
}
