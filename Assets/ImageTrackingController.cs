using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTrackingController : MonoBehaviour
{
    [SerializeField] private ARTrackedImageManager imageManager;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Text _eventCount;
    private int _eventCountValue;
    private Dictionary<string, GameObject> spawned = new();

    private void OnEnable()
    {
        imageManager.trackablesChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        imageManager.trackablesChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        _eventCountValue++;
        _eventCount.text = args.updated.Count.ToString();
        
        foreach (var img in args.added)
        {
            var name = img.referenceImage.name;

            if (!spawned.ContainsKey(name))
            {
                var go = Instantiate(modelPrefab, img.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                spawned[name] = go;
            }

            spawned[name].SetActive(img.trackingState == TrackingState.Tracking);
        }

        foreach (var img in args.updated)
        {
            var name = img.referenceImage.name;

            if (spawned.TryGetValue(name, out var go))
            {
                if (img.trackingState == TrackingState.None || img.trackingState == TrackingState.Limited)
                {
                    Destroy(go);
                    spawned.Remove(name);
                }
                else
                {
                    go.SetActive(img.trackingState == TrackingState.Tracking);
                }
            }
        }

        foreach (var img in args.removed)
        {
            var name = img.Value.referenceImage.name;

            if (spawned.TryGetValue(name, out var go))
            {
                Destroy(go);
                spawned.Remove(name);
            }
        }
    }
}
