using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UNIHper;
using Michsky.MUIP;
using UniRx;
using TMPro;
using System;

namespace EasyARKit
{
    public class ARDebuggerUI : UIBase
    {
        List<ARTarget> arTargets => Managements.Config.Get<ARSettings>().ARTargets;
        Indexer arTexIndexer = new Indexer(0);
        public int ARTexIndex => arTexIndexer.Current;
        public ARTarget currentARTarget =>
            arTargets.Count > 0 ? arTargets[arTexIndexer.Current] : null;
        public string currentARName => currentARTarget?.Name;

        private RectTransform photoRect => this.Get<RectTransform>("img_outer");

        public IObservable<int> OnARTexIndexerChangedAsObservable()
        {
            return arTexIndexer.OnIndexChangedAsObservable();
        }

        private async Task LoadARTextures()
        {
            await Managements.Config.Get<ARSettings>().LoadARTextures();
            if (Managements.Config.Get<ARSettings>().ARTargets.Count <= 0)
                return;

            arTexIndexer.SetMax(arTargets.Count - 1);
            arTexIndexer.SetToMin();
        }

        public void SyncEditorProperties2ARTransform(ARTransform _arTransform)
        {
            this.Get<UInput_Slider>("ar_options/input_scale").Value = _arTransform.ModelScale;
            this.Get<UInput_Slider>("ar_options/input_rotate_x").Value = _arTransform.ModelEulerX;
            this.Get<UInput_Slider>("ar_options/input_rotate_y").Value = _arTransform.ModelEulerY;
            this.Get<UInput_Slider>("ar_options/input_rotate_z").Value = _arTransform.ModelEulerZ;
            this.Get<UInput_Slider>("ar_options/input_offset_x").Value = _arTransform.ModelOffsetX;
            this.Get<UInput_Slider>("ar_options/input_offset_y").Value = _arTransform.ModelOffsetY;
            this.Get<UInput_Slider>("ar_options/input_offset_z").Value = _arTransform.ModelOffsetZ;
        }

        // Start is called before the first frame update
        private async void Start()
        {
            var _rawImage = this.Get<RawImage>("img_outer/img_texture");
            arTexIndexer
                .OnIndexChangedAsObservable()
                .Subscribe(_idx =>
                {
                    if (arTargets.Count <= 0)
                    {
                        _rawImage.texture = null;
                        this.Get<TextMeshProUGUI>("img_outer/text_title").text = "没有AR图片";
                        return;
                    }

                    var _arTarget = arTargets[_idx];
                    _rawImage.texture = _arTarget.ARTexture;
                    this.Get<TextMeshProUGUI>("img_outer/text_title").text =
                        $"[{_idx}]  {_arTarget.Name}";
                    this.SyncEditorProperties2ARTransform(_arTarget);
                    this.Get("ar_options").SetActive(_arTarget.ARModel != null);
                });
            registerModelInputEvents();
            registerCameraInputEvents();

            await LoadARTextures();
            arTexIndexer.SetAndForceNotify(0);
        }

