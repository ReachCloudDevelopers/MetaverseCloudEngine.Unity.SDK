using System;

namespace MetaverseCloudEngine.Unity.Assets
{
    // This is REALLY weird. Need to do something with this.
    // The reason it's here is because of some editor scripts
    // so we can grab the ID as a reference rather then just
    // a straight up GUID since it might change.
    // see AssetContributorEditor
    public interface IAssetReference
    {
        Guid? ID { get; }
    }
}
