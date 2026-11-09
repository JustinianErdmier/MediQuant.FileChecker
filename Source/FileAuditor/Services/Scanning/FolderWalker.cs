namespace FileAuditor.Services.Scanning;

/// <summary>
///     Walks the input folders of the unindexed-files audit and reports every file whose name belongs to a chosen file type.
///     <para>
///         <b>Scope.</b> An input folder covers its own files and, when <see cref="ScanFolder.IncludeSubfolders" /> is set, every folder beneath it. Hidden and system files and
///         folders are never listed (a skipped folder's contents go with it).
///     </para>
///     <para>
///         <b>Links.</b> Links are never followed or checked. An input folder that is a junction or symbolic link, a junction or symbolic link met whilst walking, and a matching
///         file that is a symbolic link are each reported once through the unverified callback as <see cref="CheckOutcomes.UnverifiedLinkNotFollowed" /> (a folder's path ends in
///         <c>\</c>); nothing beneath them is listed. Only an entry that has a link target (<see cref="FileSystemInfo.LinkTarget" />) is a link: any other reparse point (a DFS
///         folder, a OneDrive placeholder, a deduplicated file) is an ordinary folder or file. With no link followed, every folder is reached by exactly one route, so no loop and no
///         duplicate is possible.
///     </para>
///     <para>
///         <b>Attribution.</b> A file is reported once, under the deepest input folder whose scope covers the folder it sits in. The walk is a single traversal over the union of
///         the inputs, not one walk per input: each folder is listed once, its owner is worked out from the inputs, and its subfolders are queued whenever any input that covers it
///         includes subfolders. So with inputs <c>A</c> (with subfolders) and <c>A\B</c> (without), <c>A\B\x.pdf</c> belongs to <c>A\B</c>, while <c>A\B\C\y.pdf</c> belongs to
///         <c>A</c>, because <c>A\B</c> does not cover <c>C</c> but <c>A</c> does. An input nested in another is queued once, as an input, never again as a subfolder.
///     </para>
///     <para>
///         <b>Failures.</b> A folder that cannot be listed is reported once through the unverified callback and nothing beneath it is listed. Files from a listing that fails
///         part-way are not reported, so a folder is either wholly checked or wholly unverified.
///     </para>
///     <para>
///         <b>Cancellation.</b> <see cref="WalkAsync" /> does not throw when the token is cancelled: it stops starting listings, abandons the ones in progress (their files are not
///         reported) and returns once every worker has noticed. The caller must check the token afterwards to tell a stopped walk from a finished one.
///     </para>
/// </summary>
public static class FolderWalker
{
    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>The most folder listings that run at once; each listing over a network share is a round trip, so the parallelism is in listing.</summary>
    public const int MaxConcurrentListings = 16;

    /// <summary>The options for listing one folder: no recursion, hidden and system entries skipped, and every failure thrown rather than ignored.</summary>
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
    /// <param name="onUnverified">
    ///     Called for each folder that could not be listed, each link that was not followed (a folder or a file) and each entry whose link status could not be
    ///     read; may be called concurrently from several threads.
    /// </param>
    /// <param name="cancellationToken">Stops the walk promptly; see the cancellation note on the type.</param>
    /// <returns>A task that completes when no folder is queued or being listed, or the walk was stopped.</returns>
    public static Task WalkAsync(IReadOnlyList<ScanFolder> folders,
                                 FileTypeFilter            filter,
                                 Action<ScannedFile>       onFile,
                                 Action<UnverifiedFolder>  onUnverified,
                                 CancellationToken         cancellationToken)
        => Task.Run(() => WalkAsyncCore(folders, filter, onFile, onUnverified, cancellationToken), CancellationToken.None);

    /// <summary>Makes an input folder's path canonical: a full path with exactly one trailing separator, as the walk and the index query both use it.</summary>
    /// <param name="path">The folder's path as entered.</param>
    /// <returns>The full path with a trailing directory separator.</returns>
    public static string NormaliseFolder(string path)
    {
        string full = Path.GetFullPath(path);

        return Path.EndsInDirectorySeparator(full) ? full : $"{full}{Path.DirectorySeparatorChar}";
    }

