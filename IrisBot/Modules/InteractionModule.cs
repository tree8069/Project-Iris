using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IrisBot.Database;
using IrisBot.Translation;
using IrisBot.Enums;
using Lavalink4NET;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;

namespace IrisBot.Modules
{
    public class InteractionModule
    {
        private readonly DiscordShardedClient _client;
        private readonly InteractionService _handler;
        private readonly IServiceProvider _services;
        public static int ReadyShards = 0;

        public InteractionModule(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordShardedClient>();
            _handler = services.GetRequiredService<InteractionService>();
            _services = services;
        }

        public async Task ShardReady(DiscordSocketClient client)
        {
            ReadyShards++; // 샤드가 전부 연결되었을 때 커맨드를 등록해야한다.
            if (ReadyShards == _client.Shards.Count)
            {
                if (Program.IsDebug())
                {
                    CustomLog.PrintLog(LogSeverity.Warning, "Bot",
                        $"Bot is running on Debug build. Commands will be registered only on specified guild id. ({Program.TestGuildId})");

                    await Task.Run(async () => await _handler.RegisterCommandsToGuildAsync(Convert.ToUInt64(Program.TestGuildId), true));
                }
                else
                {
                    CustomLog.PrintLog(LogSeverity.Info, "Bot", "Bot is running on Release build.");
                    await Task.Run(async () => await _handler.RegisterCommandsGloballyAsync(true));
                }

                await _services.GetRequiredService<IAudioService>().InitializeAsync();

                foreach (var guild in client.Guilds)
                {
                    if (GuildSettings.GetGuildsList().Find(x => x.GuildId == guild.Id) == null)
                    {
                        await GuildSettings.AddNewGuildAsync(new GuildSettings(guild.Id, 0.5f));
                    }
                }
            }
        }

        public async Task InitializeAsync()
        {
            _client.ShardReady += ShardReady;
            _handler.Log += LogAsync;
            await _handler.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            _client.InteractionCreated += HandleInteraction;
            _client.SelectMenuExecuted += SelectMenuHandler;
        }

        private async Task LogAsync(LogMessage log)
        {
            if (log.Exception == null)
                CustomLog.PrintLog(log.Severity, log.Source, log.Message);
            else
                await CustomLog.ExceptionHandler(log.Exception);
        }

        private async Task ListPageViewAsync(SocketMessageComponent arg)
        {
            SocketGuildUser user = (SocketGuildUser)arg.User;
            string text = string.Join(", ", arg.Data.Values);
            var _audioService = _services.GetRequiredService<IAudioService>();
            if (arg.GuildId == null)
                return;
            IrisPlayer? player = _audioService.GetPlayer<IrisPlayer>(arg.GuildId.Value);
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(arg.GuildId.Value);

            StringBuilder sb = new StringBuilder();
            if (player?.CurrentTrack == null)
            {
                sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("now_playing", lang)}: N/A```");
            }
            else
            {
                int bitrate = 0;
                if (player.VoiceChannelId != null)
                {
                    var channel = user.Guild.GetVoiceChannel((ulong)player.VoiceChannelId);
                    if (channel != null)
                        bitrate = channel.Bitrate / 1000;
                }

                sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("now_playing", lang)}: {player.CurrentTrack.Title} - {player.CurrentTrack.Author} " +
                    $"[{player.Position.RelativePosition.ToString(@"hh\:mm\:ss")}/{player.CurrentTrack.Duration.ToString(@"hh\:mm\:ss")}] " +
                    $"[{bitrate} Kbps]```");
                // Now playing: Song name - [AuthorName] [00:00:00/01:10:23] [96 Kbps]
            }

            ComponentBuilder? component = null;
            if (player?.Queue == null || player.Queue.Count == 0)
            {
                sb.AppendLine("```Nothing in queue.```");
            }
            else
            {
                int startPage = (Convert.ToInt32(text) - 1) * Program.PagelistCount;
                int pageCount = 0;
                for (int i = 0; i < player.Queue.Count; pageCount++, i += Program.PagelistCount) ;
                // SelectMenu에 표시할 페이지 수 계산

                var menu = new SelectMenuBuilder()
                {
                    CustomId = "list_pageview",
                    Placeholder = await TranslationLoader.GetTranslationAsync("select_number", lang),
                };
                for (int i = 0; i < pageCount; i++)
                    menu.AddOption((i + 1).ToString(), (i + 1).ToString());

                component = new ComponentBuilder();
                component.WithSelectMenu(menu);

                sb.Append("```");
                for (int i = startPage; i < startPage + Program.PagelistCount && i < player.Queue.Count; i++)
                {
                    LavalinkTrack track = player.Queue.Tracks[i];
                    sb.AppendLine($"{i + 1}. {track.Title} - [{track.Author}] [{track.Duration.ToString(@"hh\:mm\:ss")}]");
                    // 1. Song name - [AuthorName] [01:10:23]
                }
                sb.AppendLine($"\r\nPage {Convert.ToInt32(text)}/{pageCount}```");
            }

            await arg.UpdateAsync(x =>
            {
                x.Content = sb.ToString();
                x.Components = component?.Build() ?? null;
            });
        }

