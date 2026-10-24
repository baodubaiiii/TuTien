using System.Security.Cryptography;
using System.Text.Json;

namespace TuTien.Tools.ContentBuilder
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            string source = Path.Combine(root, "content", "source", "base", "catalog.json");
            byte[] data = File.ReadAllBytes(source);
            using var catalog = JsonDocument.Parse(data);
            if (catalog.RootElement.GetProperty("version").GetString() != "base")
            {
                throw new InvalidDataException("Invalid base catalog version");
            }

            string hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            string name = "catalog." + hash[..16] + ".json";
            string output = Path.Combine(root, "content", "published", "base");
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, name), data);
            var manifest = new
            {
                version = "base",
                files = new[]
                {
                    new
                    {
                        key = "catalog",
                        path = name,
                        sha256 = hash,
                        size = data.Length
                    }
                }
            };
            var json = new JsonSerializerOptions { WriteIndented = true };
            string path = Path.Combine(output, "manifest.json");
            File.WriteAllBytes(path + ".tmp", JsonSerializer.SerializeToUtf8Bytes(manifest, json));
            File.Move(path + ".tmp", path, true);
            Console.WriteLine($"Base CDN catalog published: {output}");
        }
    }
}
