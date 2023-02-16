using Discord;
using Discord.Interactions;
using IrisBot.Database;
using IrisBot.Enums;
using IrisBot.Translation;
using System.Text;

namespace IrisBot.Modules
{
    public class MiscCommandModule : InteractionModuleBase<ShardedInteractionContext>
    {
        [SlashCommand("shard", "Display bot information")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task InfoAsync()
        {
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithTitle($"{await TranslationLoader.GetTranslationAsync("shard_info_for", lang)}");
            foreach (var shard in Context.Client.Shards)
            {
                eb.AddField($"{await TranslationLoader.GetTranslationAsync("shard", lang)}: {shard.ShardId}", $"{shard.Latency} ms\n" +
                    $"{shard.Guilds.Count} {await TranslationLoader.GetTranslationAsync("server", lang)}\n" +
                    $"{shard.Guilds.Sum(x => x.MemberCount)} {await TranslationLoader.GetTranslationAsync("member", lang)}", true);
            }
            eb.WithDescription($"{await TranslationLoader.GetTranslationAsync("average_ping", lang)}: {Context.Client.Shards.Average(x => x.Latency)} ms");
            eb.WithFooter($"{await TranslationLoader.GetTranslationAsync("current_shard", lang)}: {Context.Client.GetShardFor(Context.Guild).ShardId}");
            eb.WithColor(Color.Purple);
            await RespondAsync("", embed: eb.Build());
        }

        [SlashCommand("language", "set language")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task Language(Translations language)
        {
            await GuildSettings.UpdateLanguageAsync(language, Context.Guild.Id);

            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            await RespondAsync(await TranslationLoader.GetTranslationAsync("language_change", lang));
        }

        [SlashCommand("help", "help about command")]
        [RequireBotPermission(GuildPermission.EmbedLinks)]
        [RequireBotPermission(GuildPermission.SendMessages)]
        public async Task HelpAsync()
        {
            Translations lang = await TranslationLoader.FindGuildTranslationAsync(Context.Guild.Id);
            EmbedBuilder eb = new EmbedBuilder();
            eb.WithAuthor(Context.Client.CurrentUser);
            eb.WithDescription($"{await TranslationLoader.GetTranslationAsync("bot_description", lang)}\r\n" +
                $"[Github](https://github.com/tree8069/Project-Iris), " +
                $"[{await TranslationLoader.GetTranslationAsync("invitation_link", lang)}](https://discord.com/api/oauth2/authorize?client_id=930387137436721172&permissions=551940057088&scope=bot)");

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_join", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_music", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_pause", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_resume", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_seek", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_volume", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_leave", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_searchmode", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_musictop", lang));
            eb.AddField(await TranslationLoader.GetTranslationAsync("help_music_header", lang), sb.ToString());
            sb.Clear();

            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_list", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_skip", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_shuffle", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_remove", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_mremove", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_clear", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_loop", lang));
            eb.AddField(await TranslationLoader.GetTranslationAsync("help_queue_header", lang), sb.ToString());
            sb.Clear();

            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_shard", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_language", lang));
            eb.AddField(await TranslationLoader.GetTranslationAsync("help_misc_header", lang), sb.ToString());
            sb.Clear();

            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_playlist_add", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_playlist_list", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_playlist_remove", lang));
            sb.AppendLine(await TranslationLoader.GetTranslationAsync("help_playlist_load", lang));
            eb.AddField(await TranslationLoader.GetTranslationAsync("help_playlist_header", lang), sb.ToString());
            sb.Clear();

            eb.WithColor(Color.Purple);
            await RespondAsync("", embed: eb.Build(), ephemeral: true);
        }
    }
}
