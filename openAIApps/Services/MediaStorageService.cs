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

        public List<string> SaveAssistantImages(IEnumerable<string> payloads)
        {
            var savedPaths = new List<string>();

            if (payloads == null)
                return savedPaths;

            string folder = EnsureImagesFolder();

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

                            byte[] bytes = Convert.FromBase64String(b64);
                            string name = $"resp_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{ext}";
                            filePath = Path.Combine(folder, name);
                            File.WriteAllBytes(filePath, bytes);
                        }
                    }
                    else if (payload.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             payload.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        // Leave URL download support for a later phase.
                    }
                    else
                    {
                        byte[] bytes = Convert.FromBase64String(payload);
                        string name = $"resp_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
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
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return null;

            try
            {
                string folder = EnsureImagesFolder();
                string extension = Path.GetExtension(sourceFilePath);

                if (string.IsNullOrWhiteSpace(extension))
                    extension = ".png";

                string fileName = $"user_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}{extension}";
                string destinationPath = Path.Combine(folder, fileName);

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