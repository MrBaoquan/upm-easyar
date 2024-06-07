## EasyAR Kit

### 开始使用
- 在Hierarchy中执行菜单EasyAR Sense/Init EasyAR Kit
  

```csharp
 // SceneEntryScript.cs 场景脚本中添加下述代码

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
```

### 1.0.1 (2024.06.07)
- 依赖Modern UI Pack 插件

### v1.0.0 (2024.05.30)
- 可直接标定识别目标，并进行跟踪
- 提供调试UI界面