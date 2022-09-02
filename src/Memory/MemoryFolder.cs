﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OwlCore.Storage.Memory;

/// <summary>
/// A file implementation that resides in memory.
/// </summary>
public class MemoryFolder : IModifiableFolder, IFolderCanFastGetItem
{
    private readonly Dictionary<string, IStorable> _folderContents = new();
    private readonly MemoryFolderWatcher _folderWatcher;

    /// <summary>
    /// Creates a new instance of <see cref="MemoryFolder"/>.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    public MemoryFolder(string id, string name)
    {
        Id = id;
        Name = name;

        _folderWatcher = new MemoryFolderWatcher(this);
    }

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IAsyncEnumerable<IStorable> GetItemsAsync(StorableType type = StorableType.All, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type == StorableType.None)
            throw new ArgumentOutOfRangeException(nameof(type), $"{nameof(StorableType)}.{type} is not valid here.");

        return _folderContents.Values.Where(x =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return x is IFile && type.HasFlag(StorableType.File) ||
                   x is IFolder && type.HasFlag(StorableType.Folder);
        }).ToAsyncEnumerable();
    }

    /// <inheritdoc />
    public Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IFolderWatcher>(_folderWatcher);
    }

    /// <inheritdoc />
    public Task<IStorable> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_folderContents.ContainsKey(id))
            throw new FileNotFoundException();

        return Task.FromResult(_folderContents[id]);
    }

    /// <inheritdoc />
    public Task DeleteAsync(IStorable item, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_folderContents.ContainsKey(item.Id))
            throw new FileNotFoundException();

        _folderContents.Remove(item.Id);
        _folderWatcher.NotifyItemRemoved(new SimpleStorableItem(item.Id, item.Name));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = await fileToCopy.OpenStreamAsync(cancellationToken: cancellationToken);
        
        cancellationToken.ThrowIfCancellationRequested();
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var file = new MemoryFile($"{Guid.NewGuid()}", fileToCopy.Name, memoryStream);
        _folderContents.Add(file.Id, file);
        _folderWatcher.NotifyItemAdded(file);

        return file;
    }

    /// <inheritdoc />
    public async Task<IFile> MoveFromAsync(IFile fileToMove, IModifiableFolder source, bool overwrite = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Manual move. Slower, but covers all scenarios.
        var file = await CreateCopyOfAsync(fileToMove, overwrite, cancellationToken);
        await source.DeleteAsync(fileToMove, cancellationToken);
        _folderWatcher.NotifyItemRemoved(new SimpleStorableItem(fileToMove.Id, fileToMove.Name));

        return file;
    }

    /// <inheritdoc />
    public Task<IFolder> CreateFolderAsync(string name, bool overwrite = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingFolder = _folderContents.FirstOrDefault(x => x.Value.Name == name);

        if (overwrite && existingFolder is { })
        {
            _folderContents.Remove(existingFolder.Key);
            _folderWatcher.NotifyItemRemoved(new SimpleStorableItem(existingFolder.Value.Id, existingFolder.Value.Name));
        }

        var folder = new MemoryFolder($"{Guid.NewGuid()}", name);

        _folderContents.Add(folder.Id, folder);
        _folderWatcher.NotifyItemAdded(folder);

        return Task.FromResult<IFolder>(folder);
    }

    /// <inheritdoc />
    public Task<IFile> CreateFileAsync(string name, bool overwrite = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingFile = _folderContents.FirstOrDefault(x => x.Value.Name == name);

        if (overwrite && existingFile is { })
        {
            _folderContents.Remove(existingFile.Key);
            _folderWatcher.NotifyItemRemoved(new SimpleStorableItem(existingFile.Value.Id, existingFile.Value.Name));
        }

        var file = new MemoryFile($"{Guid.NewGuid()}", name, new MemoryStream());

        _folderContents.Add(file.Id, file);
        _folderWatcher.NotifyItemAdded(file);

        return Task.FromResult<IFile>(file);
    }
}