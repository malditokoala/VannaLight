using System;
using System.Collections.Generic;
using System.Text;

namespace VannaLight.Core.Models;

/// <summary>
/// Representa los ajustes de rendimiento de llama.cpp para un hardware específico.
/// </summary>
public sealed class LlmRuntimeProfile
{
    public string Name { get; set; } = "Default";
    public int? GpuLayerCount { get; set; }
    public uint? ContextSize { get; set; }
    public int? Threads { get; set; }
    public int? BatchThreads { get; set; }
    public uint? BatchSize { get; set; }
    public uint? UBatchSize { get; set; }
    public bool? FlashAttention { get; set; }
    public bool UseMemorymap { get; set; } = true;
    public bool NoKqvOffload { get; set; } = false;
    public bool? OpOffload { get; set; }
}
