#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class V1MusicImporter : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if(!assetPath.StartsWith("Assets/Resources/RiverbankMusic/"))return;
        var importer=(AudioImporter)assetImporter;
        var settings=importer.defaultSampleSettings;
        settings.loadType=AudioClipLoadType.Streaming;
        settings.compressionFormat=AudioCompressionFormat.Vorbis;
        settings.quality=.75f;
        importer.defaultSampleSettings=settings;
        importer.forceToMono=false;importer.loadInBackground=true;
    }
}
#endif
