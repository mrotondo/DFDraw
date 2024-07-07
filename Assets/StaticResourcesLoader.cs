using UnityEngine;

public static class StaticResourcesLoader
{
    public static ComputeShader UpdateSdfShader { get; private set; }
 
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void LoadStaticAssets()
    {
        UpdateSdfShader = Resources.Load<ComputeShader>("UpdateSdf");
    }
}