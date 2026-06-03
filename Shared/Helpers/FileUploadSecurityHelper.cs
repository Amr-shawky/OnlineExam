using Microsoft.AspNetCore.Http;

namespace OnlineExam.Shared.Helpers
{
    public static class FileUploadSecurityHelper
    {
        private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        private static readonly Dictionary<string, string[]> AllowedContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { "image/jpeg", "image/pjpeg" },
            [".jpeg"] = new[] { "image/jpeg", "image/pjpeg" },
            [".png"] = new[] { "image/png" },
            [".gif"] = new[] { "image/gif" },
            [".webp"] = new[] { "image/webp" }
        };

        public static bool TryValidateImage(IFormFile file, long maxBytes, out string extension, out string errorMessage)
        {
            extension = string.Empty;
            errorMessage = string.Empty;

            if (file == null || file.Length <= 0)
            {
                errorMessage = "Icon file is required";
                return false;
            }

            if (file.Length > maxBytes)
            {
                errorMessage = $"Icon file size must be less than {maxBytes / (1024 * 1024)}MB";
                return false;
            }

            extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedImageExtensions.Contains(extension))
            {
                errorMessage = "Only image files are allowed (jpg, jpeg, png, gif, webp)";
                return false;
            }

            var contentType = file.ContentType?.Trim().ToLowerInvariant() ?? string.Empty;
            if (!AllowedContentTypesByExtension.TryGetValue(extension, out var allowedTypes) ||
                !allowedTypes.Contains(contentType))
            {
                errorMessage = "Invalid file content type";
                return false;
            }

            if (!HasValidSignature(file, extension))
            {
                errorMessage = "File content does not match its extension";
                return false;
            }

            return true;
        }

        public static string CreateSafeFileName(string extension)
            => $"{Guid.NewGuid():N}{extension}";

        public static string BuildSafePath(string rootDirectory, string fileName)
        {
            var root = Path.GetFullPath(rootDirectory);
            var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, fileName));

            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Invalid file path");
            }

            return fullPath;
        }

        private static bool HasValidSignature(IFormFile file, string extension)
        {
            using var stream = file.OpenReadStream();
            Span<byte> header = stackalloc byte[12];
            var read = stream.Read(header);

            return extension switch
            {
                ".jpg" or ".jpeg" => read >= 3 &&
                                     header[0] == 0xFF &&
                                     header[1] == 0xD8 &&
                                     header[2] == 0xFF,
                ".png" => read >= 8 &&
                          header[0] == 0x89 &&
                          header[1] == 0x50 &&
                          header[2] == 0x4E &&
                          header[3] == 0x47 &&
                          header[4] == 0x0D &&
                          header[5] == 0x0A &&
                          header[6] == 0x1A &&
                          header[7] == 0x0A,
                ".gif" => read >= 4 &&
                          header[0] == 0x47 &&
                          header[1] == 0x49 &&
                          header[2] == 0x46 &&
                          header[3] == 0x38,
                ".webp" => read >= 12 &&
                           header[0] == 0x52 &&
                           header[1] == 0x49 &&
                           header[2] == 0x46 &&
                           header[3] == 0x46 &&
                           header[8] == 0x57 &&
                           header[9] == 0x45 &&
                           header[10] == 0x42 &&
                           header[11] == 0x50,
                _ => false
            };
        }
    }
}