        private async Task MusicSelectAsync(SocketMessageComponent arg)
        {
            SocketGuildUser user = (SocketGuildUser)arg.User;
            var selectedId = string.Join(", ", arg.Data.Values);
            var _audioService = _services.GetRequiredService<IAudioService>();
            if (arg.GuildId == null)
                return;
            
            var player = _audioService.GetPlayer<IrisPlayer>(arg.GuildId.Value);
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(arg.GuildId.Value);
            if (user.VoiceChannel == null)
            {
                string tmp = await TranslationLoader.GetTranslationAsync("should_joined", lang);
                await arg.UpdateAsync(x =>
                {
                    x.Content = tmp;
                    x.Components = null;
                    x.Embeds = null;
                });
                return;
            }
            else if (player != null && player.VoiceChannelId != user.VoiceChannel.Id)
            {
                string tmp = await TranslationLoader.GetTranslationAsync("player_already_running", lang);
                await arg.UpdateAsync(x =>
                {
                    x.Content = tmp;
                    x.Components = null;
                    x.Embeds = null;
                });
                return;
            }
            else if (player == null)
            {
                player = await _audioService.JoinAsync<IrisPlayer>(arg.GuildId.Value, user.VoiceChannel.Id, selfDeaf: true);
                player.Channel = arg.Message.Channel;
            }
            else if (player.Queue.Count >= Program.MaxQueueCount)
            {
                string tmp = await TranslationLoader.GetTranslationAsync("maximum_queue", lang);
                await arg.UpdateAsync(x =>
                {
                    x.Content = tmp;
                    x.Components = null;
                    x.Embeds = null;
                });
                return;
            }

            SearchMode mode = GuildSettings.FindGuildSearchMode(arg.GuildId.Value);
            LavalinkTrack? myTrack;
            if (arg.Data.CustomId == "music_select" || arg.Data.CustomId == "music_select_top") // YouTube 검색 모드인경우
                myTrack = await _audioService.GetTrackAsync(selectedId, SearchMode.YouTube);
            else // SoundCloud 검색 모드인 경우
                myTrack = await _audioService.GetTrackAsync(selectedId, SearchMode.SoundCloud);

            player.Channel = arg.Channel;
            if (myTrack == null)
            {
                await arg.UpdateAsync(x =>
                {
                    x.Content = "Failed to fetch track information.";
                    x.Components = null;
                    x.Embeds = null;
                });
                return;
            }
            myTrack.Context = new TrackContext
            {
                RequesterName = arg.User.Username,
                OriginalQuery = selectedId,
            };

            if (arg.Data.CustomId.Contains("_top")) // 우선순위 예약 명령일 경우
            {
                EmbedBuilder eb = new EmbedBuilder();
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("top_priority_queue", lang))
                    .WithDescription($"[{myTrack.Title}]({myTrack.Uri})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("author", lang)} : `{myTrack.Author}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("duration", lang)} : `{myTrack.Duration.ToString(@"hh\:mm\:ss")}`")
                    .WithColor(Color.Purple);

                await player.PlayTopAsync(myTrack);
                await arg.UpdateAsync(x =>
                {
                    x.Embed = eb.Build();
                    x.Content = "";
                    x.Components = null;
                });
            }
            else // 우선순위 예약 명령이 아닌 경우
            {
                EmbedBuilder eb = new EmbedBuilder();
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("added_queue", lang))
                    .WithDescription($"[{myTrack.Title}]({myTrack.Uri})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("author", lang)} : `{myTrack.Author}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("duration", lang)} : `{myTrack.Duration.ToString(@"hh\:mm\:ss")}`")
                    .WithColor(Color.Purple);

                await player.PlayAsync(myTrack);
                await arg.UpdateAsync(x =>
                {
                    x.Embed = eb.Build();
                    x.Content = "";
                    x.Components = null;
                });
            }
        }

        private async Task SelectMenuHandler(SocketMessageComponent arg)
        {
            if (arg.Data.CustomId == "music_select" || arg.Data.CustomId == "music_select_top"
                || arg.Data.CustomId == "music_select_soundcloud" || arg.Data.CustomId == "music_select_soundcloud_top")
            {
                await MusicSelectAsync(arg);
            }
            else if (arg.Data.CustomId == "list_pageview")
            {
                await ListPageViewAsync(arg);
            }
        }

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                if (interaction.IsDMInteraction)
                {
                    await interaction.RespondAsync("Sorry. Slash commands on DM is not supported.");
                    return;
                }

                // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
                var context = new ShardedInteractionContext(_client, interaction);

                // Execute the incoming command.
                var result = await _handler.ExecuteCommandAsync(context, _services);
                CustomLog.PrintLog(LogSeverity.Info, "Interaction",
                    $"Command executed (Guild: {interaction.GuildId}, Channel: {interaction.Channel.Name}, User: {interaction.User.Username})");

                if (!result.IsSuccess)
                    switch (result.Error)
                    {
                        case InteractionCommandError.UnmetPrecondition:
                            // implement
                            break;
                        default:
                            break;
                    }
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }
}
