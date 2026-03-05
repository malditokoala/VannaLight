namespace VannaLight.Api.Services.Docs;

public static class WiAnswerBuilder
{
    public static string Build(Dictionary<string, string> facts, DocsIntent intent)
    {
        if (facts is null || facts.Count == 0)
            return "Encontré páginas relevantes, pero no pude extraer el dato de forma confiable.";

        var periodo = intent.Periodo; // "Turno" | "2 Horas" | null
        var lines = new List<string> { "Necesitas:" };

        if (intent.WantsResina)
        {
            if (TryGet(facts, out var resinNp, "ResinaNP"))
                lines.Add($"- Resina (N/P): {resinNp}");

            AddResinaPorPeriodo(lines, facts, periodo);
        }

        if (intent.WantsEmpaque)
        {
            AddEmpaquePorPeriodo(lines, facts, periodo);
        }

        if (lines.Count == 1)
        {
            var p = periodo != null ? $" para el período {periodo}" : "";
            return $"Encontré la WI, pero no pude extraer resina/empaque{p}. Revisa las citas.";
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddResinaPorPeriodo(List<string> lines, Dictionary<string, string> facts, string? periodo)
    {
        if (periodo is null)
        {
            // ✅ sin periodo: muestra ambos (si existen)
            if (TryGetAny(facts, out var turno, "ResinaTurno", "Resina por Turno"))
                lines.Add($"- Resina por turno: {turno}");
            if (TryGetAny(facts, out var dosH, "Resina2Horas", "Resina por 2 Horas", "Resina por Dos Horas"))
                lines.Add($"- Resina por 2 horas: {dosH}");

            // fallback ultra: schema viejo
            if (!TryHasAny(lines, "- Resina por turno:", "- Resina por 2 horas:") &&
                TryGet(facts, out var legacy, "ResinaLbs"))
                lines.Add($"- Resina: {legacy}");

            return;
        }

        if (periodo.Equals("Turno", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetAny(facts, out var v, "ResinaTurno", "Resina por Turno", "ResinaLbs"))
                lines.Add($"- Resina por turno: {v}");
        }
        else if (periodo.Equals("2 Horas", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetAny(facts, out var v, "Resina2Horas", "Resina por 2 Horas", "Resina por Dos Horas", "ResinaLbs"))
                lines.Add($"- Resina por 2 horas: {v}");
        }
        else
        {
            // periodo desconocido: fallback
            if (TryGet(facts, out var legacy, "ResinaLbs"))
                lines.Add($"- Resina: {legacy}");
        }
    }

    private static string NormalizeEmpaque(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s ?? string.Empty;

        // Orden deseado: Cajas primero, luego Separadores, luego el resto.
        var parts = s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .ToList();

        parts.Sort((a, b) => GetEmpaquePriority(a).CompareTo(GetEmpaquePriority(b)));

        return string.Join(", ", parts);

        static int GetEmpaquePriority(string p)
        {
            if (p.Contains("Cajas", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Caja", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (p.Contains("Separadores", StringComparison.OrdinalIgnoreCase) ||
                p.Contains("Separador", StringComparison.OrdinalIgnoreCase))
                return 1;

            return 2;
        }
    }

    private static void AddEmpaquePorPeriodo(List<string> lines, Dictionary<string, string> facts, string? periodo)
    {
        if (periodo is null)
        {
            if (TryGetAny(facts, out var turno, "EmpaqueTurno", "Empaque por Turno"))
                lines.Add($"- Empaque por turno: {NormalizeEmpaque(turno)}");

            if (TryGetAny(facts, out var dosH, "Empaque2Horas", "Empaque por 2 Horas", "Empaque por Dos Horas"))
                lines.Add($"- Empaque por 2 horas: {NormalizeEmpaque(dosH)}");

            // fallback schema viejo
            if (!TryHasAny(lines, "- Empaque por turno:", "- Empaque por 2 horas:") &&
                TryGet(facts, out var legacy, "Empaque"))
                lines.Add($"- Empaque: {NormalizeEmpaque(legacy)}");

            return;
        }

        if (periodo.Equals("Turno", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetAny(facts, out var v, "EmpaqueTurno", "Empaque por Turno", "Empaque"))
                lines.Add($"- Empaque por turno: {NormalizeEmpaque(v)}");

            return;
        }

        if (periodo.Equals("2 Horas", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetAny(facts, out var v, "Empaque2Horas", "Empaque por 2 Horas", "Empaque por Dos Horas", "Empaque"))
                lines.Add($"- Empaque por 2 horas: {NormalizeEmpaque(v)}");

            return;
        }

        // periodo desconocido
        if (TryGet(facts, out var legacy2, "Empaque"))
            lines.Add($"- Empaque: {NormalizeEmpaque(legacy2)}");
    }
    private static bool TryGet(Dictionary<string, string> facts, out string value, string key)
        => facts.TryGetValue(key, out value!) && !string.IsNullOrWhiteSpace(value);

    private static bool TryGetAny(Dictionary<string, string> facts, out string value, params string[] keys)
    {
        foreach (var k in keys)
        {
            if (facts.TryGetValue(k, out value!) && !string.IsNullOrWhiteSpace(value))
                return true;
        }
        value = "";
        return false;
    }

    private static bool TryHasAny(List<string> lines, params string[] prefixes)
        => lines.Any(l => prefixes.Any(p => l.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
}