    /// <summary>Runs the walk on worker threads, after dropping the input folders that are links or whose link status cannot be read.</summary>
    /// <param name="folders">The input folders.</param>
    /// <param name="filter">Decides which files are reported, and under which file type.</param>
    /// <param name="onFile">Called for each file found.</param>
    /// <param name="onUnverified">Called for each folder or link that could not be checked.</param>
    /// <param name="cancellationToken">Stops the walk.</param>
    /// <returns>A task that completes when every worker has finished.</returns>
    private static async Task WalkAsyncCore(IReadOnlyList<ScanFolder> folders,
                                            FileTypeFilter            filter,
                                            Action<ScannedFile>       onFile,
                                            Action<UnverifiedFolder>  onUnverified,
                                            CancellationToken         cancellationToken)
    {
        InputRoot[] allPathRoots = BuildRoots(folders);

        if (allPathRoots is []
            || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // An input that is itself a link is not walked; it is reported, and the other inputs go on.
        List<InputRoot> pathRoots = [];

        foreach (InputRoot inputRoot in allPathRoots)
        {
            // Only a reparse point can be a link, so an ordinary input costs no extra probe, and a missing or unreachable one is left for its listing to classify. An input whose
            // link status cannot be read is not walked either: it is reported as unverified.
            if (!HasReparsePointAttribute(inputRoot.Path))
            {
                pathRoots.Add(inputRoot);

                continue;
            }

            LinkStates state = Classify(new DirectoryInfo(inputRoot.Path), out string? failure);

            if (IsOrdinary(state, inputRoot.Path, failure, onUnverified))
            {
                pathRoots.Add(inputRoot);
            }
        }

        if (pathRoots is [])
        {
            return;
        }

        using CancellationTokenSource linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Walk walk = new([.. pathRoots],
                        allPathRoots.Select(root => root.Path)
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                        filter,
                        onFile,
                        onUnverified,
                        linkedSource.Token);

        foreach (InputRoot root in pathRoots)
        {
            walk.Enqueue(root.Path);
        }

        // Dedicated threads: the listings block, and the thread pool would ramp up to 16 blocked threads slowly.
        // ReSharper disable AccessToDisposedClosure
        Task[] workers =
        [
            .. Enumerable.Range(start: 0, MaxConcurrentListings)
                         .Select(_ => Task.Factory.StartNew(() => RunWorker(walk, linkedSource),
                                                            CancellationToken.None,
                                                            TaskCreationOptions.LongRunning,
                                                            TaskScheduler.Default))
        ];

        // ReSharper restore AccessToDisposedClosure

        try
        {
            await Task.WhenAll(workers)
                      .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            // Every worker has finished by now (WhenAll waits for all of them, faulted or not).
            walk.Queue.Dispose();
        }
    }

    /// <summary>The loop of one worker: lists folders from the queue until the walk is over, stopped or failed.</summary>
    /// <param name="walk">The walk the worker takes its folders from.</param>
    /// <param name="linkedSource">Cancelled when a callback throws, so the other workers stop too.</param>
    private static void RunWorker(Walk walk, CancellationTokenSource linkedSource)
    {
        try
        {
            foreach (string folder in walk.Queue.GetConsumingEnumerable(walk.CancellationToken))
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
        catch (OperationCanceledException) when (walk.CancellationToken.IsCancellationRequested)
        {
            // Stopped by the caller, or because another worker failed (then that worker's exception is what the walk throws). An
            // OperationCanceledException from a callback while the token is not cancelled is not a stop: it falls through to the next catch.
        }
        catch
        {
            // A callback threw: stop the others, then let the exception surface from WalkAsync.
            linkedSource.Cancel();

            throw;
        }
    }

    /// <summary>Builds the distinct scan roots used to start a folder walk.</summary>
    /// <param name="folders">The configured folders to convert into walk roots.</param>
    /// <returns>The roots to walk, ordered from deepest to shallowest so containment checks select the most specific root first.</returns>
    private static InputRoot[] BuildRoots(IReadOnlyList<ScanFolder> folders)
    {
        Dictionary<string, InputRoot> byPath = new(StringComparer.OrdinalIgnoreCase);

        foreach (ScanFolder folder in folders)
        {
            string normalised = NormaliseFolder(folder.Path);

            // The same folder listed twice (pre-flight refuses it) is one input; it includes subfolders if either entry does.
            byPath[normalised] = byPath.TryGetValue(normalised, out InputRoot? existing)
                                     ? existing with
                                     {
                                         IncludeSubfolders = existing.IncludeSubfolders || folder.IncludeSubfolders
                                     }
                                     : new InputRoot(normalised, folder.Path, folder.IncludeSubfolders);
        }

        // Deepest first, so the first root that covers a folder is its owner.
        return [.. byPath.Values.OrderByDescending(root => root.Path.Length)];
    }

    /// <summary>
    ///     Tells whether an entry is a junction or symbolic link (it has a link target). Any other reparse point is an ordinary entry, but an entry whose link target cannot be read
    ///     is <see cref="LinkStates.Unknown" />, never assumed ordinary: the caller reports it and does not walk or check it.
    /// </summary>
    /// <param name="fileSystemInfo">The folder or file to classify.</param>
    /// <param name="failure">The system message when the link status could not be read; otherwise <see langword="null" />.</param>
    /// <returns>The entry's link status.</returns>
    private static LinkStates Classify(FileSystemInfo fileSystemInfo, out string? failure)
    {
        failure = null;

        try
        {
            return fileSystemInfo.LinkTarget is not null ? LinkStates.Link : LinkStates.Ordinary;
        }
        catch (Exception exception)
        {
            failure = exception.Message;

            return LinkStates.Unknown;
        }
    }

    /// <summary>Determines whether the specified path is, or should be treated as, a reparse point before walking it.</summary>
    /// <param name="path">The file-system path whose attributes are inspected.</param>
    /// <returns><see langword="true" /> if the path is marked as a reparse point or its link status cannot be determined; otherwise, <see langword="false" />.</returns>
    /// <remarks>
    ///     A folder whose attributes cannot be read is treated as ordinary here, so its listing reports the real reason (drive not found, unreachable, permission denied) rather than
    ///     a link failure.
    /// </remarks>
    private static bool HasReparsePointAttribute(string path)
    {
        try
        {
            return File.GetAttributes(path)
                       .HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or ArgumentException
                                                       or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Creates an unverified folder result for a path whose link status could not be determined.</summary>
    /// <param name="path">The folder path that could not be classified.</param>
    /// <param name="message">The optional system message describing why the classification could not be completed.</param>
    /// <returns>An <see cref="UnverifiedFolder" /> result with the reason set to <see cref="CheckOutcomes.UnverifiedOther" />, i.e. an unclassified link-status failure.</returns>
    private static UnverifiedFolder Unclassifiable(string path, string? message)
        => new(path, CheckOutcomes.UnverifiedOther, ReasonText: "Could not tell whether this is a link", message);

    /// <summary>Creates an unverified folder result for a link that was detected but not followed.</summary>
    /// <param name="path">The path of the link that was not followed.</param>
    /// <returns>An <see cref="UnverifiedFolder" /> record with the <see cref="CheckOutcomes.UnverifiedLinkNotFollowed" /> reason.</returns>
    private static UnverifiedFolder LinkNotFollowed(string path) => new(path, CheckOutcomes.UnverifiedLinkNotFollowed, ReasonText: null, SystemMessage: null);

    /// <summary>Reports a link, or an entry whose link status could not be read, as unverified; an ordinary entry is left for the caller to walk or report.</summary>
    /// <param name="state">The entry's link status.</param>
    /// <param name="path">The entry's path.</param>
    /// <param name="failure">The system message when the link status could not be read.</param>
    /// <param name="onUnverified">The callback that receives the unverified entry.</param>
    /// <returns><see langword="true" /> only for an ordinary entry, which the caller then walks or reports.</returns>
    private static bool IsOrdinary(LinkStates state, string path, string? failure, Action<UnverifiedFolder> onUnverified)
    {
        switch (state)
        {
            case LinkStates.Ordinary:
                return true;

            case LinkStates.Link:
                onUnverified(LinkNotFollowed(path));

                return false;

            case LinkStates.Unknown:
                onUnverified(Unclassifiable(path, failure));

                return false;

            default:
                throw new UnreachableException();
        }
    }

    /// <summary>
    ///     Describes the outcome of classifying a file-system entry before traversal, distinguishing entries that can be processed normally, entries that are links and must not be
    ///     followed, and entries whose link status could not be determined.
    /// </summary>
    private enum LinkStates
    {
        /// <summary>The entry is an ordinary file or folder, including any reparse point that is not a link.</summary>
        Ordinary,

        /// <summary>The entry is a junction or symbolic link, which is reported and not followed.</summary>
        Link,

        /// <summary>The entry's link status could not be read, so it is reported and neither walked nor checked.</summary>
        Unknown
    }

    /// <summary>An input folder: its normalised path, the path as entered, and its scope.</summary>
    /// <param name="Path">The folder's normalised path: a full path with one trailing separator.</param>
    /// <param name="Entered">The folder's path as the user entered it, which is how the report names it.</param>
    /// <param name="IncludeSubfolders">Whether the folders beneath it are covered too.</param>
    private sealed record InputRoot(string Path, string Entered, bool IncludeSubfolders);

    /// <summary>Represents one file-system entry discovered while listing a folder during a scan.</summary>
    /// <param name="Name">The entry name relative to the folder being listed.</param>
    /// <param name="IsDirectory">Indicates whether the entry is a directory.</param>
    /// <param name="IsReparsePoint">Indicates whether the entry has the reparse-point attribute and needs special handling before traversal or reporting.</param>
    private readonly record struct Entry(string Name, bool IsDirectory, bool IsReparsePoint);

    /// <summary>Carries the mutable state for a single folder-walking operation.</summary>
    /// <remarks>
    ///     Maintains the pending folder queue, cancellation token, input-root metadata, file-type filter, and reporting callbacks used while traversing the resolved set of input
    ///     folders. The walk enqueues folders that still need to be listed, reports matching files through the scanned-file callback, and reports folders or links that cannot be verified
    ///     through the unverified-folder callback.
    /// </remarks>
    private sealed class Walk
    {
        /// <summary>Decides which files are reported, and under which file type.</summary>
        private readonly FileTypeFilter _filter;

        /// <summary>The normalised paths of every input folder, so a subfolder that is itself an input is not queued a second time.</summary>
        private readonly HashSet<string> _inputPaths;

        /// <summary>Called for each file found.</summary>
        private readonly Action<ScannedFile> _onFile;

        /// <summary>Called for each folder that could not be listed and each link that was not followed.</summary>
        private readonly Action<UnverifiedFolder> _onUnverified;

        /// <summary>The input folders that are walked, deepest first.</summary>
        private readonly InputRoot[] _roots;

        /// <summary>The number of folders queued or being listed; the walk is over when it reaches zero.</summary>
        private int _pending;

        /// <summary>Initialises a new walk.</summary>
        /// <param name="roots">The input folders that are walked, deepest first.</param>
        /// <param name="inputPaths">The normalised paths of every input folder, including those that are links and so not walked.</param>
        /// <param name="filter">Decides which files are reported, and under which file type.</param>
        /// <param name="onFile">Called for each file found.</param>
        /// <param name="onUnverified">Called for each folder that could not be listed and each link that was not followed.</param>
        /// <param name="cancellationToken">Stops the walk.</param>
        public Walk(InputRoot[]              roots,
                    HashSet<string>          inputPaths,
                    FileTypeFilter           filter,
                    Action<ScannedFile>      onFile,
                    Action<UnverifiedFolder> onUnverified,
                    CancellationToken        cancellationToken)
        {
            _roots            = roots;
            _inputPaths       = inputPaths;
            _filter           = filter;
            _onFile           = onFile;
            _onUnverified     = onUnverified;
            CancellationToken = cancellationToken;
        }

        /// <summary>Gets the token that stops the walk.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>Gets the folders waiting to be listed.</summary>
        public BlockingCollection<string> Queue { get; } = new();

        /// <summary>Queues a folder; the walk is over when every queued folder has finished.</summary>
        /// <param name="folder">The normalised path of the folder, with a trailing separator.</param>
        public void Enqueue(string folder)
        {
            Interlocked.Increment(ref _pending);

            // The queue is unbounded, so Add never blocks; a token could throw before the workers start and leave _pending counted for an unqueued folder.
            // ReSharper disable once MethodSupportsCancellation
            Queue.Add(folder);
        }

        /// <summary>Marks one queued folder as done; the last one closes the queue, which ends every worker.</summary>
        public void Finished()
        {
            if (Interlocked.Decrement(ref _pending) is 0)
            {
                Queue.CompleteAdding();
            }
        }

        /// <summary>Lists one folder: reports its matching files, and queues its subfolders when any input that covers it includes subfolders.</summary>
        /// <param name="folder">The normalised path of the folder, with a trailing separator.</param>
        public void List(string folder)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }

            InputRoot? owner   = null;
            bool       descend = false;

            foreach (InputRoot root in _roots)
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

            List<Entry> entries = [];

            try
            {
                // Only the enumeration is guarded: a failure in the filter, or a callback is a bug, not an unreadable folder.
                FileSystemEnumerable<Entry> listing = new(folder, ToEntry, ListingOptions);

                foreach (Entry listed in listing)
                {
                    CancellationToken.ThrowIfCancellationRequested();

                    entries.Add(listed);
                }
            }
            catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                MappedOutcome mapped = Win32ErrorMapper.Map(exception);

                // A folder that vanished mid-run is not a "file missing" outcome; it is a folder that could not be checked.
                _onUnverified(mapped.Outcome is CheckOutcomes.MissingDoesNotExist
                                  ? new UnverifiedFolder(folder, CheckOutcomes.UnverifiedOther, ReasonText: "Folder not found", exception.Message)
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

                // A file that is a link is reported, not checked; one whose status cannot be read is reported too. Only a matching reparse point is probed.
                string file = $"{folder}{entry.Name}";

                string? failure = null;

                LinkStates state = entry.IsReparsePoint ? Classify(new FileInfo(file), out failure) : LinkStates.Ordinary;

                if (IsOrdinary(state, file, failure, _onUnverified))
                {
                    _onFile(new ScannedFile(file, owner.Entered, type));
                }
            }
        }

        /// <summary>Queues a subfolder, unless it is itself an input or is a link, which is reported instead.</summary>
        /// <param name="folder">The normalised path of the folder being listed.</param>
        /// <param name="subfolder">The subfolder's entry.</param>
        private void Descend(string folder, Entry subfolder)
        {
            string path = $"{folder}{subfolder.Name}{Path.DirectorySeparatorChar}";

            // An input is queued, or reported if it is a link, on its own: here it would be a second visit.
            if (_inputPaths.Contains(path))
            {
                return;
            }

            // A folder's status is probed only for a reparse point, and before the folder is queued; one that cannot be classified is not walked.
            string? failure = null;

            LinkStates state = subfolder.IsReparsePoint ? Classify(new DirectoryInfo(path), out failure) : LinkStates.Ordinary;

            if (IsOrdinary(state, path, failure, _onUnverified))
            {
                Enqueue(path);
            }
        }

        /// <summary>Converts a raw file-system entry into an <see cref="Entry" />.</summary>
        /// <param name="entry">The entry the enumeration is positioned on.</param>
        /// <returns>The entry's name, whether it is a directory and whether it is a reparse point.</returns>
        private static Entry ToEntry(ref FileSystemEntry entry)
            => new(entry.FileName.ToString(),
                   entry.IsDirectory,
                   (entry.Attributes & FileAttributes.ReparsePoint) is not 0);

        /// <summary>Determines whether an input folder covers a folder: the folder is the input itself, or is beneath it and the input includes subfolders.</summary>
        /// <param name="inputRoot">The input folder.</param>
        /// <param name="folder">The normalised path of the folder, with a trailing separator.</param>
        /// <returns><see langword="true" /> when the input covers the folder.</returns>
        private static bool Covers(InputRoot inputRoot, string folder)
            => inputRoot.Path.Equals(folder, StringComparison.OrdinalIgnoreCase)
               || (inputRoot.IncludeSubfolders
                   && folder.StartsWith(inputRoot.Path, StringComparison.OrdinalIgnoreCase));
    }
}
