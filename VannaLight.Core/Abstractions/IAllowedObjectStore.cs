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

        Task<IReadOnlyList<AllowedObject>> GetAllObjectsAsync(
            string domain,
            CancellationToken cancellationToken = default);

        Task<long> UpsertAsync(
            AllowedObject allowedObject,
            CancellationToken cancellationToken = default);

        Task<bool> SetIsActiveAsync(
            long id,
            bool isActive,
            CancellationToken cancellationToken = default);
    }
}