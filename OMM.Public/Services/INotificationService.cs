using OMM.Public.Models;

namespace OMM.Public.Services;

public interface INotificationService
{
    Task<IReadOnlyList<Notification>> GetAsync(CancellationToken cancellationToken = default);
    Task GenerateAsync(CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> DismissAsync(string id, CancellationToken cancellationToken = default);
}
