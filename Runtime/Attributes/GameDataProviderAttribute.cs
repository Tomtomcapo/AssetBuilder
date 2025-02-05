using System;

namespace AssetBuilder
{
    /// <summary>
    /// Marks a static class as a provider of game data arrays.
    /// This class will be scanned for arrays of types marked with [GameDataArray].
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class GameDataProviderAttribute : Attribute
    {
        public GameDataProviderAttribute()
        {
        }
    }
}