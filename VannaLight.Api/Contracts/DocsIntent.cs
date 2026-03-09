using System.Collections.Generic;

namespace VannaLight.Api.Contracts;

public class DocsIntent
{
    // --- NUEVO (Para el Router LLM) ---
    public List<string> RequestedFields { get; set; } = new();
    public bool ShowAll { get; set; }

    // --- LEGACY (Tus booleanos originales para retrocompatibilidad) ---
    public string? Periodo { get; set; }
    public bool WantsResina { get; set; }
    public bool WantsEmpaque { get; set; }
}