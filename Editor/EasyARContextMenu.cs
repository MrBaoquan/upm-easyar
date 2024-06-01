using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using easyar;

namespace EasyARKit.Editor
{
    public class EasyARContextMenu
    {
        [MenuItem("GameObject/EasyAR Sense/Init EasyAR Kit", false, 12)]
        static void InitEasyARKit()
        {
#if UNITY_2023_1_OR_NEWER
            var arController = GameObject.FindFirstObjectByType<EasyARController>();
#else
            var arController = GameObject.FindObjectOfType<EasyARController>();
#endif
            if (arController == null)
            {
                EditorApplication.ExecuteMenuItem(
                    "GameObject/EasyAR Sense/Image Tracking/AR Session (Image Tracking Preset)"
                );
#if UNITY_2023_1_OR_NEWER
                arController = GameObject.FindFirstObjectByType<EasyARController>();
#else
                arController = GameObject.FindObjectOfType<EasyARController>();
#endif
            }
            if (arController == null)
            {
                Debug.LogError("EasyARController not found!");
                return;
            }
            if (arController.GetComponent(typeof(EasyARManager)) == null)
            {
                arController.gameObject.AddComponent(typeof(EasyARManager));
            }
        }
    }
}
