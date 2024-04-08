namespace MetaverseCloudEngine.Unity.Components
{
    public class OpenURLOnMouseDown : OpenURL
    {
        public bool blockedByUI = true;

        private void OnMouseDown()
        {
            if (blockedByUI && MVUtils.IsPointerOverUI())
                return;

            Open();
        }
    }
}