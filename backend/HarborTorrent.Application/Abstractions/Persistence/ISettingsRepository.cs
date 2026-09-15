using HarborTorrent.Domain.Settings;

namespace HarborTorrent.Application.Abstractions.Persistence;

public interface ISettingsRepository
{
    Task<GlobalSettings> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(GlobalSettings settings, CancellationToken cancellationToken = default);
}
