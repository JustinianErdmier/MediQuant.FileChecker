namespace FileAuditor.Services.Scanning;

/// <summary>
///     Walks the input folders of the unindexed-files audit and reports every file whose name belongs to a chosen file type (milestone 4 spec §3.2).
///     <para>
///         <b>Scope.</b> An input folder covers its own files and, when <see cref="ScanFolder.IncludeSubfolders" /> is set, every folder beneath it.
///         Hidden and system files and folders are never listed (a skipped folder's contents go with it).
///     </para>
///     <para>
///         <b>Links.</b> Links are never followed or checked. An input folder that is a junction or symbolic link, a junction or symbolic link met while walking, and
///         a matching file that is a symbolic link are each reported once through the unverified callback as <see cref="CheckOutcome.UnverifiedLinkNotFollowed" />
///         (a folder's path ends in <c>\</c>) and nothing beneath them is listed. Only an entry that has a link target (<see cref="FileSystemInfo.LinkTarget" />) is a
///         link: any other reparse point (a DFS folder, a OneDrive placeholder, a deduplicated file) is an ordinary folder or file. With no link followed, every
///         folder is reached by exactly one route, so no loop and no duplicate is possible.
///     </para>
///     <para>
///         <b>Attribution.</b> A file is reported once, under the deepest input folder whose scope covers the folder it sits in. The walk is a single
///         traversal over the union of the inputs, not one walk per input: each folder is listed once, its owner is worked out from the inputs, and
///         its subfolders are queued whenever any input that covers it includes subfolders. So with inputs <c>A</c> (with subfolders) and <c>A\B</c>
///         (without), <c>A\B\x.pdf</c> belongs to <c>A\B</c>, while <c>A\B\C\y.pdf</c> belongs to <c>A</c>, because <c>A\B</c> does not cover <c>C</c> but
///         <c>A</c> does. An input nested in another is queued once, as an input, never again as a subfolder.
///     </para>
///     <para>
///         <b>Failures.</b> A folder that cannot be listed is reported once through the unverified callback and nothing beneath it is listed. Files
///         from a listing that fails part-way are not reported, so a folder is either wholly checked or wholly unverified.
///     </para>
///     <para>
///         <b>Cancellation.</b> <see cref="WalkAsync" /> does not throw when the token is cancelled: it stops starting listings, abandons the ones in progress
///         (their files are not reported) and returns once every worker has noticed. The caller must check the token afterwards to tell a stopped walk from a
///         finished one.
///     </para>
/// </summary>
public sealed class FolderWalker
{
    /// <summary>The most folder listings that run at once; each listing over a network share is a round trip, so the parallelism is in listing.</summary>
    public const int MaxConcurrentListings = 16;

    private static readonly EnumerationOptions ListingOptions = new()
    {
        RecurseSubdirectories    = false,
        AttributesToSkip         = FileAttributes.Hidden | FileAttributes.System,
        IgnoreInaccessible       = false,
        ReturnSpecialDirectories = false
    };

    /// <summary>Walks the folders, calling back for every matching file and every folder that could not be listed.</summary>
    /// <param name="folders">The input folders; each path is normalised with <see cref="NormaliseFolder" />, which the caller may have done already.</param>
    /// <param name="filter">Decides which files are reported, and under which file type.</param>
    /// <param name="onFile">Called for each file found; may be called concurrently from several threads.</param>
    /// <param name="onUnverified">Called for each folder that could not be listed; may be called concurrently from several threads.</param>
    /// <param name="ct">Stops the walk promptly; see the cancellation note on the type.</param>
    /// <returns>A task that completes when no folder is queued or being listed, or the walk was stopped.</returns>
    public Task WalkAsync(IReadOnlyList<ScanFolder> folders, FileTypeFilter filter, Action<ScannedFile> onFile, Action<UnverifiedFolder> onUnverified,
                          CancellationToken ct)
    {
        return Task.Run(() => WalkAsyncCore(folders, filter, onFile, onUnverified, ct), CancellationToken.None);
    }

