namespace VannaLight.Api.Services;

public sealed record DocsIntent(bool WantsResina, bool WantsEmpaque, string? Periodo);

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

        return new DocsIntent(wantsResina, wantsEmpaque, periodo);
    }

    private static bool ContainsAny(string s, params string[] tokens)
        => tokens.Any(t => !string.IsNullOrWhiteSpace(t) && s.Contains(t, StringComparison.OrdinalIgnoreCase));
}