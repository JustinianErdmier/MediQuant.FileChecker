Hey team, hope you all had a great weekend! 👋

I spent a bit of mine building a new tool for us called **File Auditor**, inspired by Ron’s File Checker. It’s at `<add path>`, and there’s a full wiki article at `<add link>`, but
here’s the quick rundown:

**Yes, it looks like Windows 95**  
When you first open it, you’ll notice it looks like an old Win9x app. I promise that’s completely intentional and purely cosmetic! Under the hood, it’s built on the latest version
of .NET and fully optimised. I just really like the old Win9x look and enjoy the nostalgia. 😄

**What’s different from File Checker**

1. **No SQL.** Just type in the table and column names, and File Auditor builds the connection string and its queries for you.
2. **The ID column is now optional.** File Checker needs one, but all it does is print it next to the filepath in the report. I’ve had to add an ID column to an index table more
   than once just to use the tool, which always felt pointless and a waste of time. I'll admit that a small number is nicer to look at, but searching the table by file path works
   just as well. As a bonus, this also sidesteps a File Checker bug: if you start a check with a bad query (e.g. no ID column, or the columns the wrong way round), it crashes into
   an unhandled exception and locks up. `Stop` does nothing, so you’re stuck killing the process and relaunching.
3. **Much better reports.** They’re now detailed plain-text reports with useful info in them:
    - Handy stats at the top, like audit time and total files.
    - A clear “no missing files” message instead of an empty table.
    - A heads-up when files are only flagged because the empty-files option is on.
    - Results grouped and sorted sensibly, rather than one big tab-separated table, so they’re easy to work through.
    - A new **Unverified** section. File Checker quietly marked files as missing if it hit a network, permissions, or other I/O error. File Auditor lists those separately, so you
      know what actually needs a second look.
4. **Standardised report names and locations.** These are built from your project stage and the SVN convention Logan established for our team. Just pop in your project folder name
   (e.g. `D12345`), pick your stages, and it sorts out the path and file name for you. If you’ve got an odd case, or don’t use the standard convention, you can set a custom path
   and file name instead.
5. **Settings saved per project.** File Checker saved its settings next to the executable on the shared drive, so we were all sharing (and overwriting) the same ones. File Auditor
   saves them per project alongside your reports, so you can check them into SVN and pick up right where you left off. Say you audit one project against the interim server, hop
   over to another project, then come back to audit the first one against production: just load your settings and go. No reentering everything, and no stepping on anyone else’s
   toes or losing your settings for other projects.

**The big new thing: Unindexed Files audit**  
This isn’t just a shiny rewrite, though; File Auditor can audit in both directions! File Checker only checks that the paths in your index table exist on disk. Going the other way,
checking what’s on disk against the table, meant picking one of two workarounds:

1. Counting the files on disk (File Explorer, TreeSize, or a custom script) and comparing that to the table’s row count. Not always reliable, especially when the files are mixed in
   with other stuff, like when a client dumps an entire drive on us.
2. Building a temporary index table from the files on disk (with FIG (my modern File Index Generator rewrite), Ron’s original File Index Generator, or a custom reverse-index
   script) and comparing the two tables in SQL. Slow, manual, and you throw it all away afterwards.

File Auditor now handles this for you with hardly any extra effort, and it’s quick; there are many optimisations under the hood to keep folder scanning snappy.

To use it, all you have to do is add your input folders and, if you want, whitelist and/or blacklist file types by extension. That’s it! Both lists support `*` and `?` wildcards
and multi-dot extensions, and there’s a default whitelist of common filetypes, so hopefully you won’t need to set anything up at all.

Give it a go and let me know what you think. Any feedback, bug reports, or feature ideas are very welcome! 🙂