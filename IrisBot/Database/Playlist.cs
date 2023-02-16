using System.Text;
using IrisBot.Interfaces;
using Lavalink4NET.Player;
using IrisBot.Enums;

namespace IrisBot.Database
{
    public class Playlist : IPlaylist
    {
        public ulong GuildId { get; }
        public string Name { get; set; }

        public Playlist(ulong guildId, string name)
        {
            GuildId = guildId;
            Name = name;
        }

        /// <summary>
        /// 현재 트랙을 포함한 대기열 전체를 플레이리스트로 저장한다. 이미 존재할 경우 덮어 씌운다.
        /// </summary>
        /// <param name="list">플레이리스트 정보</param>
        /// <param name="currentTrack">현재 트랙</param>
        /// <param name="queue">대기열</param>
        /// <returns>플레이리스트 생성 결과</returns>
        public static async Task<PlaylistResult> CreatePlaylistAsync(Playlist list, LavalinkTrack? currentTrack, LavalinkQueue? queue)
        {
            try
            {
                string directoryPath = Path.Combine(Program.PlaylistDirectory, list.GuildId.ToString());
                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                string path = Path.Combine(directoryPath, list.Name);

                DirectoryInfo di = new DirectoryInfo(directoryPath);
                bool isExists = false; // 이미 존재하는 이름일 경우 덮어씌움을 알리기 위해 사용한다.
                if (File.Exists(path))
                    isExists = true;
                else if (di.GetFiles().Length >= Program.MaxPlaylistCount)
                    return PlaylistResult.CreationLimit;
                else if (currentTrack != null && queue != null)
                    throw new ArgumentNullException();
                    

                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true))
                {
                    if (currentTrack != null)
                    {
                        byte[] buffer = Encoding.UTF8.GetBytes(currentTrack.Uri + Environment.NewLine);
                        await fs.WriteAsync(buffer, 0, buffer.Length);
                    }
                        
                    if (queue != null)
                    {
                        foreach (var item in queue)
                        {
                            byte[] buffer = Encoding.UTF8.GetBytes(item.Uri + Environment.NewLine);
                            await fs.WriteAsync(buffer, 0, buffer.Length);
                        }
                    }
                }

                if (isExists)
                    return PlaylistResult.Overwrite;
                else
                    return PlaylistResult.New;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                return PlaylistResult.Fail;
            }
        }

        /// <summary>
        /// 플레이리스트를 삭제한다
        /// </summary>
        /// <param name="guildId"></param>
        /// <param name="name">삭제할 플레이리스트 이름</param>
        /// <returns>삭제 결과</returns>
        public static async Task<PlaylistDeleteResult> DeletePlaylistAsync(ulong guildId, string name)
        {
            try
            {
                string path = Path.Combine(Program.PlaylistDirectory, guildId.ToString(), name);
                if (!File.Exists(path))
                    return PlaylistDeleteResult.NotExists;

                await Task.Run(() =>
                {
                    File.Delete(path);
                });
                return PlaylistDeleteResult.Success;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                return PlaylistDeleteResult.Fail;
            }
        }

        /// <summary>
        /// 플레이리스트 전체를 삭제한다
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>true false</returns>
        public static async Task<bool> ClearPlaylistAsync(ulong guildId)
        {
            try
            {
                string path = Path.Combine(Program.PlaylistDirectory, guildId.ToString());
                DirectoryInfo di = new DirectoryInfo(path);
                if (!di.Exists)
                    return false;

                await Task.Run(() =>
                {
                    var files = di.GetFiles();
                    foreach (var file in files)
                        file.Delete();
                });
                return true;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                return false;
            }
        }

        /// <summary>
        /// 지정한 이름의 플레이리스트를 불러온다
        /// </summary>
        /// <param name="guildId"></param>
        /// <param name="name">불러올 플레이리스트 이름</param>
        /// <returns>플레이리스트 파일 내의 음원 링크 배열</returns>
        public static async Task<string[]?> LoadPlaylistAsync(ulong guildId, string name)
        {
            try
            {
                string path = Path.Combine(Program.PlaylistDirectory, guildId.ToString(), name);
                if (!File.Exists(path))
                    return null;

                return await File.ReadAllLinesAsync(path);
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                return null;
            }
        }
    }
}
