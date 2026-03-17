using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VannaLight.Core.Models;

namespace VannaLight.Core.Abstractions
{
    public interface IAllowedObjectStore
    {
        Task<IReadOnlyList<AllowedObject>> GetActiveObjectsAsync(
            string domain,
            CancellationToken cancellationToken = default);

        Task<bool> IsAllowedAsync(
            string domain,
            string schemaName,
            string objectName,
            CancellationToken cancellationToken = default);
    }
}