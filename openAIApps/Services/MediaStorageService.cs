using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace openAIApps.Services
{
    public class MediaStorageService
    {
        private string _imagesFolder = string.Empty;

        public MediaStorageService()
        {
        }

        public MediaStorageService(string imagesFolder)
        {
            SetImagesFolder(imagesFolder);
        }

        public void SetImagesFolder(string imagesFolder)
        {
            _imagesFolder = imagesFolder ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(_imagesFolder))
            {
                Directory.CreateDirectory(_imagesFolder);
            }
        }

        public string EnsureImagesFolder()
        {
            if (string.IsNullOrWhiteSpace(_imagesFolder))
            {
                _imagesFolder = Path.Combine(AppContext.BaseDirectory, "Images");
            }

            Directory.CreateDirectory(_imagesFolder);
            return _imagesFolder;
        }

        public List<string> SaveAssistantImages(IEnumerable<string> payloads, string outputFormat = "png")
        {
            var savedPaths = new List<string>();

            if (payloads == null)
                return savedPaths;

            string folder = EnsureImagesFolder();
            string normalizedFormat = NormalizeImageFormat(outputFormat);
            string extension = GetExtensionForImageFormat(normalizedFormat);

            foreach (var payload in payloads)
            {
                if (string.IsNullOrWhiteSpace(payload))
                    continue;

                string filePath = null;

                try
                {
                    if (payload.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    {
                        int commaIndex = payload.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            string meta = payload.Substring(0, commaIndex);
                            string b64 = payload.Substring(commaIndex + 1);

                            string ext = ".png";
                            if (meta.Contains("jpeg", StringComparison.OrdinalIgnoreCase))
                                ext = ".jpg";
                            else if (meta.Contains("webp", StringComparison.OrdinalIgnoreCase))
                                ext = ".webp";
                            else if (meta.Contains("png", StringComparison.OrdinalIgnoreCase))
                                ext = ".png";

                            byte[] bytes = Convert.FromBase64String(b64);
                            string name = $"resp_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{ext}";
                            filePath = Path.Combine(folder, name);
                            File.WriteAllBytes(filePath, bytes);
                        }
                    }
                    else if (payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        // Leave URL download support for later.
                    }
                    else
                    {
                        byte[] bytes = Convert.FromBase64String(payload);
                        string name = $"resp_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{extension}";
                        filePath = Path.Combine(folder, name);
                        File.WriteAllBytes(filePath, bytes);
                    }

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        savedPaths.Add(filePath);
                    }
                }
                catch
                {
                    // Ignore individual bad payloads for now.
                }
            }

            return savedPaths;
        }

        private static string NormalizeImageFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return "png";

            return format.Trim().ToLowerInvariant() switch
            {
                "jpg" => "jpeg",
                "jpeg" => "jpeg",
                "png" => "png",
                "webp" => "webp",
                _ => "png"
            };
        }

        private static string GetExtensionForImageFormat(string format)
        {
            return format switch
            {
                "jpeg" => ".jpg",
                "webp" => ".webp",
                _ => ".png"
            };
        }

        public void DeleteFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
                return;

            foreach (string path in filePaths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                    // Keep deletion tolerant for now.
                }
            }
        }
        public string ImportUserImage(string sourceFilePath)
        {
            return ImportUserFile(sourceFilePath);
        }
        private static string MakeSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "attachment.bin";

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        public string ImportUserFile(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return null;

            try
            {
                string folder = EnsureImagesFolder();
                string originalName = MakeSafeFileName(Path.GetFileName(sourceFilePath));
                string destinationPath = Path.Combine(
                    folder,
                    $"user_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{originalName}");

                File.Copy(sourceFilePath, destinationPath, overwrite: false);
                return destinationPath;
            }
            catch
            {
                return null;
            }
        }
    }
}