using Discord;
using IrisBot.Enums;
using IrisBot.Interfaces;
using Lavalink4NET.Rest;
using System;
using System.Data.SQLite;
using System.Security.Cryptography.X509Certificates;

namespace IrisBot.Database
{
    public class GuildSettings : IGuildSettings
    {
        public ulong GuildId { get; }
        public float PlayerVolume { get; set; }
        public ulong? ListMessagdId { get; set; }
        public Translations Language { get; set; }
        public SearchMode SearchPlatform { get; set; }

        private static List<GuildSettings>? GuildsList;

        public GuildSettings(ulong guildId, float playerVolume)
        {
            GuildId = guildId;
            PlayerVolume = playerVolume;
            ListMessagdId = null;
            Language = Translations.English;
            SearchPlatform = SearchMode.YouTube;
        }

        public GuildSettings(ulong guildId, float playerVolume, Translations language, SearchMode searchMode)
        {
            GuildId = guildId;
            PlayerVolume = playerVolume;
            ListMessagdId = null;
            Language = language;
            SearchPlatform = searchMode;
        }

        public static List<GuildSettings> GetGuildsList()
        {
            if (GuildsList == null)
                GuildsList = new List<GuildSettings>();

            return GuildsList;
        }

        /// <summary>
        /// DB 파일이 없는 상태에서 실행시 테이블 생성을 하고 DB에서 값을 불러와 List<GuildSettings>에 저장한다
        /// </summary>
        /// <returns>void</returns>
        public static async Task InitializeAsync()
        {
            try
            {
                string connStr = @"DataSource=.\GuildSettings.db";
                string paths = AppDomain.CurrentDomain.BaseDirectory;
                AppDomain.CurrentDomain.SetData("DataDirectory", paths);

                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();
                    // 테이블이 있는지 없는지 확인한다.
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE NAME='Guilds'", conn))
                    {
                        var tableCount = await cmd.ExecuteScalarAsync();
                        if (tableCount == null || (long)tableCount == 0)
                        {
                            using (var createTable = new SQLiteCommand("CREATE TABLE Guilds(ID TEXT PRIMARY KEY, VOLUME REAL, LANG INTEGER, SEARCHMODE INTEGER)", conn))
                            {
                                CustomLog.PrintLog(LogSeverity.Warning, "Database", "Table \"Guilds\" not exists. Creating new one.");
                                await createTable.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    using (var cmd = new SQLiteCommand("SELECT * FROM Guilds", conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            GetGuildsList().Add(new GuildSettings(
                                Convert.ToUInt64(reader["ID"]), Convert.ToSingle(reader["VOLUME"]), 
                                (Translations)Convert.ToInt32(reader["LANG"]), (SearchMode)Convert.ToInt32(reader["SEARCHMODE"])));

                            CustomLog.PrintLog(LogSeverity.Info, "Database", 
                                $"Load success (GuildId: {Convert.ToUInt64(reader["ID"])}, Language: {(Translations)Convert.ToInt32(reader["LANG"])})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        /// <summary>
        /// DB와 메모리 상의 List의 볼륨 값을 업데이트한다.
        /// </summary>
        /// <param name="volume">볼륨 값</param>
        /// <param name="guildId">디스코드 서버 ID</param>
        /// <returns>void</returns>
        public static async Task UpdateVolumeAsync(float volume, ulong guildId)
        {
            string connStr = @"DataSource=.\GuildSettings.db";
            string paths = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", paths);

            try
            {
                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SQLiteCommand("UPDATE Guilds SET VOLUME=@VOLUME WHERE ID=@ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", guildId.ToString());
                        cmd.Parameters.AddWithValue("@VOLUME", volume);
                        await cmd.ExecuteNonQueryAsync();
                        CustomLog.PrintLog(LogSeverity.Info, "Database",
                            $"Changed volume to {volume} (GuildId: {guildId})");
                    }
                }

                GuildSettings? guild = GetGuildsList().Find(x => x.GuildId == guildId);
                if (guild != null)
                    guild.PlayerVolume = volume;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        /// <summary>
        /// DB와 메모리 상의 List의 언어 설정을 업데이트한다.
        /// </summary>
        /// <param name="language">언어</param>
        /// <param name="guildId">디스코드 서버 ID</param>
        /// <returns>void</returns>
        public static async Task UpdateLanguageAsync(Translations language, ulong guildId)
        {
            string connStr = @"DataSource=.\GuildSettings.db";
            string paths = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", paths);

            try
            {
                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SQLiteCommand("UPDATE Guilds SET LANG=@LANG WHERE ID=@ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", guildId.ToString());
                        cmd.Parameters.AddWithValue("@LANG", language);
                        await cmd.ExecuteNonQueryAsync();
                        CustomLog.PrintLog(LogSeverity.Info, "Database",
                            $"Changed searchmode to {language} (GuildId: {guildId})");
                    }
                }

                GuildSettings? guild = GetGuildsList().Find(x => x.GuildId == guildId);
                if (guild != null)
                    guild.Language = language;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        /// <summary>
        /// DB와 메모리 상의 List의 검색 플랫폼 설정을 업데이트한다.
        /// </summary>
        /// <param name="searchMode">유튜브/사운드클라우드</param>
        /// <param name="guildId">디스코드 서버 ID</param>
        /// <returns>void</returns>
        public static async Task UpdateSearchMode(SearchMode searchMode, ulong guildId)
        {
            string connStr = @"DataSource=.\GuildSettings.db";
            string paths = AppDomain.CurrentDomain.BaseDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", paths);

            try
            {
                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SQLiteCommand("UPDATE Guilds SET SEARCHMODE=@SEARCHMODE WHERE ID=@ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", guildId.ToString());
                        cmd.Parameters.AddWithValue("@SEARCHMODE", searchMode);
                        await cmd.ExecuteNonQueryAsync();
                        CustomLog.PrintLog(LogSeverity.Info, "Database",
                            $"Changed searchmode to {searchMode} (GuildId: {guildId})");
                    }
                }

                GuildSettings? guild = GetGuildsList().Find(x => x.GuildId == guildId);
                if (guild != null)
                    guild.SearchPlatform = searchMode;
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        public static SearchMode FindGuildSearchMode(ulong guildId)
        {
            GuildSettings? data = GetGuildsList().Find(x => x.GuildId == guildId);
            if (data?.SearchPlatform == null)
                return SearchMode.YouTube;
            else
                return data.SearchPlatform;
        }

        /// <summary>
        /// DB와 List<GuildSettings>에 새로운 디스코드 서버 설정값을 추가하는 함수
        /// </summary>
        /// <param name="settings">길드 설정 정보</param>
        /// <returns>void</returns>
        public static async Task AddNewGuildAsync(GuildSettings settings)
        {
            try
            {
                string connStr = @"DataSource=.\GuildSettings.db";
                string paths = AppDomain.CurrentDomain.BaseDirectory;
                AppDomain.CurrentDomain.SetData("DataDirectory", paths);

                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SQLiteCommand("SELECT ID FROM Guilds WHERE ID=@ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", settings.GuildId.ToString());
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (await reader.ReadAsync()) // 이미 등록된 GuildId의 데이터베이스가 있을 경우 취소한다.
                                return;
                        }
                    }

                    using (var cmd = new SQLiteCommand("INSERT INTO Guilds([ID], [VOLUME], [LANG], [SEARCHMODE]) VALUES(@ID, @VOLUME, @LANG, @SEARCHMODE)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", settings.GuildId.ToString());
                        cmd.Parameters.AddWithValue("@VOLUME", 0.5f);
                        cmd.Parameters.AddWithValue("@LANG", settings.Language);
                        cmd.Parameters.AddWithValue("@SEARCHMODE", settings.SearchPlatform);
                        await cmd.ExecuteNonQueryAsync();
                        if (GetGuildsList().Find(x => x.GuildId == settings.GuildId) == null)
                            GetGuildsList().Add(settings);

                        CustomLog.PrintLog(LogSeverity.Info, "Database",
                            $"New database added successfully (GuildId: {settings.GuildId}");
                    }
                }                   
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        /// <summary>
        /// 봇의 디스코드 서버 퇴장 이벤트에서 데이터를 삭제하는 함수
        /// </summary>
        /// <param name="guildId">디스코드 서버 ID</param>
        /// <returns>void</returns>
        public static async Task RemoveGuildDataAsync(ulong guildId)
        {
            try
            {
                var guild = GetGuildsList().Find(x => x.GuildId == guildId);
                if (guild != null)
                    GetGuildsList().Remove(guild);

                string connStr = @"DataSource=.\GuildSettings.db";
                string paths = AppDomain.CurrentDomain.BaseDirectory;
                AppDomain.CurrentDomain.SetData("DataDirectory", paths);

                using (var conn = new SQLiteConnection(connStr))
                {
                    await conn.OpenAsync();

                    using (var cmd = new SQLiteCommand("DELETE FROM Guilds WHERE [ID]=@ID", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", guildId.ToString());
                        int result = await cmd.ExecuteNonQueryAsync();

                        CustomLog.PrintLog(LogSeverity.Info, "Database",
                            $"Database removed successfully (GuildId: {guildId}");
                    }
                }
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }
    }
}
