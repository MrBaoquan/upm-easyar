using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UNIHper;
using EasyARKit;
using UniRx;

public class EasyARSampleScript : SceneScriptBase
{

    private void Awake()
    {
        // step1. 添加EasyAR配置
        Managements.UI.AddConfig("EasyAR_uis");
        Managements.Resource.AddConfig("EasyAR_resources");
    }

    // Called once after scene is loaded
    private void Start()
    {
        Managements.UI.Show<ARDebuggerUI>();

        // step2. 注册EasyAR事件
        EasyARManager.Instance
            .OnFoundAsObservable()
            .Subscribe(_arTarget =>
            {
                Debug.LogWarning(
                    $"Found {_arTarget.Name}, Total: {EasyARManager.Instance.TrackedCount}"
                );
            });

        EasyARManager.Instance
            .OnLostAsObservable()
            .Subscribe(_arTarget =>
            {
                Debug.LogWarning(
                    $"Lost {_arTarget.Name}, Total: {EasyARManager.Instance.TrackedCount}"
                );
            });
    }

    // Called per frame after Start
    private void Update() { }

    // Called when scene is unloaded
    private void OnDestroy() { }

    // Called when application is quit
    private void OnApplicationQuit() { }
}
