using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IrisBot.Database;
using IrisBot.Enums;
using IrisBot.Translation;
using Lavalink4NET;
using Lavalink4NET.Player;
using System.Text;

namespace IrisBot.Modules
{
    [Group("playlist", "Playlist management command")]
    public class PlaylistCommandModule : InteractionModuleBase<ShardedInteractionContext>
    {
        private IAudioService _audioService;
        private IServiceProvider _services;

        public PlaylistCommandModule(IAudioService audioService, IServiceProvider services)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _services = services;
        }

        [SlashCommand("list", "Display playlist lists")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task ViewPlaylist()
        {
            string path = Path.Combine(Program.PlaylistDirectory, Context.Guild.Id.ToString());
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (!Directory.Exists(path))
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("no_playlist", lang), ephemeral: true);
                return;
            }

            DirectoryInfo di = new DirectoryInfo(path);
            StringBuilder sb = new StringBuilder();
            sb.Append("```");
            int i = 1;
            foreach (FileInfo file in di.GetFiles())
            {
                sb.AppendLine($"{i} - {file.Name}");
            }
            sb.AppendLine("```");

            await RespondAsync(sb.ToString());
        }

        [SlashCommand("add", "Make or \"OVERWRITE\" playlist")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task AddPlaylist(string name)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            IrisPlayer? player = _audioService.GetPlayer(Context.Guild.Id) as IrisPlayer;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            if (player == null || (player.Queue.IsEmpty && player.CurrentTrack == null))
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("empty_queue", lang), ephemeral: true);
            }
            else if (player.VoiceChannelId != user.VoiceChannel.Id)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("different_channel_warning", lang), ephemeral: true);
            }
            else
            {
                PlaylistResult result = await Playlist.CreatePlaylistAsync(new Playlist(player.GuildId, name), player?.CurrentTrack, player?.Queue);
                switch (result)
                {
                    case PlaylistResult.New:
                        await RespondAsync($"{await TranslationLoader.GetTranslationAsync("playlist_new", lang)}: {name}", ephemeral: true);
                        break;
                    case PlaylistResult.Overwrite:
                        await RespondAsync($"{await TranslationLoader.GetTranslationAsync("playlist_overwrite", lang)}: {name}", ephemeral: true);
                        break;
                    case PlaylistResult.CreationLimit:
                        await RespondAsync(await TranslationLoader.GetTranslationAsync("playlist_creation_limit", lang), ephemeral: true);
                        break;
                    case PlaylistResult.Fail:
                        await RespondAsync(await TranslationLoader.GetTranslationAsync("playlist_fail", lang), ephemeral: true);
                        break;
                }
            }
        }

        [SlashCommand("remove", "Remove specified name of playlist")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task RemovePlaylist(string name)
        {
            PlaylistDeleteResult result = await Playlist.DeletePlaylistAsync(Context.Guild.Id, name);
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            switch (result)
            {
                case PlaylistDeleteResult.Success:
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("playlist_remove_success", lang));
                    break;
                case PlaylistDeleteResult.NotExists:
                    await RespondAsync($"{await TranslationLoader.GetTranslationAsync("playlist_remove_not_exists", lang)}: {name}", ephemeral: true);
                    break;
                case PlaylistDeleteResult.Fail:
                    await RespondAsync(await TranslationLoader.GetTranslationAsync("playlist_remove_fail", lang), ephemeral: true);
                    break;
            }
        }

        [SlashCommand("load", "Load specified name of playlist")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task LoadPlaylist(string name)
        {
            SocketGuildUser user = (SocketGuildUser)Context.User;
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            string[]? lists = await Playlist.LoadPlaylistAsync(Context.Guild.Id, name);

            if (lists == null || lists.Count() < 1)
            {
                await RespondAsync(await TranslationLoader.GetTranslationAsync("playlist_empty_error", lang), ephemeral: true);
                return;
            }

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

            foreach (var trackUri in lists)
            {
                if (player.Queue.Count() >= Program.MaxQueueCount)
                    break;

                var track = await _audioService.GetTrackAsync(trackUri);
                if (track != null)
                {
                    track.Context = new TrackContext
                    {
                        RequesterName = user.Username,
                    };

                    player.Queue.Add(track);
                    if (player.State != PlayerState.Playing)
                        await player.SkipAsync();
                }
            }

            await RespondAsync($"{await TranslationLoader.GetTranslationAsync("playlist_load_success", lang)}: {name}");
        }
    }
}