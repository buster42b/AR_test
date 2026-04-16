using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ARWorldLabel : MonoBehaviour
{
    [Header("Label Prefab")]
    [SerializeField] private AssetReferenceGameObject labelPrefabReference;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.25f, 0f);

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas canvas;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenOffScreen = true;
    [SerializeField] private bool useOcclusionRaycast = false;
    [SerializeField] private LayerMask occlusionMask = ~0;
    [SerializeField] private float raycastPadding = 0.02f;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField] private bool disableWhenInvisible = true;

    private bool _isVisible;
    private GameObject _labelInstance;
    private RectTransform _labelRect;
    private CanvasGroup _canvasGroup;
    private AsyncOperationHandle<GameObject> _labelLoadHandle;
    [SerializeField]private string _pendingText;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindAnyObjectByType<Camera>();
            }
        }
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }
        
        LoadLabelPrefab();
    }

    private void LateUpdate()
    {
        if (_labelInstance is null || targetCamera is null || canvas is null || _labelRect is null || _canvasGroup is null)
            return;

        Vector3 worldPos = transform.position + worldOffset;
        Vector3 screenPos = targetCamera.WorldToScreenPoint(worldPos);

        bool visible = screenPos.z > 0f;

        if (visible && hideWhenOffScreen)
        {
            visible =
                screenPos.x >= 0f && screenPos.x <= Screen.width &&
                screenPos.y >= 0f && screenPos.y <= Screen.height;
        }

        if (visible && useOcclusionRaycast)
        {
            Vector3 dir = worldPos - targetCamera.transform.position;
            float dist = dir.magnitude;

            if (Physics.Raycast(
                    targetCamera.transform.position,
                    dir.normalized,
                    out RaycastHit hit,
                    dist - raycastPadding,
                    occlusionMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                    visible = false;
            }
        }

        _isVisible = visible;

        if (_isVisible)
            UpdateLabelPosition(screenPos);

        float targetAlpha = _isVisible ? 1f : 0f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);

        if (!disableWhenInvisible) return;
        bool shouldBeActive = _canvasGroup.alpha > 0.001f || _isVisible;
        if (_labelRect.gameObject.activeSelf != shouldBeActive)
            _labelRect.gameObject.SetActive(shouldBeActive);
    }

    private void UpdateLabelPosition(Vector3 screenPos)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            _labelRect.position = screenPos;
            return;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            cam,
            out Vector2 localPoint
        );

        _labelRect.localPosition = localPoint;
    }

    private void LoadLabelPrefab()
    {
        if (labelPrefabReference == null)
        {
            Debug.LogError("Label prefab reference is not set!", this);
            return;
        }

        _labelLoadHandle = labelPrefabReference.LoadAssetAsync<GameObject>();
        _labelLoadHandle.Completed += OnLabelLoaded;
    }

    private void OnLabelLoaded(AsyncOperationHandle<GameObject> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _labelInstance = Instantiate(handle.Result, canvas.transform);
            _labelRect = _labelInstance.GetComponent<RectTransform>();
            _canvasGroup = _labelInstance.GetComponent<CanvasGroup>();

            if (_labelRect == null)
            {
                Debug.LogError("Label prefab must have a RectTransform component!", this);
                Destroy(_labelInstance);
                return;
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = _labelInstance.AddComponent<CanvasGroup>();
            }

            // Apply any pending text that was set before label loaded
            if (!string.IsNullOrEmpty(_pendingText))
            {
                SetLabelText(_pendingText);
                _pendingText = null;
            }
        }
        else
        {
            Debug.LogError($"Failed to load label prefab: {handle.OperationException}", this);
        }
    }

    private void OnDestroy()
    {
        if (_labelLoadHandle.IsValid())
        {
            Addressables.Release(_labelLoadHandle);
        }

        if (_labelInstance != null)
        {
            Destroy(_labelInstance);
        }
    }

    public void SetLabelText(string text)
    {
        if (_labelInstance != null)
        {
            var textComponent = _labelInstance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = text;
            }
        }
        else
        {
            // Queue the text to be applied when label loads
            _pendingText = text;
        }
    }
}