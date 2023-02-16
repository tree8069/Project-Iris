using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IrisBot.Database;
using IrisBot.Modules;
using Lavalink4NET;
using Lavalink4NET.DiscordNet;
using Lavalink4NET.Logging.Microsoft;
using Lavalink4NET.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IrisBot
{
    public class Program
    {
        private readonly IServiceProvider _services;
        private readonly DiscordSocketConfig _socketConfig;
        private readonly DiscordShardedClient _client;

        public static string? TestGuildId { get; private set; }
        public static int PagelistCount { get; private set; }
        public static int MaxPlaylistCount { get; private set; }
        public static int MaxQueueCount { get; private set; }
        private static int ShardsCount = 1;
        private static int AutoDisconnectDelay = 600;
        private static string? Token = "";
        private static string? RestUri = "";
        private static string? WebSocketUri = "";
        private static string? Password = "";
        private static string? BotMessage = "";
        public static string PlaylistDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Playlist"); }
        }
        public static string TranslationsDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Translation"); }
        }

        public Program()
        {
            MaxQueueCount = 200;
            if (string.IsNullOrEmpty(RestUri))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"rest_uri\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(WebSocketUri))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"websocket_uri\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(Password))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"password\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(TestGuildId) && IsDebug()) // testguild_id는 Debug에서만 필요함
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"testguild_id\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(Token))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"token\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(RestUri))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"rest_uri\" is empty on appsettings.json");
                Environment.Exit(1);
            }
            else if (string.IsNullOrEmpty(BotMessage))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "\"bot_message\" is empty on appsettings.json");
                Environment.Exit(1);
            }

            _socketConfig = new DiscordSocketConfig()
            {
                GatewayIntents = GatewayIntents.AllUnprivileged ^ GatewayIntents.GuildInvites ^ GatewayIntents.GuildScheduledEvents,
                AlwaysDownloadUsers = true,
                LogLevel = LogSeverity.Debug,
                TotalShards = ShardsCount,
                UseInteractionSnowflakeDate = false
            };
            _client = new DiscordShardedClient(_socketConfig);

            _services = new ServiceCollection()
                // Discord.NET
                .AddSingleton<InteractionModule>()
                .AddSingleton<MusicCommandModule>()
                .AddSingleton(_client)
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordShardedClient>()))
                // Discord.NET

                // Lavalink4NET
                .AddSingleton<IAudioService, LavalinkNode>()
                .AddSingleton<IDiscordClientWrapper, DiscordClientWrapper>()
                .AddSingleton(new LavalinkNodeOptions
                {
                    RestUri = RestUri,
                    WebSocketUri = WebSocketUri,
                    Password = Password,
                    ReconnectStrategy = ReconnectStrategies.DefaultStrategy,
                    BufferSize = 1048576, // 1 MB
                    DisconnectOnStop = false,
                })
                .AddMicrosoftExtensionsLavalinkLogging()
                .AddLogging(s => s.AddConsole().SetMinimumLevel(LogLevel.Information))
                // Lavalink4NET

                // InactivityTracking
                .AddSingleton(new InactivityTrackingOptions
                {
                    DisconnectDelay = TimeSpan.FromSeconds(AutoDisconnectDelay),
                })
                .AddSingleton(x => new InactivityTrackingService(x.GetRequiredService<IAudioService>(),
                                x.GetRequiredService<IDiscordClientWrapper>(),
                                x.GetRequiredService<InactivityTrackingOptions>()))
                // InactivityTracking

                .BuildServiceProvider();
        }

        static void Main(string[] args)
        {
            LoadSettingsAsync().GetAwaiter().GetResult();
            new Program().RunAsync().GetAwaiter().GetResult();
        }

        public async Task RunAsync()
        {
            await GuildSettings.InitializeAsync(); // appsettings.json 불러오기

            var client = _services.GetRequiredService<DiscordShardedClient>();
            client.Log += LogAsync;
            client.JoinedGuild += JoinGuildAsync;
            client.LeftGuild += LeftGuildAsync;

            // 명령어 등록 및 클래스 이벤트 등록하는 함수
            await _services.GetRequiredService<InteractionModule>().InitializeAsync();
            await _services.GetRequiredService<MusicCommandModule>().InitializeAsync();
            _services.GetRequiredService<InactivityTrackingService>().BeginTracking();

            await client.LoginAsync(TokenType.Bot, Token);
            await client.StartAsync();
            await client.SetGameAsync(BotMessage); // appsettings.json에서 현재 상태 메세지를 수정할 수 있음.

            await Task.Delay(Timeout.Infinite);
        }

        private async Task JoinGuildAsync(SocketGuild guild)
        {
            if (GuildSettings.GetGuildsList().Find(x => x.GuildId == guild.Id) == null)
                await GuildSettings.AddNewGuildAsync(new GuildSettings(guild.Id, 0.5f)); // 데이터베이스 및 서버 객체 추가
        }

        private async Task LeftGuildAsync(SocketGuild guild)
        {
            await GuildSettings.RemoveGuildDataAsync(guild.Id); // 데이터베이스 및 서버 객체 삭제
            await Playlist.ClearPlaylistAsync(guild.Id); // 플레이리스트 전체 삭제
            
        }

        private async static Task LoadSettingsAsync()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(jsonPath))
            {
                CustomLog.PrintLog(LogSeverity.Error, "Bot", "appsettings.json is not exists.");
                Environment.Exit(1);
            }

            try
            {
                using (StreamReader file = File.OpenText(jsonPath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    JObject json = (JObject)JToken.ReadFrom(reader);
                    RestUri = json["rest_uri"]?.ToString();
                    WebSocketUri = json["websocket_uri"]?.ToString();
                    Password = json["password"]?.ToString();
                    TestGuildId = json["testguild_id"]?.ToString();
                    Token = json["token"]?.ToString();
                    BotMessage = json["bot_message"]?.ToString();
                    bool pagelistResult = int.TryParse(json["pagelist_count"]?.ToString(), out int pagelistCount);
                    bool playlistResult = int.TryParse(json["max_playlist_count"]?.ToString(), out int playlistCount);
                    bool shardsCountResult = int.TryParse(json["shards_count"]?.ToString(), out int shardsCount);
                    bool autoDisconnectDelayResult = int.TryParse(json["auto_disconnect_delay"]?.ToString(), out int autoDisconnectDelay);

                    if (pagelistResult)
                        PagelistCount = pagelistCount;
                    else
                    {
                        CustomLog.PrintLog(LogSeverity.Warning, "Bot", "\"pagelist_count\" is empty on appsettings.json.\r\nAutomatically set to default value 10.");
                        PagelistCount = 10;
                    }

                    if (playlistResult)
                        MaxPlaylistCount = playlistCount;
                    else
                    {
                        CustomLog.PrintLog(LogSeverity.Warning, "Bot", "\"max_playlist_count\" is empty on appsettings.json.\r\nAutomatically set to default value 10.");
                        MaxPlaylistCount = 10;
                    }

                    if (shardsCountResult)
                        ShardsCount = shardsCount;
                    else
                        CustomLog.PrintLog(LogSeverity.Warning, "Bot", "\"shards_count\" is empty on appsettings.json.\r\nAutomatically set to default value 1.");

                    if (autoDisconnectDelayResult)
                        AutoDisconnectDelay = autoDisconnectDelay;
                    else
                        CustomLog.PrintLog(LogSeverity.Warning, "Bot", "\"auto_disconnect_delay\" is empty on appsettings.json.\r\nAutomatically set to default value 600s.");
                }
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
            }
        }

        private async Task LogAsync(LogMessage log)
        {
            if (log.Exception == null)
                CustomLog.PrintLog(log.Severity, log.Source, log.Message);
            else
                await CustomLog.ExceptionHandler(log.Exception);
        }

        public static bool IsDebug()
        {
#if DEBUG
            return true;
#else
                return false;
#endif
        }
    }
}
