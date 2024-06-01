using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using UNIHper;
using UniRx;

namespace EasyARKit
{
    public class ARTarget
    {
        [XmlAttribute]
        public string Name = string.Empty;

        [XmlAttribute]
        public string Path = "default.jpg";

        [XmlIgnore]
        public Texture2D ARTexture;

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
                    _path =>
                        new ARTarget
                        {
                            Path = $"ARTargets/{Path.GetFileName(_path)}",
                            Name = Path.GetFileNameWithoutExtension(_path)
                        }
                )
                .ToList();
        }
    }
}
