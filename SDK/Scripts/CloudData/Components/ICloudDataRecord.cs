using MetaverseCloudEngine.Unity.Assets.LandPlots;

namespace MetaverseCloudEngine.Unity.CloudData.Components
{
    public interface ICloudDataRecord
    {
        void NotifyLandPlotDeleted(LandPlot plot);
    }
}