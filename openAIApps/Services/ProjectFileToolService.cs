using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace openAIApps.Services
{
    public sealed class ProjectFileToolService
    {
        private readonly DeveloperToolsOptions _options;
        private readonly WorkspaceGuard _guard;

        public ProjectFileToolService(DeveloperToolsOptions options, WorkspaceGuard guard)
        {
            _options = options;
            _guard = guard;
        }

        public string ReadProjectFile(string path, int? startLine, int? endLine)
        {
            try
            {
                var fullPath = _guard.ResolveRelativeFile(path);

                var info = new FileInfo(fullPath);
                if (!info.Exists)
                    throw new FileNotFoundException("File not found.", path);

                if (info.Length > _options.MaxFileBytes)
                    throw new InvalidOperationException($"File exceeds maximum allowed size of {_options.MaxFileBytes} bytes.");

                var lines = File.ReadAllLines(fullPath);

                int start = Math.Max(1, startLine ?? 1);
                int end = Math.Min(lines.Length, endLine ?? Math.Min(lines.Length, start + _options.MaxReadLines - 1));

                if (end < start)
                    throw new InvalidOperationException("end_line must be greater than or equal to start_line.");

                if ((end - start + 1) > _options.MaxReadLines)
                    end = start + _options.MaxReadLines - 1;

                var selected = new List<string>();
                for (int i = start; i <= end; i++)
                {
                    selected.Add($"{i}: {lines[i - 1]}");
                }

                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    path,
                    start_line = start,
                    end_line = end,
                    content = string.Join(Environment.NewLine, selected)
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = ex.Message
                }, JsonOptions);
            }
        }

        public string ListProjectFiles(string subpath, string glob, int? maxResults)
        {
            try
            {
                var root = _guard.ResolveRelativeDirectory(subpath);
                var pattern = string.IsNullOrWhiteSpace(glob) ? "*.*" : glob;
                var limit = Math.Clamp(maxResults ?? 100, 1, 500);

                var files = Directory
                    .EnumerateFiles(root, pattern, SearchOption.AllDirectories)
                    .Where(f => _options.AllowedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .Select(_guard.ToRelativePath)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)
                    .ToList();

                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    files
                }, JsonOptions);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = ex.Message
                }, JsonOptions);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }
}