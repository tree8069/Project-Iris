using Discord.WebSocket;
using Lavalink4NET.Player;


namespace IrisBot
{
    internal sealed class IrisPlayer : QueuedLavalinkPlayer
    {
        // InactivityTrackingService 에서 안내 메세지를 보낼 채널 아이디를 보관하기 위해 CustomPlayer 클래스를 만들어 Channel 멤버변수 생성.
        // 기본 Player 클래스에는 해당 항목이 존재하지 않음.
        public ISocketMessageChannel? Channel { get; set; }
        
        public IrisPlayer()
            : base()
        {
        }
    }
}
