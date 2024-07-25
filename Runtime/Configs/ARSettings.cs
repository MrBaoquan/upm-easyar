using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using UNIHper;
using UniRx;
using UnityEngine.Events;

namespace EasyARKit
{public class ARTransform
{
    [XmlAttribute]
    public float ModelScale = 1;

    [XmlAttribute]
    public float ModelEulerX = 0;

    [XmlAttribute]
    public float ModelEulerY = 0;

    [XmlAttribute]
    public float ModelEulerZ = 0;

    [XmlAttribute]
    public float ModelOffsetX = 0;

    [XmlAttribute]
    public float ModelOffsetY = 0;

    [XmlAttribute]
    public float ModelOffsetZ = 0;

    public void SyncFromARTarget(ARTransform aRTarget)
    {
        if (aRTarget == null)
            return;
        this.ModelScale = aRTarget.ModelScale;
        this.ModelEulerX = aRTarget.ModelEulerX;
        this.ModelEulerY = aRTarget.ModelEulerY;
        this.ModelEulerZ = aRTarget.ModelEulerZ;
        this.ModelOffsetX = aRTarget.ModelOffsetX;
        this.ModelOffsetY = aRTarget.ModelOffsetY;
        this.ModelOffsetZ = aRTarget.ModelOffsetZ;
    }

    public void SyncTargetTransform(Transform target)
    {
        if (target == null)
            return;
        target.localScale = new Vector3(ModelScale, ModelScale, ModelScale);
        target.localEulerAngles = new Vector3(ModelEulerX, ModelEulerY, ModelEulerZ);
        target.localPosition = new Vector3(ModelOffsetX, ModelOffsetY, ModelOffsetZ);
    }
}
    public class ARTarget : ARTransform
    {
        // 是否忽略位置信息应用到ARModel
        public static bool IngoreTransform = false;

        [XmlAttribute]
        public string Name = string.Empty;

        [XmlAttribute]
        public string Path = "default.jpg";

        [XmlAttribute]
        public string ModelAsset = "";

       

        [XmlIgnore]
        public Texture2D ARTexture;

        [XmlIgnore]
        public GameObject ARModel = null;

        [XmlIgnore]
        public UnityEvent<ARTarget> OnModelPropertyChanged = new UnityEvent<ARTarget>();

        public void InvokeModelChangedEvent(){
            OnModelPropertyChanged?.Invoke(this);
            SyncModelTransform();
        }

        public void SyncModelTransform(){
            if(IngoreTransform) return;
            SyncTargetTransform(ARModel?.transform);
        }


        public async Task<Texture2D> LoadARTexture()
        {
            Debug.LogWarning($"Load ARTexture: {ARTextureFullPath}");
            var _arTex = await Managements.Resource
                .LoadTexture2D(ARTextureFullPath)
                .Catch(
                    (System.Exception _ex) =>
                    {
                        Debug.LogError($"Load ARTexture Failed: {_ex.Message}");
                        return Observable.Return<Texture2D>(null);
                    }
                );
            ARTexture = _arTex;
            return _arTex;
        }

        public string ARTextureFullPath =>
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            System.IO.Path.Combine(Application.streamingAssetsPath, Path);
#elif UNITY_ANDROID
            System.IO.Path.Combine("jar:file://" + Application.persistentDataPath, Path);
#endif
    }

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    [SerializedAt(AppPath.StreamingDir)]
#elif UNITY_ANDROID
    [SerializedAt(AppPath.PersistentDir)]
#endif
    public class ARSettings : UConfig
    {
        [XmlArray("ARTargets")]
        [XmlArrayItem("ARTarget")]
        public List<ARTarget> ARTargets = new List<ARTarget>();

        [XmlAttribute]
        public float PhotoWidth = 0.5f;

        [XmlAttribute]
        public float PhotoHeight = 0.5f;

        [XmlAttribute]
        public float OffsetX = 0;

        [XmlAttribute]
        public float OffsetY = 0;

        public async Task LoadARTextures()
        {
            if (ARTargets.Count == 0)
                return;
            await Observable.Zip(
                ARTargets.Select(_target => _target.LoadARTexture().ToObservable())
            );
        }

        public bool RemoveARTarget(ARTarget arTarget)
        {
            var _removed = ARTargets.Remove(arTarget);
            if (_removed)
            {
                this.Serialize();
                File.Delete(arTarget.ARTextureFullPath);
            }
            return _removed;
        }

        private string arTargetDir
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            get => Path.Combine(Application.streamingAssetsPath, "ARTargets");
#elif UNITY_ANDROID
            get => Path.Combine(Application.persistentDataPath, "ARTargets");
#endif
        }

        protected override void OnLoaded()
        {
            RefreshARTargets();
            this.Serialize();
        }

        public void RefreshARTargets()
        {
            if (!Directory.Exists(arTargetDir))
                Directory.CreateDirectory(arTargetDir);

            ARTargets = Directory
                .GetFiles(arTargetDir, "*.*", SearchOption.TopDirectoryOnly)
                .Where(_path => _path.EndsWith(".jpg") || _path.EndsWith(".png"))
                .Select(
                    _path =>{
                        var _fileName = Path.GetFileNameWithoutExtension(_path);
                        return new ARTarget
                        {
                            Path = $"ARTargets/{Path.GetFileName(_path)}",
                            Name = _fileName,
                            ModelAsset = $"AR_Model_{_fileName}",
                        };
                    }
                        
                ).Select(_arTarget=>ARTargets.FirstOrDefault(_target=>_target.Name==_arTarget.Name)??_arTarget)
                .Select(_arTarget=>{
                    _arTarget.ModelAsset = _arTarget.ModelAsset==string.Empty?$"AR_Model_{_arTarget.Name}":_arTarget.ModelAsset;
                    return _arTarget;
                })
                .ToList();
        }
    }
}
