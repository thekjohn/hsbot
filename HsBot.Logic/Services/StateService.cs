namespace HsBot.Logic
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;

    internal static class StateService
    {
        public static string Folder { get; }
        private static readonly object _lock = new();

        static StateService()
        {
            //Folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), ".."));
            Folder = @"c:\HsBot";
        }

        public static string[] ListIds(ulong guildId, string idPrefix)
        {
            lock (_lock)
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
        }

        public static T Get<T>(ulong guildId, string id)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                if (File.Exists(fn))
                {
                    var content = File.ReadAllText(fn);
                    return JsonSerializer.Deserialize<T>(content);
                }
            }

            return default;
        }

        public static List<T> GetList<T>(ulong guildId, string id)
        {
            var result = new List<T>();
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                if (File.Exists(fn))
                {
                    var lines = File.ReadAllLines(fn);
                    foreach (var content in lines)
                    {
                        result.Add(JsonSerializer.Deserialize<T>(content));
                    }
                }
            }

            return result;
        }

        private static string GetFileName(ulong guildId, string id)
        {
            var folder = Path.Combine(Folder, guildId.ToStr());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, "state-" + id + ".txt");
        }

        public static void Set<T>(ulong guildId, string id, T value)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                var content = JsonSerializer.Serialize(value);
                File.WriteAllText(fn, content);
            }
        }

        public static void AppendToList<T>(ulong guildId, string id, params T[] values)
        {
            if (values.Length == 0)
                return;

            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                if (values.Length > 1)
                {
                    var sb = new StringBuilder();
                    foreach (var value in values)
                    {
                        var content = JsonSerializer.Serialize(value);
                        sb.Append(content);
                    }
                    File.AppendAllText(fn, sb.ToString() + "\n");
                }
                else
                {
                    var content = JsonSerializer.Serialize(values[0]);
                    File.AppendAllText(fn, content + "\n");
                }
            }
        }

        public static void Delete(ulong guildId, string id)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                if (File.Exists(fn))
                    File.Delete(fn);
            }
        }

        public static void Rename(ulong guildId, string oldId, string newId)
        {
            lock (_lock)
            {
                var oldFn = GetFileName(guildId, oldId);
                if (File.Exists(oldFn))
                {
                    var newFn = GetFileName(guildId, newId);
                    File.Move(oldFn, newFn, true);
                }
            }
        }

        public static bool Exists(ulong guildId, string id)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                return File.Exists(fn);
            }
        }

        public static string GetId(string prefix, params ulong[] parts)
        {
            return (prefix != null ? prefix + "-" : string.Empty)
                + string.Join('-', parts.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
        }
    }
}