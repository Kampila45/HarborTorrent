using HarborTorrent.Application.Contracts.Directories;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Directories.ListDirectories;

internal sealed class ListDirectoriesQueryHandler : IRequestHandler<ListDirectoriesQuery, Result<IReadOnlyCollection<DirectoryItemDto>>>
{
    public Task<Result<IReadOnlyCollection<DirectoryItemDto>>> Handle(ListDirectoriesQuery request, CancellationToken cancellationToken)
    {
        var directories = new List<DirectoryItemDto>();
        var targetPath = request.Path;

        try
        {
            // If the path is provided but doesn't exist, traverse up until a valid parent is found
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                while (!string.IsNullOrWhiteSpace(targetPath) && !Directory.Exists(targetPath))
                {
                    targetPath = Directory.GetParent(targetPath)?.FullName;
                }
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                if (OperatingSystem.IsWindows())
                {
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .Select(d => new DirectoryItemDto
                        {
                            Name = d.Name,
                            Path = d.Name
                        });
                    
                    directories.AddRange(drives);
                }
                else
                {
                    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!string.IsNullOrWhiteSpace(homeDir) && Directory.Exists(homeDir))
                    {
                        directories.Add(new DirectoryItemDto { Name = "Home", Path = homeDir });
                    }
                    directories.Add(new DirectoryItemDto { Name = "Root System (/)", Path = "/" });
                }
            }
            else
            {
                var dirInfo = new DirectoryInfo(targetPath);
                var subDirs = dirInfo.GetDirectories()
                    .Select(d => new DirectoryItemDto
                    {
                        Name = d.Name,
                        Path = d.FullName
                    });
                
                directories.AddRange(subDirs);
            }

            return Task.FromResult(Result<IReadOnlyCollection<DirectoryItemDto>>.Success(directories.AsReadOnly()));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(Result<IReadOnlyCollection<DirectoryItemDto>>.Failure(Error.Conflict("Access to the path is denied.")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyCollection<DirectoryItemDto>>.Failure(Error.Conflict(ex.Message)));
        }
    }
}
