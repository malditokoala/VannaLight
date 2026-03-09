// Ruta: VannaLight.Core/Abstractions/IDocsAnswerService.cs
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models; // Asegúrate de que DocsAnswerResult también esté en Core.Models

namespace VannaLight.Core.Abstractions;

public interface IDocsAnswerService
{
    Task<DocsAnswerResult> AnswerAsync(string question, CancellationToken ct);
}