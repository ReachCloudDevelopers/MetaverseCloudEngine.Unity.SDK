using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MetaverseCloudEngine.Unity.UI.Components
{
    [RequireComponent(typeof(ScrollRect))]
    [DefaultExecutionOrder(int.MaxValue)]
    public class ScrollRectRefresh : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Serializable]
        public class RefreshControlEvent : UnityEvent
        {
        }

        [SerializeField] private float pullDistanceRequiredRefresh = 150f;
        [SerializeField] private RefreshControlEvent onRefresh = new();

        private ScrollRect _scrollRect;
        private float _initialPosition;
        private bool _isPulled;
        private Vector2 _positionStop;
        private bool _isDragging;

        public float PullValue { get; private set; }
        public bool IsRefreshing { get; private set; }
        public RefreshControlEvent OnRefresh
        {
            get => onRefresh;
            set => onRefresh = value;
        }

        private void Awake() => _scrollRect = GetComponent<ScrollRect>();
        private void OnEnable() => _scrollRect.onValueChanged.AddListener(OnScroll);
        private void OnDisable() => _scrollRect.onValueChanged.RemoveListener(OnScroll);

        private void Start()
        {
            _initialPosition = GetContentAnchoredPosition();
            _positionStop = new Vector2(
                _scrollRect.content.anchoredPosition.x,
                _initialPosition - pullDistanceRequiredRefresh);
        }

        private void LateUpdate()
        {
            if (!_isPulled)
                return;
            if (!IsRefreshing)
                return;

            _scrollRect.content.anchoredPosition = _positionStop;
        }

        public void EndRefreshing()
        {
            _isPulled = false;
            IsRefreshing = false;
        }

        private void OnScroll(Vector2 normalizedPosition)
        {
            float distance = _initialPosition - GetContentAnchoredPosition();
            if (distance < 0f)
                return;

            OnPull(distance);
        }

        private void OnPull(float distance)
        {
            if (IsRefreshing && Math.Abs(distance) < 1f)
                IsRefreshing = false;

            if (_isPulled && _isDragging)
                return;

            PullValue = distance / pullDistanceRequiredRefresh;
            if (PullValue < 1f)
                return;

            if (_isDragging)
                _isPulled = true;

            if (_isPulled && !_isDragging)
            {
                IsRefreshing = true;
                OnRefresh?.Invoke();
            }

            PullValue = 0f;
        }

        private float GetContentAnchoredPosition() => _scrollRect.content.anchoredPosition.y;

        void IDragHandler.OnDrag(PointerEventData eventData) {}
        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData) => _isDragging = true;
        void IEndDragHandler.OnEndDrag(PointerEventData eventData) => _isDragging = false;
    }
}