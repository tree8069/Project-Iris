using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IrisBot.Database;
using IrisBot.Enums;
using IrisBot.Translation;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracking;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace IrisBot.Modules
{
    public sealed class TrackContext
    {
        public string? RequesterName { get; set; }
        public string? OriginalQuery { get; set; }
    }

    public class MusicCommandModule : InteractionModuleBase<ShardedInteractionContext>
    {
        private IAudioService _audioService;
        private IServiceProvider _services;

        public MusicCommandModule(IAudioService audioService, IServiceProvider services)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _services = services;
        }

        public Task InitializeAsync()
        {
            _services.GetRequiredService<InactivityTrackingService>().InactivePlayer += Program_InactivePlayer;
            _audioService.TrackEnd += _audioService_TrackEnd;
            _audioService.TrackStarted += _audioService_TrackStarted;
            return Task.CompletedTask;
        }

        private async Task _audioService_TrackStarted(object sender, TrackStartedEventArgs eventArgs)
        {
            IrisPlayer? player = eventArgs.Player as IrisPlayer;

            if (player?.CurrentTrack != null && player?.Channel != null)
            {
                TrackContext? ctx = player.CurrentTrack.Context as TrackContext;
                string requesterName = "null";
                if (ctx?.RequesterName != null)
                    requesterName = ctx.RequesterName;


                EmbedBuilder eb = new EmbedBuilder();
                Translations lang = await TranslationLoader.FindGuildTranslationAsync(player.GuildId);
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("now_playing", lang))
                    .WithDescription($"[{player.CurrentTrack.Title}]({player.CurrentTrack.Uri?.ToString()})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("author", lang)} : `{player.CurrentTrack.Author}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("duration", lang)} : `{player.CurrentTrack.Duration.ToString(@"hh\:mm\:ss")}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("requester_name", lang)} : `{requesterName}`")
                    .WithColor(Color.Purple);
                await player.Channel.SendMessageAsync(embed: eb.Build());
            }
        }

        private async Task _audioService_TrackEnd(object sender, TrackEndEventArgs eventArgs)
        {
            IrisPlayer? player = eventArgs.Player as IrisPlayer;
            if (eventArgs.Reason != TrackEndReason.Finished)
                return;

            if (player?.Channel != null && player.Queue.IsEmpty)
            {
                Translations lang = await TranslationLoader.FindGuildTranslationAsync(player.GuildId);
                await player.Channel.SendMessageAsync(await TranslationLoader.GetTranslationAsync("track_end_no_queue", lang));
            }
        }

        private async Task Program_InactivePlayer(object sender, InactivePlayerEventArgs eventArgs)
        {
            IrisPlayer? player = eventArgs.Player as IrisPlayer;
            if (player?.Channel != null)
            {
                Translations lang = await TranslationLoader.FindGuildTranslationAsync(player.GuildId);
                await player.Channel.SendMessageAsync(await TranslationLoader.GetTranslationAsync("inactivity_disconnect", lang));
            }
        }

        [SlashCommand("join", "Join voicechannel")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task JoinAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (user.VoiceChannel == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("should_joined", lang), ephemeral: true);
                return;
            }

            var player = _audioService.GetPlayer<IrisPlayer>(Context.Guild.Id);
            if (player == null)
            {
                player = await _audioService.JoinAsync<IrisPlayer>(Context.Guild.Id, user.VoiceChannel.Id, selfDeaf: true);
                player.Channel = Context.Channel;
                await RespondAsync($"{await TranslationLoader.GetTranslationAsync("connect_voicechannel", lang)}: {user.VoiceChannel.Name}");
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("player_already_running", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId == user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("player_already_samechannel", lang), ephemeral: true);
            }
        }

        [SlashCommand("music", "Search music and add it to queue")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task MusicAsync(string query)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            var player = _audioService.GetPlayer<IrisPlayer>(Context.Guild.Id);

            if (user.VoiceChannel == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("should_joined", lang), ephemeral: true);
                return;
            }
            else if (player != null && player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("player_already_running", lang), ephemeral: true);
                return;
            }
            else if (player == null)
            {
                player = await _audioService.JoinAsync<IrisPlayer>(Context.Guild.Id, user.VoiceChannel.Id, selfDeaf: true);
                player.Channel = Context.Channel;
            }
            else if (player.Queue.Count >= Program.MaxQueueCount)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("maximum_queue", lang), ephemeral: true);
                return;
            }

            GuildSettings? data = GuildSettings.GetGuildsList().Find(x => x.GuildId == Context.Guild.Id);

            if (data == null)
                await player.SetVolumeAsync(0.5f);
            else
                await player.SetVolumeAsync(data.PlayerVolume);

            SearchMode mode = GuildSettings.FindGuildSearchMode(Context.Guild.Id);
            var myTracks = await _audioService.LoadTracksAsync(query, mode);

            if (myTracks?.Tracks?.Length == 0)
            {
                // 결과가 0개인 경우 검색 실패로 판단한다.
                await RespondAsync(await TranslationLoader.GetTranslationAsync("search_failed", lang), ephemeral: true);
            }
            else if (myTracks?.PlaylistInfo?.Name != null && myTracks?.Tracks != null)
            {
                // Playlist 정보가 있을 경우 플레이리스트 등록 메소드 사용
                foreach (var track in myTracks.Tracks)
                {
                    if (player.Queue.Count() >= Program.MaxQueueCount)
                        break;

                    track.Context = new TrackContext
                    {
                        RequesterName = user.Username,
                    };
                    player.Queue.Add(track);
                }

                // 재생중이 아닌 상태에서 PlayAsync가 아닌 Queue.Add를 사용했을 경우 재생하지 않기 때문에 SkipAsync를 사용한다.
                if (player.State != PlayerState.Playing)
                    await player.SkipAsync();

                EmbedBuilder eb = new EmbedBuilder();
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("added_playlist_queue", lang))
                    .WithDescription($"[{myTracks.PlaylistInfo.Name}]({query})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("total_tracks", lang)} : `{myTracks.Tracks.Length}`")
                    .WithColor(Color.Purple);

                // 대기열 수 초과시 Footer로 안내 메세지 출력
                if (player.Queue.Count() >= Program.MaxQueueCount)
                    eb.WithFooter(await TranslationLoader.GetTranslationAsync("playlist_fail_maximum_queue", lang));

                await RespondAsync(embed: eb.Build());
            }
            else if (myTracks?.Tracks?.Length == 1)
            {
                // 존재하는 1개의 링크를 입력할 경우 바로 재생한다
                LavalinkTrack myTrack = myTracks.Tracks.First();
                myTrack.Context = new TrackContext
                {
                    RequesterName = user.Username,
                };

                EmbedBuilder eb = new EmbedBuilder();
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("added_queue", lang))
                    .WithDescription($"[{myTrack.Title}]({myTrack.Uri?.ToString()})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("author", lang)} : `{myTrack.Author}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("duration", lang)} : `{myTrack.Duration.ToString(@"hh\:mm\:ss")}`")
                    .WithColor(Color.Purple);
                
                await player.PlayAsync(myTrack);
                await RespondAsync(embed: eb.Build(), ephemeral: true);
            }
            else
            {
                string customId = "";
                StringBuilder sb = new StringBuilder(1024);
                if (mode == SearchMode.YouTube)
                {
                    customId = "music_select";
                    sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("searchresult_youtube", lang)} \"{query}\"\r\n");
                }
                else
                {
                    customId = "music_select_soundcloud";
                    sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("searchresult_soundcloud", lang)} \"{query}\"\r\n");
                }

                var menu = new SelectMenuBuilder()
                {
                    CustomId = customId,
                    Placeholder = await TranslationLoader.GetTranslationAsync("select_number", lang),
                };

                for (int i = 0; i < 10 && i < myTracks?.Tracks?.Length; i++)
                {
                    LavalinkTrack track = myTracks.Tracks.ElementAt(i);
                    sb.AppendLine($"{i + 1}. {track.Title} - [{track.Author}] [{track.Duration}]");
                    menu.AddOption((i + 1).ToString(), track.Uri?.ToString(), $"{track.Uri?.ToString()}");
                }
                var component = new ComponentBuilder();
                component.WithSelectMenu(menu);
                sb.AppendLine("```");

                await RespondAsync(sb.ToString(), components: component.Build(), ephemeral: true);
            }
        }

        [SlashCommand("pause", "Pause current track")]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task PauseAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (player?.CurrentTrack == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                if (player.State == PlayerState.Playing)
                {
                    await player.PauseAsync();
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("pause_music", lang));
                }
                else
                {
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("already_paused", lang), ephemeral: true);
                }
            }
        }

        [SlashCommand("resume", "Resume paused track")]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task ResumeAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (player?.CurrentTrack == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                if (player.State == PlayerState.Paused)
                {
                    await player.ResumeAsync();
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("resume_music", lang));
                }
                else
                {
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("already_resumed", lang), ephemeral: true);
                }

            }
        }

        [SlashCommand("seek", "Seek current track position")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task SeekAsync(int seconds)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (player?.CurrentTrack == null)
            {    
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                await player.SeekPositionAsync(time);
                await RespondAsync($"{await TranslationLoader.GetTranslationAsync("seek_to_position", lang)}: {new DateTime(time.Ticks).ToString("mm:ss")}");
            }
        }

        [SlashCommand("volume", "Set player volume")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task VolumeAsync(int volume)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (volume < 1 || volume > 100)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("volume_invalid_value", lang), ephemeral: true);
            }
            else if (player == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                await player.SetVolumeAsync((float)volume / 100);
                await GuildSettings.UpdateVolumeAsync((float)volume / 100, Context.Guild.Id);
                await RespondAsync($"{await TranslationLoader.GetTranslationAsync("volume_changed", lang)}: {volume}");
            }
        }

        [SlashCommand("leave", "Leave voice channel")]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task LeaveAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (player == null || player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                await player.DisconnectAsync();
                await player.DisposeAsync();
                await RespondAsync(await TranslationLoader.GetTranslationAsync("disconnect_voicechannel", lang));
            }
        }

        [SlashCommand("searchmode", "Change search platform YouTube or SoundCloud")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task Language(IrisSearchMode searchMode)
        {
            await GuildSettings.UpdateSearchMode((SearchMode)searchMode, Context.Guild.Id);
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (searchMode == IrisSearchMode.YouTube)
                await RespondAsync(await TranslationLoader.GetTranslationAsync("searchmode_youtube", lang));
            else
                await RespondAsync(await TranslationLoader.GetTranslationAsync("searchmode_soundcloud", lang));
        }

        [SlashCommand("list", "Display current track and queue list")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task ListAsync()
        {
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
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
                    var channel = Context.Guild.GetVoiceChannel((ulong)player.VoiceChannelId);
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
                for (int i = 0; i < Program.PagelistCount && i < player.Queue.Count; i++)
                {
                    LavalinkTrack track = player.Queue.Tracks[i];
                    TrackContext? ctx = track.Context as TrackContext;
                    string requesterName = "ERROR";
                    if (ctx?.RequesterName != null)
                        requesterName = ctx.RequesterName;

                    sb.AppendLine($"{i + 1}. {track.Title} - [{track.Author}] [{track.Duration.ToString(@"hh\:mm\:ss")}]" +
                        $" - [{await TranslationLoader.GetTranslationAsync("requester_name", lang)}: {requesterName}]");
                    // 1. Song name - [AuthorName] [01:10:23] - 신청자명
                }
                sb.AppendLine($"\r\nPage 1/{pageCount}```");
            }

            await RespondAsync(sb.ToString(), components: component?.Build() ?? null);

            // 기존 /list 명령 컴포넌트가 있을 경우 삭제한다.
            GuildSettings? data = GuildSettings.GetGuildsList().Find(x => x.GuildId == Context.Guild.Id);
            var msg = await GetOriginalResponseAsync();
            if (data != null)
            {
                if (data.ListMessagdId != null)
                    await Context.Channel.DeleteMessageAsync((ulong)data.ListMessagdId);
                data.ListMessagdId = msg.Id;
            }
        }

        [SlashCommand("skip", "Skip current track")]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task SkipAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (player?.CurrentTrack == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                var track = player.CurrentTrack;
                await player.SkipAsync();
                await RespondAsync($"{await TranslationLoader.GetTranslationAsync("skip_music", lang)}: {track.Title}");
            }
        }

        [SlashCommand("shuffle", "Randomize the entire queue")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task ShuffleAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (player == null || player.Queue.IsEmpty)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("empty_queue", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                player.Queue.Shuffle();
                await RespondAsync(await TranslationLoader.GetTranslationAsync("shuffle_queue", lang));
            }
        }

        [SlashCommand("remove", "Remove a track on the queue")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task RemoveAsync(int index)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (index < 1)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("queue_delete_overflow", lang), ephemeral: true);
            }
            else if (player == null || player.Queue.IsEmpty)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("empty_queue", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                if (index > player.Queue.Count)
                {
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("queue_delete_overflow", lang), ephemeral: true);
                }
                else
                {
                    LavalinkTrack myTrack = player.Queue.ElementAt(index - 1);
                    player.Queue.RemoveAt(index - 1);
                    await RespondAsync($"{await TranslationLoader.GetTranslationAsync("remove_queue", lang)}: " +
                        $"{myTrack.Title} - [{myTrack.Author}] [{myTrack.Duration.ToString(@"hh\:mm\:ss")}]");
                    // Song name - [AuthorName] [01:10:23]
                }
            }
        }

        [SlashCommand("mremove", "Remove multiple tracks on the queue")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task MremoveAsync(int index, int count)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (index < 1 || count < 1)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("queue_delete_overflow", lang), ephemeral: true);
            }
            else if (player == null || player.Queue.IsEmpty)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("empty_queue", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                if (index + count - 1 > player.Queue.Count)
                {
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("queue_delete_overflow", lang), ephemeral: true);
                }
                else
                {
                    player.Queue.RemoveRange(index - 1, count);
                    await RespondAsync($"{await TranslationLoader.GetTranslationAsync("remove_queue", lang)}: " +
                        $"{await TranslationLoader.GetTranslationAsync("queue", lang)} #{index} ~ #{index + count - 1}");
                    // 대기열 #1 ~ #5
                }
            }
        }

        [SlashCommand("clear", "Clear entire queue")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task ClearAsync()
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (player == null || player.Queue.IsEmpty)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("empty_queue", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                player.Queue.Clear();
                await RespondAsync($"{await TranslationLoader.GetTranslationAsync("clear_queue", lang)}:");
            }
        }

        [SlashCommand("loop", "Toggle loop mode")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task LoopAsync(IrisLoopMode loopMode)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);

            if (player == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("nothing_playing", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                player.LoopMode = (PlayerLoopMode)loopMode;
                if (loopMode == IrisLoopMode.None)
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("loop_none", lang));
                else if (loopMode == IrisLoopMode.Queue)
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("loop_entire", lang));
                else
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("loop_single", lang));
            }
        }

        [SlashCommand("musictop", "Add track as 1st priority")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task MusicTopAsync(string query)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            var player = _audioService.GetPlayer<IrisPlayer>(Context.Guild.Id);

            if (user.VoiceChannel == null)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("should_joined", lang), ephemeral: true);
                return;
            }
            else if (player != null && player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("player_already_running", lang), ephemeral: true);
                return;
            }
            else if (player == null)
            {
                player = await _audioService.JoinAsync<IrisPlayer>(Context.Guild.Id, user.VoiceChannel.Id, selfDeaf: true);
                player.Channel = Context.Channel;
            }
            else if (player.Queue.Count >= Program.MaxQueueCount)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("maximum_queue", lang), ephemeral: true);
                return;
            }

            GuildSettings? data = GuildSettings.GetGuildsList().Find(x => x.GuildId == Context.Guild.Id);

            if (data == null)
                await player.SetVolumeAsync(0.5f);
            else
                await player.SetVolumeAsync(data.PlayerVolume);

            SearchMode mode = GuildSettings.FindGuildSearchMode(Context.Guild.Id);
            var myTracks = await _audioService.LoadTracksAsync(query, mode);

            if (myTracks?.Tracks?.Length == 0)
            {
                // 결과가 0개인 경우 검색 실패로 판단한다.
                await RespondAsync(await TranslationLoader.GetTranslationAsync("search_failed", lang), ephemeral: true);
            }
            else if (myTracks?.Tracks?.Length == 1)
            {
                // 존재하는 1개의 링크를 입력할 경우 바로 재생한다
                LavalinkTrack myTrack = myTracks.Tracks.First();
                myTrack.Context = new TrackContext
                {
                    RequesterName = user.Username,
                };

                EmbedBuilder eb = new EmbedBuilder();
                eb.WithTitle(await TranslationLoader.GetTranslationAsync("top_priority_queue", lang))
                    .WithDescription($"[{myTrack.Title}]({myTrack.Uri?.ToString()})" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("author", lang)} : `{myTrack.Author}`" +
                        $"\r\n{await TranslationLoader.GetTranslationAsync("duration", lang)} : `{myTrack.Duration.ToString(@"hh\:mm\:ss")}`")
                    .WithColor(Color.Purple);

                await player.PlayTopAsync(myTrack);
                await RespondAsync(embed: eb.Build());
            }
            else
            {
                string customId = "";
                StringBuilder sb = new StringBuilder(1024);
                if (mode == SearchMode.YouTube)
                {
                    customId = "music_select_top";
                    sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("searchresult_youtube", lang)} \"{query}\"\r\n");
                }
                else
                {
                    customId = "music_select_soundcloud_top";
                    sb.AppendLine($"```{await TranslationLoader.GetTranslationAsync("searchresult_soundcloud", lang)} \"{query}\"\r\n");
                }

                var menu = new SelectMenuBuilder()
                {
                    CustomId = customId,
                    Placeholder = await TranslationLoader.GetTranslationAsync("select_number", lang),
                };

                for (int i = 0; i < 10 && i < myTracks?.Tracks?.Length; i++)
                {
                    LavalinkTrack track = myTracks.Tracks.ElementAt(i);
                    sb.AppendLine($"{i + 1}. {track.Title} - [{track.Author}] [{track.Duration}]");
                    menu.AddOption((i + 1).ToString(), track.Uri?.ToString(), $"{track.Uri?.ToString()}");
                }
                var component = new ComponentBuilder();
                component.WithSelectMenu(menu);
                sb.AppendLine("```");

                await RespondAsync(sb.ToString(), components: component.Build());
            }
        }
    }
}
