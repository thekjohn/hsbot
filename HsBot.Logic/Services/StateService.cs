﻿namespace HsBot.Logic
{
    using System.Globalization;
    using System.Text;
    using System.Text.Json;

    internal class StateService
    {
        public string Folder { get; }
        private readonly object _lock = new();

        public StateService()
        {
            //Folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Uri.UnescapeDataString(new Uri(Assembly.GetExecutingAssembly().Location).AbsolutePath)), ".."));
            Folder = @"c:\HsBot";
        }

        public string[] ListIds(ulong guildId, string idPrefix)
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

        public T Get<T>(ulong guildId, string id)
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

        public List<T> GetList<T>(ulong guildId, string id)
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

        private string GetFileName(ulong guildId, string id)
        {
            var folder = Path.Combine(Folder, guildId.ToStr());
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, "state-" + id + ".txt");
        }

        public void Set<T>(ulong guildId, string id, T value)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                var content = JsonSerializer.Serialize(value);
                File.WriteAllText(fn, content);
            }
        }

        public void AppendToList<T>(ulong guildId, string id, params T[] values)
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

        public void Delete(ulong guildId, string id)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                if (File.Exists(fn))
                    File.Delete(fn);
            }
        }

        public bool Exists(ulong guildId, string id)
        {
            lock (_lock)
            {
                var fn = GetFileName(guildId, id);
                return File.Exists(fn);
            }
        }

        public string GetId(string prefix, params ulong[] parts)
        {
            return (prefix != null ? prefix + "-" : string.Empty)
                + string.Join('.', parts.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
        }
    }
}