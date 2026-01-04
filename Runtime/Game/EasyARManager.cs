using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using easyar;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UNIHper;
using UnityEngine.InputSystem;

namespace EasyARKit
{
    using UNIHper.UI;

    using UniRx;
    using UniRx.Triggers;

    public class EasyARManager : SingletonBehaviour<EasyARManager>
    {
        private const int MAX_CAMERA_OPEN_RETRIES = 5;
        private const float RETRY_DELAY_SECONDS = 2f;

        private string[] lastDeviceNames;
        private bool isRestarting;
        private float lastChangeTime;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod, UnityEditor.InitializeOnEnterPlayMode]
        public static void AddAssemblyToUNIHper()
        {
            var _currentAssembly = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            UNIHperSettings.AddAssemblyToSettingsIfNotExists(_currentAssembly);
        }
#endif

        private Transform arTrackersRoot;
        public Transform ARTrackersRoot
        {
            get
            {
                if (arTrackersRoot == null)
                {
                    arTrackersRoot = new GameObject("ARTrackers").transform;
                }
                return arTrackersRoot;
            }
        }
        private UnityEvent<ARTarget> onFound = new UnityEvent<ARTarget>();
        private UnityEvent<ARTarget> onLost = new UnityEvent<ARTarget>();
        private Dictionary<string, ImageTargetController> allTargets =
            new Dictionary<string, ImageTargetController>();

        /// <summary>
        /// 当前跟踪到的目标数量
        /// </summary>
        /// <returns></returns>
        public int TrackedCount
        {
            get => TrackedControllers.Count;
        }

        /// <summary>
        /// 当前跟踪到的目标控制器
        /// </summary>
        /// <returns></returns>
        public List<ImageTargetController> TrackedControllers
        {
            get => Controllers.Where(_controller => _controller.IsTracked).ToList();
        }

        public List<ImageTargetController> Controllers => allTargets.Values.ToList();

        public IObservable<ARTarget> OnFoundAsObservable()
        {
            return onFound.AsObservable();
        }

        public IObservable<ARTarget> OnLostAsObservable()
        {
            return onLost.AsObservable();
        }

        List<GameObject> arTrackerGOs = new List<GameObject>();

        private ReactiveCommand<UniRx.Unit> onRebuild = new ReactiveCommand<UniRx.Unit>();

        public IObservable<UniRx.Unit> OnRebuildAsObservable()
        {
            return onRebuild;
        }

        public void BuildARTargets()
        {
            arTrackerGOs.ForEach(_ => DestroyImmediate(_));
            allTargets.Clear();
            var _imageTrackerFilter = FindObjectOfType<ImageTrackerFrameFilter>();

            arTrackerGOs = Managements.Config
                .Get<ARSettings>()
                .ARTargets.Select(_arTarget =>
                {
                    var _arGO = new GameObject();
                    _arGO.name = _arTarget.Name;
                    _arGO.transform.SetParent(ARTrackersRoot);
                    if (Managements.Resource.Exists<GameObject>(_arTarget.ModelAsset))
                    {
                        var _model = Managements.Resource.Get<GameObject>(_arTarget.ModelAsset);
                        var _modelInstance = Instantiate(_model, _arGO.transform);
                        _arTarget.ARModel = _modelInstance;
                        _arTarget.SyncModelTransform();
                    }

                    var _imageARTargetController = _arGO.AddComponent<ImageTargetController>();
                    Debug.Log($"Create AR Target {_arTarget.Name}");
                    allTargets.Add(_arTarget.Name, _imageARTargetController);
                    _imageARTargetController.SourceType = ImageTargetController
                        .DataSource
                        .ImageFile;
                    _imageARTargetController.ImageFileSource.PathType = PathType.Absolute;
                    _imageARTargetController.ImageFileSource.Path = _arTarget.ARTextureFullPath;
                    _imageARTargetController.Tracker = _imageTrackerFilter;
                    _imageARTargetController.TargetFound += () =>
                    {
                        Observable
                            .NextFrame()
                            .Subscribe(_ =>
                            {
                                onFound.Invoke(_arTarget);
                            });
                    };
                    _imageARTargetController.TargetLost += () =>
                    {
                        Observable
                            .NextFrame()
                            .Subscribe(_ =>
                            {
                                onLost.Invoke(_arTarget);
                            });
                    };
                    return _arGO;
                })
                .ToList();
            onRebuild.Execute(UniRx.Unit.Default);
        }

        public bool IsTargetsTracked(List<string> targetNames)
        {
            var _trackedNames = TrackedControllers.Select(_ => _.name);
            return targetNames.All(_targetName => _trackedNames.Contains(_targetName));
        }

