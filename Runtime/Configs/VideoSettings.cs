using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using UNIHper;

public class VideoInfo
{
    [XmlAttribute]
    public string Target = string.Empty;

    [XmlAttribute]
    public string Path = string.Empty;

    [XmlAttribute]
    public float StartTime = 0f;

    [XmlAttribute]
    public float EndTime = 0;

    [XmlIgnore]
    public List<string> Targets
    {
        get => Target.Split("|").ToList();
    }
}

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
[SerializedAt(AppPath.StreamingDir)]
#elif UNITY_ANDROID
[SerializedAt(AppPath.PersistentDir)]
#endif
public class VideoSettings : UConfig
{
    [XmlArray("VideoInfos")]
    [XmlArrayItem("VideoItem")]
    public List<VideoInfo> VideoInfos = new List<VideoInfo>();

    private string videoDir
    {
        // windows平台
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        get => Path.Combine(Application.streamingAssetsPath, "Videos");
#elif UNITY_ANDROID
        get => Path.Combine(Application.persistentDataPath, "Videos");
#endif
    }

    // Called once when config data is loaded
    protected override void OnLoaded()
    {
        if (!Directory.Exists(videoDir))
        {
            Directory.CreateDirectory(videoDir);
        }
        if (VideoInfos.Count <= 0)
        {
            VideoInfos = Directory
                .GetFiles(videoDir, "*.mp4")
                .Select(_path => new VideoInfo { Path = "Videos/" + Path.GetFileName(_path) })
                .ToList();
            this.Serialize();
        }
    }
}
