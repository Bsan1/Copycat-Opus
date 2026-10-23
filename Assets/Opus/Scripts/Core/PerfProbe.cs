using Unity.Profiling;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>Averages over a measuring window (ms and per-frame counts; -1 for a value that could not be measured).</summary>
    public struct PerfStats
    {
        public int Frames;
        public float FrameMs;
        public float CpuMainMs;
        public float CpuRenderMs;
        public float GpuMs;
        public float Batches;
        public float SetPassCalls;
        public float Triangles;

        public override string ToString() =>
            $"frame {FrameMs:F2} ms ({(FrameMs > 0f ? 1000f / FrameMs : 0f):F0} fps) · CPU main {Fmt(CpuMainMs)} · " +
            $"batch {Count(Batches)} · setpass {Count(SetPassCalls)} · triangles {(Triangles < 0f ? "?" : (Triangles / 1e6f).ToString("F2") + " M")}";

        static string Fmt(float ms) => ms < 0f ? "?" : ms.ToString("F2") + " ms";
        static string Count(float value) => value < 0f ? "?" : value.ToString("F0");
    }

    /// <summary>
    /// Splits the frame time into CPU (main and render thread) and GPU (FrameTimingManager; "Frame Timing Stats" must be
    /// enabled in the player settings), and reads the draw counts from the profiler counters. The GPU time does not come back
    /// reliably on this machine (DX12, laptop); it is not shown in the reports. Throughout the measuring window
    /// <see cref="Sample"/> is called every frame.
    /// </summary>
    public sealed class PerfProbe : System.IDisposable
    {
        readonly FrameTiming[] timing = new FrameTiming[1];
        ProfilerRecorder batches;
        ProfilerRecorder setPass;
        ProfilerRecorder triangles;
        int frames, timedFrames, gpuFrames;
        double frameSum, mainSum, renderSum, gpuSum, batchSum, setPassSum, triangleSum;
        int counterFrames;

        public PerfProbe()
        {
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
        }

        public void Sample()
        {
            frames++;
            frameSum += Time.unscaledDeltaTime * 1000.0;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                timedFrames++;
                mainSum += timing[0].cpuMainThreadFrameTime;
                renderSum += timing[0].cpuRenderThreadFrameTime;
                if (timing[0].gpuFrameTime > 0.0)
                {
                    gpuFrames++;
                    gpuSum += timing[0].gpuFrameTime;
                }
            }

            if (batches.Valid && batches.LastValue > 0)
            {
                counterFrames++;
                batchSum += batches.LastValue;
                setPassSum += setPass.Valid ? setPass.LastValue : 0;
                triangleSum += triangles.Valid ? triangles.LastValue : 0;
            }
        }

        public PerfStats Result => new PerfStats
        {
            Frames = frames,
            FrameMs = frames > 0 ? (float)(frameSum / frames) : 0f,
            CpuMainMs = timedFrames > 0 ? (float)(mainSum / timedFrames) : -1f,
            CpuRenderMs = timedFrames > 0 ? (float)(renderSum / timedFrames) : -1f,
            GpuMs = gpuFrames > 0 ? (float)(gpuSum / gpuFrames) : -1f,
            Batches = counterFrames > 0 ? (float)(batchSum / counterFrames) : -1f,
            SetPassCalls = counterFrames > 0 && setPass.Valid ? (float)(setPassSum / counterFrames) : -1f,
            Triangles = counterFrames > 0 && triangles.Valid ? (float)(triangleSum / counterFrames) : -1f,
        };

        public void Dispose()
        {
            batches.Dispose();
            setPass.Dispose();
            triangles.Dispose();
        }
    }
}
