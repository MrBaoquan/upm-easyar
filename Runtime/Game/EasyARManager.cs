using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using easyar;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UNIHper;
using UnityEngine.InputSystem;
using DigitalRubyShared;

namespace EasyARKit
{
    public class EasyARManager : SingletonBehaviour<EasyARManager>
    {
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
            Action<Camera, RenderTexture> targetTextureEventHandler = (_camera, _texture) =>
            {
                RenderTexture = _texture;
            };

            this.Get<CameraImageRenderer>("Camera Device")
                .RequestTargetTexture(targetTextureEventHandler);

            Observable
                .OnceApplicationQuit()
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
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                ToggleDebugUI();
            }
#endif
        }
    }
}
