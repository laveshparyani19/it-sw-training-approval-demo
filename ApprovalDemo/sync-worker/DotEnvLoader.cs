namespace ApprovalDemo.SyncWorker;

/// <summary>
/// Loads KEY=VALUE lines from the first .env file found (dotnet does not load .env by default).
/// </summary>
internal static class DotEnvLoader
{
    /// <summary>
    /// Tries repo-relative paths so running from "IT SW Training" or "sync-worker/bin/Debug/net10.0" both work.
    /// </summary>
    public static bool TryLoad(out string? loadedPath)
    {
        foreach (var path in GetCandidatePaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            LoadFile(path);
            loadedPath = path;
            return true;
        }

        loadedPath = null;
        return false;
    }

    private static IEnumerable<string> GetCandidatePaths()
    {
        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, ".env");
        yield return Path.Combine(cwd, "ApprovalDemo", ".env");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && dir != null; i++)
        {
            yield return Path.Combine(dir.FullName, "ApprovalDemo", ".env");
            yield return Path.Combine(dir.FullName, ".env");
            dir = dir.Parent;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line[7..].TrimStart();
            }

            // PowerShell: $env:NAME = 'value'
            if (line.StartsWith("$env:", StringComparison.OrdinalIgnoreCase))
            {
                line = line[5..].TrimStart();
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            var value = line[(eq + 1)..].Trim();
            // Strip trailing inline comment (unquoted only)
            var hash = value.IndexOf('#');
            if (hash >= 0 && !IsInsideQuotes(value, hash))
            {
                value = value[..hash].TrimEnd();
            }

            if (value.Length >= 2
                && ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))
                    || (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static bool IsInsideQuotes(string s, int index)
    {
        var single = 0;
        var dbl = 0;
        for (var i = 0; i < index; i++)
        {
            if (s[i] == '\'' && (i == 0 || s[i - 1] != '\\'))
            {
                single ^= 1;
            }
            else if (s[i] == '"' && (i == 0 || s[i - 1] != '\\'))
            {
                dbl ^= 1;
            }
        }

        return (single & 1) != 0 || (dbl & 1) != 0;
    }
}
