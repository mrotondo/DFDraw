using UnityEngine;

public static class StaticResourcesLoader
{
    public static ComputeShader SpheresVolumeTextureShader { get; private set; }
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void LoadStaticAssets()
    {
        SpheresVolumeTextureShader = Resources.Load<ComputeShader>("SpheresVolumeTexture");
    }
}