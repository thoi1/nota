// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Text.Json;
using Nota.Application;
using Nota.Application.Analysis;

namespace Nota.Infrastructure;

/// <summary>Derived original-source observations, alongside existing peak/tempo caches.
/// They are excluded from the project manifest/undo model: preview never edits a song.</summary>
internal static class SourceTempoCache
{
    private sealed record Entry(double OriginalBpm, SourceBeatResult? Timing);
    private static string CachePath(string sourcePath)
        => Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath))!, "analysis", "source-tempo",
            Path.GetFileNameWithoutExtension(sourcePath) + ".json");
    public static void Save(IAudioEngine engine, long sourceId, string sourcePath)
    {
        double bpm = engine.TransientAnalysis.OriginalBpm(sourceId);
        var timing = engine.TransientAnalysis.OriginalTiming(sourceId);
        if (bpm <= 0 && timing is null) return;
        string path = CachePath(sourcePath); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        BundleContent.WriteAtomic(path, tmp => File.WriteAllText(tmp, JsonSerializer.Serialize(new Entry(bpm, timing))));
    }
    public static void Restore(IAudioEngine engine, long sourceId, string sourcePath)
    {
        string path = CachePath(sourcePath);
        if (!File.Exists(path) || new FileInfo(path).Length > 8 * 1024 * 1024) return;
        try
        {
            var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(path));
            if (entry is null) return;
            engine.TransientAnalysis.RememberOriginalBpm(sourceId, entry.OriginalBpm);
            if (entry.Timing is { } timing && engine.TryGetSampleInfo(sourceId, out var info)
                && timing.SourceEnd <= info.Frames && Math.Abs(timing.SampleRate - info.SampleRate) < 0.01)
                engine.TransientAnalysis.RestoreOriginalTiming(sourceId, timing);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
    }
}
