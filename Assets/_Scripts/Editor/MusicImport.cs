using UnityEditor;
using UnityEngine;

/// <summary>Music under Assets/_Audio/Music: Vorbis at medium quality, streamed, so the WebGL download stays small.</summary>
class MusicImport : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/_Audio/Music/")) return;
        var importer = (AudioImporter)assetImporter;
        var s = importer.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.45f;
        s.loadType = AudioClipLoadType.Streaming;
        importer.defaultSampleSettings = s;
        importer.loadInBackground = true;
    }
}