        public bool IsTargetTracked(string targetName)
        {
            var _trackedNames = TrackedControllers.Select(_ => _.name);
            return _trackedNames.Contains(targetName);
        }

        public RenderTexture RenderTexture { get; private set; } = null;

        // Start is called before the first frame update
        void Start()
        {
            EasyARController.Instance.ShowPopupMessage = false;

            Action<Camera, RenderTexture> targetTextureEventHandler = (_camera, _texture) =>
            {
                RenderTexture = _texture;
            };

            this.Get<CameraImageRenderer>("Camera Device")
                .RequestTargetTexture(targetTextureEventHandler);

            this.OnDestroyAsObservable()
                .Subscribe(_ =>
                {
                    this.Get<CameraImageRenderer>("Camera Device")
                        .DropTargetTexture(targetTextureEventHandler);
                });

            Managements.Framework
                .OnInitializedAsObservable()
                .Subscribe(_ =>
                {
                    BuildARTargets();
                    Managements.Framework
                        .OnToggleDebugAsObservable()
                        .Skip(2)
                        .Subscribe(_ =>
                        {
                            ToggleDebugUI();
                        });
                });

            Observable.FromCoroutine(() => InitialCameraOpenWithRetry()).Subscribe();

            lastDeviceNames = WebCamTexture.devices.Select(d => d.name).ToArray();

            Observable
                .Interval(TimeSpan.FromSeconds(1))
                .Where(_ => !isRestarting)
                .Subscribe(_ =>
                {
                    var currentDevices = WebCamTexture.devices;
                    var currentDeviceNames = currentDevices.Select(d => d.name).ToArray();

                    if (!lastDeviceNames.SequenceEqual(currentDeviceNames))
                    {
                        var currentTime = Time.realtimeSinceStartup;
                        if (currentTime - lastChangeTime < 1.5f)
                        {
                            return;
                        }

                        lastChangeTime = currentTime;
                        lastDeviceNames = currentDeviceNames;

                        Debug.Log(
                            $"Camera devices changed. Old: {string.Join(",", lastDeviceNames)}, New: {string.Join(",", currentDeviceNames)}"
                        );

                        RestartCameraAsync().Subscribe();
                    }
                });
        }

        List<UIBase> _uis = new List<UIBase>();

        public void ToggleDebugUI()
        {
            var _arDebuggerUI = Managements.UI.Get<ARDebuggerUI>();
            if (_arDebuggerUI.isShowing)
            {
                _arDebuggerUI.Hide();
                this._uis.ForEach(_ui => _ui.Show());
                this._uis.Clear();
            }
            else
            {
                this._uis = Managements.UI.ActiveUIs;
                Managements.UI.HideAll();
                _arDebuggerUI.Show();
            }
        }

        // Update is called once per frame
        void Update()
        {
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR) && ENABLE_INPUT_SYSTEM

            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                ToggleDebugUI();
            }
#else
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ToggleDebugUI();
            }
