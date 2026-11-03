// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.

using System.Diagnostics;
using Nota.Infrastructure;
using Nota.Presentation;

string file = Path.Combine(Path.GetTempPath(), "nota-tempo-lag-" + Guid.NewGuid().ToString("N") + ".wav");
try
{
    const int rate = 44100, frames = rate * 20;
    using (var w = new BinaryWriter(File.Create(file)))
    {
        w.Write("RIFF"u8); w.Write(36 + frames * 2); w.Write("WAVEfmt "u8);
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
        w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(frames * 2);
        for (int i = 0; i < frames; i++) w.Write((short)(12000 * Math.Sin(2 * Math.PI * 440 * i / rate)));
    }
    using var engine = new NotaEngine();
    engine.RenderOffline(new float[128], 64, rate);
    for (int i = 0; i < 2; i++)
    {
        int track = engine.AddAudioTrack();
        int clip = engine.AddAudioClip(track, file, 0);
        engine.SetClipWarp(track, clip, true, 3);
    }
    var vm = new TransportViewModel(engine);
    var watch = new Stopwatch();
    foreach (int bpm in new[] { 121, 132, 145, 110 })
    {
        watch.Restart(); vm.Bpm = bpm; watch.Stop();
        Console.WriteLine($"Project BPM {bpm}: setter {watch.Elapsed.TotalMilliseconds:F3} ms, pending frames {engine.WarpBuildRemaining}");
        engine.RenderOffline(new float[128], 64);
    }
}
finally { File.Delete(file); }