        private void registerCameraInputEvents()
        {
            var _btnNext = this.Get<ButtonManager>("img_outer/btn_next");
            var _btnPrev = this.Get<ButtonManager>("img_outer/btn_prev");
            this.Get<Toggle>("img_outer/toggle_img")
                .OnValueChangedAsObservable()
                .Subscribe(_enabled =>
                {
                    this.Get("img_outer/img_texture").SetActive(_enabled);
                });

            _btnNext.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    arTexIndexer.Next();
                });
            _btnPrev.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    arTexIndexer.Prev();
                });

            var _arSettings = Managements.Config.Get<ARSettings>();

            ReactiveProperty<float> _offsetX = new ReactiveProperty<float>(_arSettings.OffsetX);
            ReactiveProperty<float> _offsetY = new ReactiveProperty<float>(_arSettings.OffsetY);
            ReactiveProperty<float> _width = new ReactiveProperty<float>(_arSettings.PhotoWidth);
            ReactiveProperty<float> _height = new ReactiveProperty<float>(_arSettings.PhotoHeight);

            var _sliderX = this.Get<SliderManager>("camera_options/slider_x/slider");
            _sliderX.mainSlider.minValue = -0.5f;
            _sliderX.mainSlider.maxValue = 0.5f;
            _sliderX.mainSlider.value = _arSettings.OffsetX;
            _sliderX.UpdateUI();
            _sliderX.mainSlider.onValueChanged.AsObservable().Subscribe(_ => _offsetX.Value = _);

            var _sliderY = this.Get<SliderManager>("camera_options/slider_y/slider");
            _sliderY.mainSlider.minValue = -0.5f;
            _sliderY.mainSlider.maxValue = 0.5f;
            _sliderY.mainSlider.value = _arSettings.OffsetY;
            _sliderY.UpdateUI();
            _sliderY.mainSlider.onValueChanged.AsObservable().Subscribe(_ => _offsetY.Value = _);

            var _sliderWidth = this.Get<SliderManager>("camera_options/slider_width/slider");

            _sliderWidth.mainSlider.minValue = 0.1f;
            _sliderWidth.mainSlider.maxValue = 0.9f;
            _sliderWidth.mainSlider.value = _arSettings.PhotoWidth;
            _sliderWidth.UpdateUI();
            _sliderWidth.mainSlider.onValueChanged.AsObservable().Subscribe(_ => _width.Value = _);

            var _sliderHeight = this.Get<SliderManager>("camera_options/slider_height/slider");
            _sliderHeight.mainSlider.minValue = 0.1f;
            _sliderHeight.mainSlider.maxValue = 0.9f;
            _sliderHeight.mainSlider.value = _arSettings.PhotoHeight;
            _sliderHeight.mainSlider.onValueChanged
                .AsObservable()
                .Subscribe(_ => _height.Value = _);

            _width.Value = _arSettings.PhotoWidth;
            _height.Value = _arSettings.PhotoHeight;
            Observable
                .Merge(_width, _height, _offsetX, _offsetY)
                .Subscribe(_ =>
                {
                    _arSettings.PhotoWidth = Mathf.Clamp(_width.Value, 0.1f, 0.9f);
                    _arSettings.PhotoHeight = Mathf.Clamp(_height.Value, 0.1f, 0.9f);
                    _arSettings.OffsetX = Mathf.Clamp(_offsetX.Value, -0.5f, 0.5f);
                    _arSettings.OffsetY = Mathf.Clamp(_offsetY.Value, -0.5f, 0.5f);

                    _arSettings.Serialize();

                    Debug.LogWarning(
                        $"offsetX: {_arSettings.OffsetX} offsetY: {_arSettings.OffsetY} width: {_arSettings.PhotoWidth} height: {_arSettings.PhotoHeight}"
                    );
                    var _posX = _arSettings.OffsetX * (Screen.width / 2);
                    Debug.Log(
                        $"screenWidth: {Screen.width} screenHeight: {Screen.height} _posX: {_posX}"
                    );

                    var _sizeX = _arSettings.PhotoWidth * Screen.width;
                    var _sizeY = _arSettings.PhotoHeight * Screen.height;

                    photoRect.anchoredPosition = new Vector2(
                        (int)(_arSettings.OffsetX * (Screen.width - _sizeX)),
                        (int)(_arSettings.OffsetY * (Screen.height - _sizeY))
                    );
                    photoRect.sizeDelta = new Vector2(
                        (int)(_arSettings.PhotoWidth * Screen.width + 2),
                        (int)(_arSettings.PhotoHeight * Screen.height + 2)
                    );
                })
                .AddTo(this);

            // 按钮组操作
            var _btnDelete = this.Get<ButtonManager>("camera_options/btn_group/btn_delete");
            var _btnView = this.Get<ButtonManager>("camera_options/btn_group/btn_view");
            var _btnTakePhoto = this.Get<ButtonManager>("camera_options/btn_group/btn_takePhoto");
            var _btnSave = this.Get<ButtonManager>("camera_options/btn_group/btn_savePhoto");

            _btnDelete.checkForDoubleClick = false;
            _btnView.checkForDoubleClick = false;
            _btnSave.checkForDoubleClick = false;
            _btnTakePhoto.checkForDoubleClick = false;

            _btnView.gameObject.SetActive(false);
            _btnSave.gameObject.SetActive(false);
            photoRect.SetChildrenActive(arTargets.Count > 0);
            _btnDelete.gameObject.SetActive(arTargets.Count > 0);
            _btnTakePhoto.gameObject.SetActive(true);

            _btnView.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    _btnView.gameObject.SetActive(false);
                    _btnSave.gameObject.SetActive(false);
                    _btnDelete.gameObject.SetActive(true);
                    _btnTakePhoto.gameObject.SetActive(true);
                    photoRect.SetChildrenActive(true);
                    Managements.UI.HideSaveFileDialog();
                });

            _btnTakePhoto.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    _btnDelete.gameObject.SetActive(false);
                    _btnTakePhoto.gameObject.SetActive(false);
                    _btnView.gameObject.SetActive(arTargets.Count > 0);
                    _btnSave.gameObject.SetActive(true);
                    photoRect.SetChildrenActive(false);
                    photoRect.Get("anchor").SetActive(true);
                });
            _btnSave.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    Managements.UI.ShowSaveFileDialog(
#if UNITY_EDITOR || UNITY_STANDALONE
                        Path.Combine(Application.streamingAssetsPath, "ARTargets"),
#elif UNITY_ANDROID
                        Path.Combine(Application.persistentDataPath, "ARTargets"),
#endif
                        "*.jpg|*.png",
                        (_path) =>
                        {
                            var _renderTex = EasyARManager.Instance.RenderTexture;
                            Debug.Log(
                                "width: " + _renderTex.width + " height: " + _renderTex.height
                            );

                            RenderTexture.active = _renderTex;
                            Texture2D _tex = new Texture2D(
                                (int)photoRect.rect.width,
                                (int)photoRect.rect.height,
                                TextureFormat.RGB24,
                                false
                            );

                            var _x =
                                Screen.width / 2
                                + photoRect.anchoredPosition.x
                                - photoRect.sizeDelta.x / 2;
                            var _y =
                                Screen.height / 2
                                - photoRect.anchoredPosition.y
                                - photoRect.sizeDelta.y / 2;
                            _x = Mathf.Clamp(_x, 0, Screen.width);
                            _y = Mathf.Clamp(_y, 0, Screen.height);

                            _tex.ReadPixels(
                                new Rect(_x, _y, photoRect.sizeDelta.x, photoRect.sizeDelta.y),
                                0,
                                0
                            );
                            RenderTexture.active = null;
                            _tex.Apply();
                            _tex.SaveToFile(_path);
                            _arSettings.RefreshARTargets();
                            EasyARManager.Instance.BuildARTargets();
                            LoadARTextures()
                                .ToObservable()
                                .Subscribe(_ =>
                                {
                                    Managements.UI.HideSaveFileDialog();
                                    if (arTargets.Count > 0)
                                    {
                                        _btnView.gameObject.SetActive(true);
                                    }
                                });

                            // Observable
                            //     .FromCoroutine(() => takePhoto(_path))
                            //     .Subscribe(async _ =>
                            //     {
                            //         _arSettings.RefreshARTargets();
                            //         EasyARManager.Instance.BuildARTargets();
                            //         await LoadARTextures();
                            //         Managements.UI.HideSaveFileDialog();
                            //         if (arTargets.Count > 0)
                            //         {
                            //             _btnView.gameObject.SetActive(true);
                            //         }
                            //     });

                            return true;
                        }
                    );
                });

            _btnDelete.onClick
                .AsObservable()
                .Subscribe(_ =>
                {
                    Managements.UI.ShowConfirmPanel(
                        $"确认删除{currentARTarget.Name}吗?",
                        () =>
                        {
                            _arSettings.RemoveARTarget(currentARTarget);
                            arTexIndexer.SetMax(Mathf.Max(0, arTargets.Count - 1));
                            if (arTargets.Count <= 0)
                            {
                                photoRect.SetChildrenActive(false);
                                _btnDelete.gameObject.SetActive(false);
                                arTexIndexer.Notify();
                            }
                        }
                    );
                });
        }

        private void registerModelInputEvents()
        {
            this.Get<UInput_Slider>("ar_options/input_scale")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    var _arTarget = currentARTarget;
                    if (_arTarget == null)
                        return;
                    _arTarget.ModelScale = _;
                    _arTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_rotate_x")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelEulerX = _;
                    currentARTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_rotate_y")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelEulerY = _;
                    currentARTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_rotate_z")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelEulerZ = _;
                    currentARTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_offset_x")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelOffsetX = _;
                    currentARTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_offset_y")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelOffsetY = _;
                    currentARTarget.InvokeModelChangedEvent();
                });

            this.Get<UInput_Slider>("ar_options/input_offset_z")
                .OnValueChangedAsObservable()
                .Subscribe(_ =>
                {
                    if (currentARTarget == null)
                        return;
                    currentARTarget.ModelOffsetZ = _;
                    currentARTarget.InvokeModelChangedEvent();
                });
        }

        // 拍照
        private IEnumerator takePhoto(string InFileName)
        {
            Managements.UI.StashActiveUI();
            yield return new WaitForEndOfFrame();
            var _imgRect = photoRect;
            Texture2D _photo = new Texture2D(
                (int)_imgRect.rect.width,
                (int)_imgRect.rect.height,
                TextureFormat.RGB24,
                false
            );
            _photo.ReadPixels(
                new Rect(
                    _imgRect.position.x - _imgRect.rect.width / 2,
                    _imgRect.position.y - _imgRect.rect.height / 2,
                    _imgRect.rect.width,
                    _imgRect.rect.height
                ),
                0,
                0,
                false
            );
            _photo.Apply();
            _photo.SaveToFile(
                Path.Combine(Application.streamingAssetsPath, "ARTargets", InFileName)
            );

            Destroy(_photo);
            Managements.UI.PopStashedUI();
        }

        // Update is called once per frame
        private void Update() { }

        // Called when this ui is loaded
        protected override void OnLoaded()
        {
            this.Get<Button>("btn_save")
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    Managements.Config.Serialize<ARSettings>();
                });
        }

        // Called when this ui is shown
        protected override void OnShown() { }

        // Called when this ui is hidden
        protected override void OnHidden() { }
    }
}
