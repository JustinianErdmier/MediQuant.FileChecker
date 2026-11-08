namespace FileAuditor.Services.Scanning;

/// <summary>
///     Walks the input folders of the unindexed-files audit and reports every file whose name belongs to a chosen file type (milestone 4 spec §3.2).
///     <para>
///         <b>Scope.</b> An input folder covers its own files and, when <see cref="ScanFolder.IncludeSubfolders" /> is set, every folder beneath it.
///         Hidden and system files and folders are never listed (a skipped folder's contents go with it).
///     </para>
///     <para>
///         <b>Links.</b> A link (junction or symbolic link) whose resolved target lies inside the scope of an input folder (the folder itself, plus the folders
///         beneath it when that input includes subfolders) is not walked: the input's own visit lists those files, so the direct visit always wins. A link whose
///         target lies outside every input's scope is walked, and the files found through it carry a <see cref="ScannedFile.CanonicalPath" />, their path with
///         the link's prefix replaced by the resolved target, so that the caller can match an index that holds either path. Folders are keyed for the loop guard
///         by their canonical path, so a physical folder is never listed twice through different links, and a link loop ends at its first repeat. A link whose
///         target cannot be resolved (a DFS link, for one) is an ordinary folder.
///     </para>
///     <para>
///         <b>Attribution.</b> A file is reported once, under the deepest input folder whose scope covers the folder it sits in. The walk is a single
///         traversal over the union of the inputs, not one walk per input: each folder is listed at most once, its owner is worked out from the inputs, and
///         its subfolders are queued whenever any input that covers it includes subfolders. So with inputs <c>A</c> (with subfolders) and <c>A\B</c>
///         (without), <c>A\B\x.pdf</c> belongs to <c>A\B</c>, while <c>A\B\C\y.pdf</c> belongs to <c>A</c>, because <c>A\B</c> does not cover <c>C</c> but
///         <c>A</c> does. Because every folder is listed once and the names in a folder are unique, no file can be reported twice.
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
        Root[] roots = BuildRoots(folders);

        if (roots.Length == 0 || ct.IsCancellationRequested)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var walk = new Walk(roots, filter, onFile, onUnverified, linked.Token);

        foreach (Root root in roots)
        {
            walk.Enqueue(new Item(root.Path, Canonical: null, IsLink: true));
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
            foreach (Item folder in walk.Queue.GetConsumingEnumerable(walk.Token))
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

    private sealed record Root(string Path, string Entered, bool IncludeSubfolders);

    /// <summary>
    ///     A queued folder. <c>Canonical</c> is its canonical path when it lies beneath a walked link (null otherwise); <c>IsLink</c> is set for folders the
    ///     listing flagged as reparse points and for the input folders, whose target is resolved when they are listed.
    /// </summary>
    private readonly record struct Item(string Path, string? Canonical, bool IsLink);

    /// <summary>What the loop guard knows about a listed folder: whether its subfolders were queued, and whether it was already reported unverified.</summary>
    private sealed class Listed(bool descended)
    {
        public bool Descended { get; set; } = descended;

        public bool Failed { get; set; }
    }

    private readonly record struct Entry(string Name, bool IsDirectory, bool IsLink);

    private sealed class Walk
    {
        private readonly Root[] _roots;

        private readonly FileTypeFilter _filter;

        private readonly Action<ScannedFile> _onFile;

        private readonly Action<UnverifiedFolder> _onUnverified;

        private readonly ConcurrentDictionary<string, Listed> _listed = new(StringComparer.OrdinalIgnoreCase);

        private int _pending;

        public Walk(Root[] roots, FileTypeFilter filter, Action<ScannedFile> onFile, Action<UnverifiedFolder> onUnverified, CancellationToken token)
        {
            _roots        = roots;
            _filter       = filter;
            _onFile       = onFile;
            _onUnverified = onUnverified;
            Token         = token;
        }

        public CancellationToken Token { get; }

        public BlockingCollection<Item> Queue { get; } = new();

        /// <summary>Queues a folder; the walk is over when every queued folder has finished.</summary>
        public void Enqueue(Item folder)
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

        public void List(Item item)
        {
            string folder = item.Path;

            if (Token.IsCancellationRequested)
            {
                return;
            }

            // A link whose target lies in some input's scope is not walked: that input's own visit lists those files. This holds for a folder beneath a
            // walked link as well (its canonical path may fall inside the scope of an input nested in the link's target).
            string? canonical = item.IsLink && ResolveTarget(folder) is { } target ? target : item.Canonical;

            if (canonical is not null
                && _roots.Any(root => Covers(root, canonical)))
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

            // A folder whose identity was listed already is not listed again; this ends link loops. The identity is the canonical path beneath a walked
            // link and the folder's own path otherwise. If the earlier visit did not queue the subfolders but this one needs to (the folder is an input
            // without subfolders and is also reached as a subfolder of another input), this visit only queues them: the files were reported already. The
            // check and the upgrade are made under the entry's lock, so exactly one visit queues the subfolders.
            var    entry = new Listed(descend);
            string key   = canonical ?? folder;
            Listed known = _listed.GetOrAdd(key, entry);

            bool subfoldersOnly = false;

            if (!ReferenceEquals(known, entry))
            {
                lock (known)
                {
                    if (!descend || known.Descended)
                    {
                        return;
                    }

                    known.Descended = true;
                    subfoldersOnly  = true;
                }
            }

            var entries = new List<Entry>();

            try
            {
                // Only the enumeration is guarded: a failure in the filter or a callback is a bug, not an unreadable folder.
                var listing = new FileSystemEnumerable<Entry>(folder,
                                                             (ref FileSystemEntry e) => new Entry(e.FileName.ToString(), e.IsDirectory,
                                                                                                (e.Attributes & FileAttributes.ReparsePoint) != 0),
                                                             ListingOptions);

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
                // Nothing beneath a folder that failed is listed, now or through a later link to it, and it is reported unverified once however many
                // visits (a first listing and an upgrade, say) fail.
                bool alreadyReported;

                lock (known)
                {
                    known.Descended = true;
                    alreadyReported = known.Failed;
                    known.Failed    = true;
                }

                if (alreadyReported)
                {
                    return;
                }

                MappedOutcome mapped = Win32ErrorMapper.Map(ex);

                // A folder that vanished mid-run is not a "file missing" outcome; it is a folder that could not be checked.
                _onUnverified(mapped.Outcome == CheckOutcome.MissingDoesNotExist
                                  ? new UnverifiedFolder(folder, CheckOutcome.UnverifiedOther, "Folder not found", ex.Message)
                                  : new UnverifiedFolder(folder, mapped.Outcome, mapped.Reason, mapped.SystemMessage));

                return;
            }

            var files   = new List<(string Name, FileTypePattern Type)>();
            var folders = new List<Entry>();

            foreach (Entry listed in entries)
            {
                if (listed.IsDirectory)
                {
                    folders.Add(listed);
                }
                else if (!subfoldersOnly)
                {
                    FileTypePattern? type = _filter.Match(listed.Name);

                    if (type is not null)
                    {
                        files.Add((listed.Name, type));
                    }
                }
            }

            foreach ((string name, FileTypePattern type) in files)
            {
                _onFile(new ScannedFile(folder + name, owner.Entered, type, canonical is null ? null : canonical + name));
            }

            if (!descend)
            {
                return;
            }

            foreach (Entry sub in folders)
            {
                string path = folder + sub.Name + Path.DirectorySeparatorChar;

                Enqueue(new Item(path, canonical is null ? null : canonical + sub.Name + Path.DirectorySeparatorChar, sub.IsLink));
            }
        }

        private static bool Covers(Root root, string folder)
            => root.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)
               || (root.IncludeSubfolders && folder.StartsWith(root.Path, StringComparison.OrdinalIgnoreCase));

        // The normalised final target of a link, or null when the folder is not a link or its target cannot be resolved (then it is an ordinary folder).
        private static string? ResolveTarget(string path)
        {
            try
            {
                FileSystemInfo? target = new DirectoryInfo(path).ResolveLinkTarget(true);

                return target is null ? null : NormaliseFolder(target.FullName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