    private static async Task WalkAsyncCore(IReadOnlyList<ScanFolder> folders, FileTypeFilter filter, Action<ScannedFile> onFile,
                                            Action<UnverifiedFolder> onUnverified, CancellationToken ct)
    {
        Root[] all = BuildRoots(folders);

        if (all.Length == 0 || ct.IsCancellationRequested)
        {
            return;
        }

        // An input that is itself a link is not walked; it is reported and the other inputs go on.
        var roots = new List<Root>();

        foreach (Root root in all)
        {
            if (IsLink(new DirectoryInfo(root.Path)))
            {
                onUnverified(LinkNotFollowed(root.Path));
            }
            else
            {
                roots.Add(root);
            }
        }

        if (roots.Count == 0)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var walk = new Walk(roots.ToArray(), all.Select(root => root.Path).ToHashSet(StringComparer.OrdinalIgnoreCase), filter, onFile, onUnverified, linked.Token);

        foreach (Root root in roots)
        {
            walk.Enqueue(root.Path);
        }

        // Dedicated threads: the listings block, and the thread pool would ramp up to 16 blocked threads slowly.
        Task[] workers = Enumerable.Range(0, MaxConcurrentListings)
                                   .Select(_ => Task.Factory.StartNew(() => RunWorker(walk, linked), CancellationToken.None, TaskCreationOptions.LongRunning,
                                                                     TaskScheduler.Default))
                                   .ToArray();

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            // Every worker has finished by now (WhenAll waits for all of them, faulted or not).
            walk.Queue.Dispose();
        }
    }

    private static void RunWorker(Walk walk, CancellationTokenSource linked)
    {
        try
        {
            foreach (string folder in walk.Queue.GetConsumingEnumerable(walk.Token))
            {
                try
                {
                    walk.List(folder);
                }
                finally
                {
                    walk.Finished();
                }
            }
        }
        catch (OperationCanceledException) when (walk.Token.IsCancellationRequested)
        {
            // Stopped by the caller, or because another worker failed (then that worker's exception is what the walk throws). An
            // OperationCanceledException from a callback while the token is not cancelled is not a stop: it falls through to the next catch.
        }
        catch
        {
            // A callback threw: stop the others, then let the exception surface from WalkAsync.
            linked.Cancel();

            throw;
        }
    }

    private static Root[] BuildRoots(IReadOnlyList<ScanFolder> folders)
    {
        var byPath = new Dictionary<string, Root>(StringComparer.OrdinalIgnoreCase);

        foreach (ScanFolder folder in folders)
        {
            string normalised = NormaliseFolder(folder.Path);

            // The same folder listed twice (pre-flight refuses it) is one input; it includes subfolders if either entry does.
            byPath[normalised] = byPath.TryGetValue(normalised, out Root? existing)
                                     ? existing with { IncludeSubfolders = existing.IncludeSubfolders || folder.IncludeSubfolders }
                                     : new Root(normalised, folder.Path, folder.IncludeSubfolders);
        }

        // Deepest first, so the first root that covers a folder is its owner.
        return byPath.Values.OrderByDescending(root => root.Path.Length).ToArray();
    }

    /// <summary>Makes an input folder's path canonical: a full path with exactly one trailing separator, as the walk and the index query both use it.</summary>
    /// <param name="path">The folder's path as entered.</param>
    /// <returns>The full path with a trailing directory separator.</returns>
    public static string NormaliseFolder(string path)
    {
        string full = Path.GetFullPath(path);

        return Path.EndsInDirectorySeparator(full) ? full : full + Path.DirectorySeparatorChar;
    }

    // Whether the entry is a junction or symbolic link (it has a link target); any other reparse point, or an entry that cannot be read, is not a link.
    private static bool IsLink(FileSystemInfo info)
    {
        try
        {
            return info.LinkTarget is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static UnverifiedFolder LinkNotFollowed(string path) => new(path, CheckOutcome.UnverifiedLinkNotFollowed, ReasonText: null, SystemMessage: null);

    /// <summary>An input folder: its normalised path, the path as entered, and its scope.</summary>
    private sealed record Root(string Path, string Entered, bool IncludeSubfolders);

    private readonly record struct Entry(string Name, bool IsDirectory, bool IsLink);

    private sealed class Walk
    {
        private readonly Root[] _roots;

        private readonly HashSet<string> _inputPaths;

        private readonly FileTypeFilter _filter;

        private readonly Action<ScannedFile> _onFile;

        private readonly Action<UnverifiedFolder> _onUnverified;

        private int _pending;

        public Walk(Root[] roots, HashSet<string> inputPaths, FileTypeFilter filter, Action<ScannedFile> onFile, Action<UnverifiedFolder> onUnverified,
                    CancellationToken token)
        {
            _roots        = roots;
            _inputPaths   = inputPaths;
            _filter       = filter;
            _onFile       = onFile;
            _onUnverified = onUnverified;
            Token         = token;
        }

        public CancellationToken Token { get; }

        public BlockingCollection<string> Queue { get; } = new();

        /// <summary>Queues a folder; the walk is over when every queued folder has finished.</summary>
        public void Enqueue(string folder)
        {
            Interlocked.Increment(ref _pending);

            Queue.Add(folder);
        }

        /// <summary>Marks one queued folder as done; the last one closes the queue, which ends every worker.</summary>
        public void Finished()
        {
            if (Interlocked.Decrement(ref _pending) == 0)
            {
                Queue.CompleteAdding();
            }
        }

        public void List(string folder)
        {
            if (Token.IsCancellationRequested)
            {
                return;
            }

            Root? owner   = null;
            bool  descend = false;

            foreach (Root root in _roots)
            {
                if (!Covers(root, folder))
                {
                    continue;
                }

                owner ??= root;

                if (root.IncludeSubfolders)
                {
                    descend = true;
                }
            }

            if (owner is null)
            {
                return;
            }

            var entries = new List<Entry>();

            try
            {
                // Only the enumeration is guarded: a failure in the filter or a callback is a bug, not an unreadable folder.
                var listing = new FileSystemEnumerable<Entry>(folder, ToEntry, ListingOptions);

                foreach (Entry listed in listing)
                {
                    Token.ThrowIfCancellationRequested();

                    entries.Add(listed);
                }
            }
            catch (OperationCanceledException) when (Token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MappedOutcome mapped = Win32ErrorMapper.Map(ex);

                // A folder that vanished mid-run is not a "file missing" outcome; it is a folder that could not be checked.
                _onUnverified(mapped.Outcome == CheckOutcome.MissingDoesNotExist
                                  ? new UnverifiedFolder(folder, CheckOutcome.UnverifiedOther, "Folder not found", ex.Message)
                                  : new UnverifiedFolder(folder, mapped.Outcome, mapped.Reason, mapped.SystemMessage));

                return;
            }

            foreach (Entry entry in entries)
            {
                if (entry.IsDirectory)
                {
                    if (descend)
                    {
                        Descend(folder, entry);
                    }

                    continue;
                }

                FileTypePattern? type = _filter.Match(entry.Name);

                if (type is null)
                {
                    continue;
                }

                // A file that is a link is reported, not checked.
                if (entry.IsLink)
                {
                    _onUnverified(LinkNotFollowed(folder + entry.Name));
                }
                else
                {
                    _onFile(new ScannedFile(folder + entry.Name, owner.Entered, type));
                }
            }
        }

        private void Descend(string folder, Entry sub)
        {
            string path = folder + sub.Name + Path.DirectorySeparatorChar;

            // An input is queued, or reported if it is a link, on its own: here it would be a second visit.
            if (_inputPaths.Contains(path))
            {
                return;
            }

            if (sub.IsLink)
            {
                _onUnverified(LinkNotFollowed(path));
            }
            else
            {
                Enqueue(path);
            }
        }

        // A reparse point is a link only when it has a link target; the check is made for reparse points alone, so ordinary entries cost nothing extra.
        private static Entry ToEntry(ref FileSystemEntry e)
            => new(e.FileName.ToString(), e.IsDirectory, (e.Attributes & FileAttributes.ReparsePoint) != 0 && IsLink(e.ToFileSystemInfo()));

        private static bool Covers(Root root, string folder)
            => root.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)
               || (root.IncludeSubfolders && folder.StartsWith(root.Path, StringComparison.OrdinalIgnoreCase));
    }
}