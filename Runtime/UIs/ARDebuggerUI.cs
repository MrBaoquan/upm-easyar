using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UNIHper;
using DNHper;
using UnityEngine.InputSystem;
using Michsky.MUIP;
using UniRx;
using TMPro;

namespace EasyARKit
{
    public class ARDebuggerUI : UIBase
    {
        List<ARTarget> arTargets => Managements.Config.Get<ARSettings>().ARTargets;
        Indexer arTexIndexer = new Indexer(0);
        ARTarget currentARTarget => arTargets.Count > 0 ? arTargets[arTexIndexer.Current] : null;

        private RectTransform photoRect => this.Get<RectTransform>("img_outer");

        private async Task LoadARTextures()
        {
            await Managements.Config.Get<ARSettings>().LoadARTextures();
            if (Managements.Config.Get<ARSettings>().ARTargets.Count <= 0)
                return;

            arTexIndexer.SetMax(arTargets.Count - 1);
            arTexIndexer.SetToMin();
        }

        // Start is called before the first frame update
        private async void Start()
        {
            var _rawImage = this.Get<RawImage>("img_outer/img_texture");
            var _btnNext = this.Get<ButtonManager>("img_outer/btn_next");
            var _btnPrev = this.Get<ButtonManager>("img_outer/btn_prev");
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
                });
            await LoadARTextures();
            arTexIndexer.SetAndForceNotify(0);

            _btnNext
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    arTexIndexer.Next();
                });
            _btnPrev
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    arTexIndexer.Prev();
                });

            var _arSettings = Managements.Config.Get<ARSettings>();

            ReactiveProperty<float> _offsetX = new ReactiveProperty<float>(_arSettings.OffsetX);
            ReactiveProperty<float> _offsetY = new ReactiveProperty<float>(_arSettings.OffsetY);
            ReactiveProperty<float> _width = new ReactiveProperty<float>(_arSettings.PhotoWidth);
            ReactiveProperty<float> _height = new ReactiveProperty<float>(_arSettings.PhotoHeight);

            var _sliderX = this.Get<SliderManager>("options/slider_x/slider");
            _sliderX.mainSlider.minValue = -0.5f;
            _sliderX.mainSlider.maxValue = 0.5f;
            _sliderX.mainSlider.value = _arSettings.OffsetX;
            _sliderX.UpdateUI();
            _sliderX.OnValueChangedAsObservable().Subscribe(_ => _offsetX.Value = _);

            var _sliderY = this.Get<SliderManager>("options/slider_y/slider");
            _sliderY.mainSlider.minValue = -0.5f;
            _sliderY.mainSlider.maxValue = 0.5f;
            _sliderY.mainSlider.value = _arSettings.OffsetY;
            _sliderY.UpdateUI();
            _sliderY.OnValueChangedAsObservable().Subscribe(_ => _offsetY.Value = _);

            var _sliderWidth = this.Get<SliderManager>("options/slider_width/slider");

            _sliderWidth.mainSlider.minValue = 0.1f;
            _sliderWidth.mainSlider.maxValue = 0.9f;
            _sliderWidth.mainSlider.value = _arSettings.PhotoWidth;
            _sliderWidth.UpdateUI();
            _sliderWidth.OnValueChangedAsObservable().Subscribe(_ => _width.Value = _);

            var _sliderHeight = this.Get<SliderManager>("options/slider_height/slider");
            _sliderHeight.mainSlider.minValue = 0.1f;
            _sliderHeight.mainSlider.maxValue = 0.9f;
            _sliderHeight.mainSlider.value = _arSettings.PhotoHeight;
            _sliderHeight.OnValueChangedAsObservable().Subscribe(_ => _height.Value = _);

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

                    var _sizeX = _arSettings.PhotoWidth * Screen.width;
                    var _sizeY = _arSettings.PhotoHeight * Screen.height;

                    photoRect.anchoredPosition = new Vector2(
                        _arSettings.OffsetX * (Screen.width - _sizeX),
                        _arSettings.OffsetY * (Screen.height - _sizeY)
                    );
                    photoRect.sizeDelta = new Vector2(
                        _arSettings.PhotoWidth * Screen.width + 2,
                        _arSettings.PhotoHeight * Screen.height + 2
                    );
                })
                .AddTo(this);

            // 按钮组操作
            var _btnDelete = this.Get<ButtonManager>("options/btn_group/btn_delete");
            var _btnView = this.Get<ButtonManager>("options/btn_group/btn_view");
            var _btnTakePhoto = this.Get<ButtonManager>("options/btn_group/btn_takePhoto");
            var _btnSave = this.Get<ButtonManager>("options/btn_group/btn_savePhoto");

            _btnDelete.checkForDoubleClick = false;
            _btnView.checkForDoubleClick = false;
            _btnSave.checkForDoubleClick = false;
            _btnTakePhoto.checkForDoubleClick = false;

            _btnView.gameObject.SetActive(false);
            _btnSave.gameObject.SetActive(false);
            photoRect.SetChildrenActive(arTargets.Count > 0);
            _btnDelete.gameObject.SetActive(arTargets.Count > 0);
            _btnTakePhoto.gameObject.SetActive(true);

            _btnView
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    _btnView.gameObject.SetActive(false);
                    _btnSave.gameObject.SetActive(false);
                    _btnDelete.gameObject.SetActive(true);
                    _btnTakePhoto.gameObject.SetActive(true);
                    photoRect.SetChildrenActive(true);
                    Managements.UI.HideSaveFileDialog();
                });

            _btnTakePhoto
                .OnClickAsObservable()
                .Subscribe(_ =>
                {
                    _btnDelete.gameObject.SetActive(false);
                    _btnTakePhoto.gameObject.SetActive(false);
                    _btnView.gameObject.SetActive(arTargets.Count > 0);
                    _btnSave.gameObject.SetActive(true);
                    photoRect.SetChildrenActive(false);
                });
            _btnSave
                .OnClickAsObservable()
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
                                - photoRect.anchoredPosition.y / 2
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

            _btnDelete
                .OnClickAsObservable()
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
        protected override void OnLoaded() { }

        // Called when this ui is shown
        protected override void OnShown() { }

        // Called when this ui is hidden
        protected override void OnHidden() { }
    }
}
