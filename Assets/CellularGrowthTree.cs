using SDF;
using UnityEditor;
using UnityEngine;
using System.Collections;

public class CellularGrowthTree : MonoBehaviour
{
    public uint SdfVolumeSideLength = 128;
    public uint SdfVolumeNumCellsPerDimension = 4;

    private SpheresVolumeTexture _sdfVolumeTexture;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _sdfVolumeTexture = new SpheresVolumeTexture(SdfVolumeSideLength, SdfVolumeNumCellsPerDimension);
        _sdfVolumeTexture.ConfigureRenderer(GetComponent<DFRenderer>());
    }

    public void Quit()
    {
        StartCoroutine(CleanUpAndQuit());
    }

    IEnumerator CleanUpAndQuit()
    {
        yield return new WaitForEndOfFrame();
        _sdfVolumeTexture.DisposeOfComputeBuffer();
        EditorApplication.ExitPlaymode();
        Application.Quit();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
