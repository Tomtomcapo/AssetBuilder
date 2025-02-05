namespace Common.Data.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class GenerateAssetAttribute : System.Attribute
    {
        public string AssetName { get; }
        
        public GenerateAssetAttribute(string assetName = null)
        {
            AssetName = assetName;
        }
    }
}