using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace HarborTorrent.Persistence.Repositories;

internal sealed class SettingsRepository : ISettingsRepository
{
    private readonly HarborDbContext _dbContext;

    public SettingsRepository(HarborDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GlobalSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.Settings.SingleOrDefaultAsync(s => s.Id == 1, cancellationToken);
        
        if (settings == null)
        {
            settings = new GlobalSettings
            {
                Id = 1,
                DefaultDownloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Downloads", 
                    "HarborTorrent")
            };
            
            await _dbContext.Settings.AddAsync(settings, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (settings.AnchorMaxDownloadSpeedBytes == 0 && settings.AnchorMaxUploadSpeedBytes == 0)
        {
            // Repair existing row that got 0 from the EF migration
            settings.AnchorMaxDownloadSpeedBytes = 10 * 1024;
            settings.AnchorMaxUploadSpeedBytes = 10 * 1024;
            _dbContext.Settings.Update(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    public async Task UpdateAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
    {
        _dbContext.Settings.Update(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