#endif
        }

        private IEnumerator InitialCameraOpenWithRetry()
        {
            yield return new WaitForSeconds(1f);

            var cameraDevice = GameObject.Find("Camera Device");
            if (cameraDevice == null)
            {
                Debug.LogError("Camera Device not found in scene!");
                yield break;
            }

            var cameraSource = cameraDevice.GetComponent<CameraDeviceFrameSource>();
            if (cameraSource == null)
            {
                Debug.LogError("CameraDeviceFrameSource component not found!");
                yield break;
            }

            for (int attempt = 1; attempt <= MAX_CAMERA_OPEN_RETRIES; attempt++)
            {
                bool isOpen = IsCameraOpened(cameraSource);
                if (isOpen)
                {
                    Debug.Log($"Camera opened successfully on attempt {attempt}");
                    yield break;
                }

                Debug.LogWarning($"Camera not opened, attempt {attempt}/{MAX_CAMERA_OPEN_RETRIES}");

                if (attempt < MAX_CAMERA_OPEN_RETRIES)
                {
                    Exception retryError = null;
                    cameraSource.Close();
                    yield return new WaitForSeconds(0.5f);

                    try
                    {
                        cameraSource.Open();
                    }
                    catch (Exception ex)
                    {
                        retryError = ex;
                        Debug.LogError($"Error retrying camera open: {ex.Message}");
                    }

                    yield return new WaitForSeconds(RETRY_DELAY_SECONDS);

                    if (retryError != null && attempt >= MAX_CAMERA_OPEN_RETRIES - 1)
                    {
                        Debug.LogError(
                            $"Failed to open camera after {MAX_CAMERA_OPEN_RETRIES} attempts"
                        );
                    }
                }
            }

            Debug.LogError($"Failed to open camera after {MAX_CAMERA_OPEN_RETRIES} attempts");
        }

        private bool IsCameraOpened(CameraDeviceFrameSource cameraSource)
        {
            if (cameraSource == null || cameraSource.Device == null)
                return false;

            try
            {
                using (var parameters = cameraSource.Device.cameraParameters())
                {
                    return parameters != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private IObservable<UniRx.Unit> RestartCameraAsync()
        {
            return Observable.FromCoroutine<UniRx.Unit>(
                observer => RestartCameraCoroutine(observer)
            );
        }

        private IEnumerator RestartCameraCoroutine(IObserver<UniRx.Unit> observer)
        {
            if (isRestarting)
            {
                Debug.LogWarning("Camera restart already in progress, skipping...");
                observer.OnCompleted();
                yield break;
            }

            isRestarting = true;
            Exception caughtException = null;

            var cameraDevice = GameObject.Find("Camera Device");
            CameraDeviceFrameSource cameraSource = null;
            if (cameraDevice)
            {
                cameraSource = cameraDevice.GetComponent<CameraDeviceFrameSource>();
            }

            Debug.Log("Starting camera hot-swap process...");

            if (cameraSource != null)
            {
                Debug.Log("Closing old camera device...");
                cameraSource.Close();
                yield return new WaitForSeconds(0.3f);
            }

            if (cameraDevice)
                cameraDevice.SetActive(false);
            if (EasyARController.Instance)
                EasyARController.Instance.gameObject.SetActive(false);

            yield return new WaitForSeconds(0.2f);

            if (EasyARController.Initialized)
            {
                Debug.Log("Deinitializing EasyAR...");
                try
                {
                    EasyARController.Deinitialize();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                yield return new WaitForSeconds(1.5f);
            }

            if (caughtException == null)
            {
                Debug.Log("Reinitializing EasyAR...");
                try
                {
                    EasyARController.Initialize();
                }
                catch (Exception ex)
                {
                    caughtException = ex;
                }
                yield return new WaitForSeconds(0.5f);
            }

            if (caughtException == null)
            {
                if (EasyARController.Instance)
                {
                    EasyARController.Instance.ShowPopupMessage = false;
                    EasyARController.Instance.gameObject.SetActive(true);
                }

                yield return null;

                if (cameraDevice)
                {
                    cameraDevice.SetActive(true);
                    yield return null;

                    if (cameraSource != null)
                    {
                        bool cameraOpened = false;
                        for (int attempt = 1; attempt <= MAX_CAMERA_OPEN_RETRIES; attempt++)
                        {
                            Debug.Log(
                                $"Opening camera device, attempt {attempt}/{MAX_CAMERA_OPEN_RETRIES}..."
                            );

                            Exception openError = null;
                            try
                            {
                                cameraSource.Open();
                            }
                            catch (Exception ex)
                            {
                                openError = ex;
                                Debug.LogWarning(
                                    $"Camera open attempt {attempt} failed: {ex.Message}"
                                );
                            }

                            yield return new WaitForSeconds(1f);

                            if (IsCameraOpened(cameraSource))
                            {
                                Debug.Log($"Camera opened successfully on attempt {attempt}");
                                cameraOpened = true;
                                break;
                            }
                            else
                            {
                                Debug.LogWarning($"Camera device not ready on attempt {attempt}");
                                if (attempt < MAX_CAMERA_OPEN_RETRIES)
                                {
                                    cameraSource.Close();
                                    yield return new WaitForSeconds(RETRY_DELAY_SECONDS);
                                }
                                else if (openError != null)
                                {
                                    caughtException = openError;
                                }
                            }
                        }

                        if (!cameraOpened && caughtException == null)
                        {
                            caughtException = new Exception(
                                $"Failed to open camera after {MAX_CAMERA_OPEN_RETRIES} attempts"
                            );
                        }
                    }
                }

                yield return new WaitForSeconds(0.5f);
            }

            isRestarting = false;

            if (caughtException != null)
            {
                Debug.LogError(
                    $"Error during camera hot-swap: {caughtException.Message}\n{caughtException.StackTrace}"
                );
                observer.OnError(caughtException);
            }
            else
            {
                Debug.Log("Camera hot-swap completed successfully.");
                observer.OnNext(UniRx.Unit.Default);
                observer.OnCompleted();
            }
        }
    }
}
