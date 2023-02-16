namespace IrisBot.Interfaces
{
    public interface IPlaylist
    {
        ulong GuildId { get; }
        string Name { get; set; }
    }
}
