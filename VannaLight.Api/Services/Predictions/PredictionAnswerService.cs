using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Abstractions;
using VannaLight.Core.Models;

namespace VannaLight.Api.Services.Predictions;

public class PredictionAnswerService : IPredictionAnswerService
{
    private readonly ILlmClient _llm;

    public PredictionAnswerService(ILlmClient llm)
    {
        _llm = llm;
    }

    public async Task<string> HumanizeAsync(PredictionIntent intent, CancellationToken ct)
    {
        if (!intent.IsSuccess || intent.PredictedValue == null)
            return $"No pude generar un pronóstico confiable para '{intent.EntityName}'. Asegúrate de que el producto exista en el historial.";

        var prompt = $@"<|im_start|>system
Eres un analista de datos. Redacta el pronóstico de ML.NET para el usuario.
Datos:
- Producto: {intent.EntityName}
- Meses a futuro: {intent.Horizon}
- Pronóstico: {intent.PredictedValue:N0} unidades
- Confianza: {intent.ConfidenceScore:P1}
- Perfil: {intent.UserTechnicalLevel}

Si el perfil es 'Operativo', sé muy directo con el número. Si es 'Gerencial', usa tono ejecutivo. NUNCA inventes números. Máximo 2 oraciones.
<|im_end|>
<|im_start|>user
Redacta el pronóstico.
<|im_end|>
<|im_start|>assistant
";

        var response = await _llm.CompleteAsync(prompt, ct);
        intent.HumanizedMessage = response.Trim();
        return intent.HumanizedMessage;
    }
}