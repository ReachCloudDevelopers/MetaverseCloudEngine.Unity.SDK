using System;

namespace MetaverseCloudEngine.Unity.OpenCV.BYTETracker
{
    public interface IDetectionBase
    {
        IRectBase Rect { get; }
        float Score { get; }

        void SetRect(IRectBase rect);
        void SetScore(float score);
    }

    public class Detection<T> : Detection
    {
        public Detection(T reference, TlwhRect rect, float score) : base(rect, score)
        {
            Ref = reference;
        }

        public T Ref { get; private set; }

        public override void OnMatched(IDetectionBase detection)
        {
            base.OnMatched(detection);
            if (detection is Detection<T> d)
                Ref = d.Ref;
        }
    }

    public class Detection : IDetectionBase
    {
        private TlwhRect _rect;
        private float _score;


        public Detection(TlwhRect rect, float score)
        {
            _rect = rect;
            _score = score;
        }

        public IRectBase Rect => _rect;
        public float Score => _score;

        public void SetRect(IRectBase rect)
        {
            if (rect is TlwhRect tlwhRect)
            {
                _rect = new TlwhRect(tlwhRect);
            }
            else
            {
                // Handle the case when a different type of rect is passed
                throw new ArgumentException("Invalid rectangle type");
            }
        }

        public void SetScore(float score = 0)
        {
            _score = score;
        }

        public virtual void OnMatched(IDetectionBase detection)
        {
        }
        
        public override string ToString()
        {
            return "[" + Rect + ", " + Score + "]";
        }
    }
}
