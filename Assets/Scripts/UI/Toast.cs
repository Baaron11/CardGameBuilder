using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardGameBuilder.UI
{
    /// <summary>
    /// Toast notification type for styling
    /// </summary>
    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error,
        GameEvent
    }

    /// <summary>
    /// Individual toast message data
    /// </summary>
    public class ToastMessage
    {
        public string text;
        public ToastType type;
        public float duration;

        public ToastMessage(string text, ToastType type = ToastType.Info, float duration = 3f)
        {
            this.text = text;
            this.type = type;
            this.duration = duration;
        }
    }

    /// <summary>
    /// Toast notification UI component that displays queued messages with fade animations
    /// </summary>
    public class Toast : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject toastContainer;
        [SerializeField] private TextMeshProUGUI toastText;
        [SerializeField] private Image toastBackground;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float defaultDuration = 3f;
        [SerializeField] private float fadeInDuration = 0.3f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private int maxQueueSize = 5;

        [Header("Colors")]
        [SerializeField] private Color infoColor = new Color(0.2f, 0.6f, 0.9f, 0.9f);
        [SerializeField] private Color successColor = new Color(0.2f, 0.8f, 0.3f, 0.9f);
        [SerializeField] private Color warningColor = new Color(0.9f, 0.7f, 0.2f, 0.9f);
        [SerializeField] private Color errorColor = new Color(0.9f, 0.3f, 0.2f, 0.9f);
        [SerializeField] private Color gameEventColor = new Color(0.5f, 0.3f, 0.8f, 0.9f);

        private Queue<ToastMessage> _messageQueue = new Queue<ToastMessage>();
        private bool _isShowing = false;

        void Awake()
        {
            // Ensure container is hidden initially
            if (toastContainer != null)
                toastContainer.SetActive(false);

            if (canvasGroup == null && toastContainer != null)
                canvasGroup = toastContainer.GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// Show a toast message
        /// </summary>
        public void Show(string message, ToastType type = ToastType.Info, float duration = 0f)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            float actualDuration = duration > 0 ? duration : defaultDuration;
            ToastMessage toast = new ToastMessage(message, type, actualDuration);

            if (_messageQueue.Count >= maxQueueSize)
            {
                Debug.LogWarning($"[Toast] Queue full, dropping oldest message");
                _messageQueue.Dequeue();
            }

            _messageQueue.Enqueue(toast);

            if (!_isShowing)
            {
                StartCoroutine(ShowNextToast());
            }
        }

        /// <summary>
        /// Convenience methods for different toast types
        /// </summary>
        public void ShowInfo(string message, float duration = 0f)
        {
            Show(message, ToastType.Info, duration);
        }

        public void ShowSuccess(string message, float duration = 0f)
        {
            Show(message, ToastType.Success, duration);
        }

        public void ShowWarning(string message, float duration = 0f)
        {
            Show(message, ToastType.Warning, duration);
        }

        public void ShowError(string message, float duration = 0f)
        {
            Show(message, ToastType.Error, duration);
        }

        public void ShowGameEvent(string message, float duration = 0f)
        {
            Show(message, ToastType.GameEvent, duration);
        }

        /// <summary>
        /// Coroutine to display toasts from queue
        /// </summary>
        private IEnumerator ShowNextToast()
        {
            _isShowing = true;

            while (_messageQueue.Count > 0)
            {
                ToastMessage toast = _messageQueue.Dequeue();

                // Configure toast appearance
                if (toastText != null)
                    toastText.text = toast.text;

                if (toastBackground != null)
                    toastBackground.color = GetColorForType(toast.type);

                // Show container
                if (toastContainer != null)
                    toastContainer.SetActive(true);

                // Fade in
                yield return FadeCanvasGroup(0f, 1f, fadeInDuration);

                // Wait for display duration
                yield return new WaitForSeconds(toast.duration);

                // Fade out
                yield return FadeCanvasGroup(1f, 0f, fadeOutDuration);

                // Hide container
                if (toastContainer != null)
                    toastContainer.SetActive(false);

                // Small delay before next toast
                yield return new WaitForSeconds(0.2f);
            }

            _isShowing = false;
        }

        /// <summary>
        /// Fade animation for CanvasGroup
        /// </summary>
        private IEnumerator FadeCanvasGroup(float from, float to, float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float elapsed = 0f;
            canvasGroup.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                canvasGroup.alpha = Mathf.Lerp(from, to, t);
                yield return null;
            }

            canvasGroup.alpha = to;
        }

        /// <summary>
        /// Get color for toast type
        /// </summary>
        private Color GetColorForType(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    return successColor;
                case ToastType.Warning:
                    return warningColor;
                case ToastType.Error:
                    return errorColor;
                case ToastType.GameEvent:
                    return gameEventColor;
                case ToastType.Info:
                default:
                    return infoColor;
            }
        }

        /// <summary>
        /// Clear all queued messages
        /// </summary>
        public void ClearQueue()
        {
            _messageQueue.Clear();
            StopAllCoroutines();
            _isShowing = false;

            if (toastContainer != null)
                toastContainer.SetActive(false);

            Debug.Log("[Toast] Queue cleared");
        }

        /// <summary>
        /// Get current queue size
        /// </summary>
        public int GetQueueSize()
        {
            return _messageQueue.Count;
        }
    }

    /// <summary>
    /// Singleton wrapper for easy global access to toast notifications
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        private static ToastManager _instance;
        public static ToastManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ToastManager");
                    _instance = go.AddComponent<ToastManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Toast _activeToast;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Set the active toast UI component (call from scene setup)
        /// </summary>
        public void SetActiveToast(Toast toast)
        {
            _activeToast = toast;
            Debug.Log("[ToastManager] Active toast UI set");
        }

        /// <summary>
        /// Show a toast using the active toast component
        /// </summary>
        public void Show(string message, ToastType type = ToastType.Info, float duration = 0f)
        {
            if (_activeToast != null)
            {
                _activeToast.Show(message, type, duration);
            }
            else
            {
                Debug.LogWarning($"[ToastManager] No active toast UI. Message: {message}");
            }
        }

        public void ShowInfo(string message, float duration = 0f) => Show(message, ToastType.Info, duration);
        public void ShowSuccess(string message, float duration = 0f) => Show(message, ToastType.Success, duration);
        public void ShowWarning(string message, float duration = 0f) => Show(message, ToastType.Warning, duration);
        public void ShowError(string message, float duration = 0f) => Show(message, ToastType.Error, duration);
        public void ShowGameEvent(string message, float duration = 2f) => Show(message, ToastType.GameEvent, duration);
    }
}
