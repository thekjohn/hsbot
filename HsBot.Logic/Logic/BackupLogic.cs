using System.IO.Compression;

namespace HsBot.Logic;

public static class BackupLogic
{
    public static async Task UploadBackupToChannel(SocketGuild guild, ISocketMessageChannel channel)
    {
        var now = DateTime.UtcNow;
        var fileName = Path.Combine(Path.GetTempPath(), "jarvis_backup_" + now.ToString("yyyy_MM_dd_HH_mm_ss_fff", CultureInfo.InvariantCulture) + ".zip");
        try
        {
            ZipFile.CreateFromDirectory(StateService.Folder, fileName, CompressionLevel.Optimal, true, Encoding.UTF8);
        }
        catch (Exception)
        {
            return;
        }

        await channel.SendFileAsync(fileName, "jarvis backup");
    }
}
