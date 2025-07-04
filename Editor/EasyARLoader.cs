using UnityEditor;
using UNIHper.Editor;
using UnityEngine;

class EasyARLoader
{
    [InitializeOnLoadMethod]
    static void Init()
    {
        AssemblyCfgUtil.AddAssembly("EasyARKit.Runtime");
        if (!AddressableUtil.IsEntryExist("Packages/com.parful.easyar/Assets/Prefabs/UIs"))
        {
            AddressableUtil.AddToLabel(
                "com.parful.easyar",
                "Packages/com.parful.easyar/Assets/Prefabs/UIs"
            );
        }

        ResCfgUtil.AddPersistenceItem(
            new ResCfgUtil.ResourceItem
            {
                driver = "Addressable",
                type = "GameObject",
                label = "com.parful.easyar"
            }
        );
    }
}
