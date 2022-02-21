namespace HsBot.Logic
{
    using System.Globalization;
    using System.Text.Json;

    internal class StateService
    {
        public string Folder { get; }

        public StateService()
        {
            //Folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), ".."));
            Folder = @"c:\HsBot";
        }

        public string[] ListIds(ulong guildId, string idPrefix)
        {
            var folder = Path.Combine(Folder, guildId.ToStr());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var prefix = "state-";
            var prefixLength = prefix.Length;

            return Directory
                .EnumerateFiles(folder, prefix + idPrefix + "*.txt")
                .Select(fn => Path.GetFileNameWithoutExtension(fn)[prefixLength..])
                .ToArray();
        }

        public T Get<T>(ulong guildId, string id)
        {
            var fn = GetFileName(guildId, id);
            if (File.Exists(fn))
            {
                var content = File.ReadAllText(fn);
                return JsonSerializer.Deserialize<T>(content);
            }

            return default;
        }

        private string GetFileName(ulong guildId, string id)
        {
            var folder = Path.Combine(Folder, guildId.ToStr());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, "state-" + id + ".txt");
        }

        public void Set<T>(ulong guildId, string id, T value)
        {
            var fn = GetFileName(guildId, id);
            var content = JsonSerializer.Serialize(value);
            File.WriteAllText(fn, content);
        }

        public void Delete(ulong guildId, string id)
        {
            var fn = GetFileName(guildId, id);
            if (File.Exists(fn))
                File.Delete(fn);
        }

        public bool Exists(ulong guildId, string id)
        {
            var fn = GetFileName(guildId, id);
            return File.Exists(fn);
        }

        public string GetId(string prefix, params ulong[] parts)
        {
            return (prefix != null ? prefix + "-" : string.Empty)
                + string.Join('.', parts.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
        }
    }
}