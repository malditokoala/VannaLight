using System;
using System.Linq;
using VannaLight.Api.Contracts;

namespace VannaLight.Api.Services;

// Lo mantenemos estático y en Services como tu "Plan B" (Fallback determinístico)
public static class DocsIntentParser
{
    public static DocsIntent Parse(string question)
    {
        var q = (question ?? "").Trim().ToLowerInvariant();

        var wantsResina = ContainsAny(q, "resina", "resin", "material");
        var wantsEmpaque = ContainsAny(q, "empaque", "pack", "caja", "cajas", "separador", "separadores", "divisor", "divisores");

        // fallback: “¿Qué necesito?” => asume ambos
        if (!wantsResina && !wantsEmpaque && ContainsAny(q, "que necesito", "qué necesito", "necesito"))
        {
            wantsResina = true;
            wantsEmpaque = true;
        }

        string? periodo = null;
        if (ContainsAny(q, "turno", "shift")) periodo = "Turno";
        else if (ContainsAny(q, "2 horas", "dos horas", "2h", "2-horas")) periodo = "2 Horas";

        // Devolvemos la NUEVA clase DocsIntent
        return new DocsIntent
        {
            WantsResina = wantsResina,
            WantsEmpaque = wantsEmpaque,
            Periodo = periodo,
            // Como es el parser manual (legacy), no llenamos RequestedFields. 
            // Así el WiAnswerBuilder sabrá que debe usar el formato viejo.
            RequestedFields = new System.Collections.Generic.List<string>(),
            ShowAll = false
        };
    }

    private static bool ContainsAny(string s, params string[] tokens)
        => tokens.Any(t => !string.IsNullOrWhiteSpace(t) && s.Contains(t, StringComparison.OrdinalIgnoreCase));
}