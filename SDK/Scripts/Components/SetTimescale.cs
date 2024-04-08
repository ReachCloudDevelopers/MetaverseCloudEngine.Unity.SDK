using TriInspectorMVCE;
using UnityEngine;

namespace MetaverseCloudEngine.Unity.Components
{
    /// <summary>
    /// A helper component to set the timescale of the game.
    /// </summary>
    [HideMonoScript]
    public class SetTimescale : TriInspectorMonoBehaviour
    {
        [InfoBox("Note that the timescale is not synced across clients in multiplayer games.")]
        [Tooltip("The timescale to set.")]
        [SerializeField, Range(0.001f, 1f)] private float timeScale = 1f;
        [Tooltip("Whether to set the timescale on start.")]
        [SerializeField] private bool setOnStart = true;
        [SerializeField] private bool adjustFixedDeltaTime = true;

        private bool _isStarted;

        /// <summary>
        /// Gets or sets the timescale.
        /// </summary>
        public float Timescale { get => timeScale; set => timeScale = value; }

        private void Start()
        {
            _isStarted = true;
            if (setOnStart)
                Set();
        }
        
        /// <summary>
        /// Sets the timescale.
        /// </summary>
        /// <param name="timescale">The timescale to set.</param>
        public void Set(float timescale)
        {
            timeScale = timescale;
            Set();
        }

        /// <summary>
        /// Sets the timescale.
        /// </summary>
        public void Set()
        {
            if (!_isStarted && setOnStart)
                return;

            if (!isActiveAndEnabled)
                return;
            
            if (timeScale < 0.001f)
                timeScale = 0.001f;

            Time.timeScale = timeScale;
            if (adjustFixedDeltaTime)
                Time.fixedDeltaTime = MetaverseConstants.Physics.DefaultFixedDeltaTime * timeScale;
        }
    }
}
