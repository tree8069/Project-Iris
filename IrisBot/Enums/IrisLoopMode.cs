using Discord.Interactions;

namespace IrisBot
{
    public enum IrisLoopMode
    {
        [ChoiceDisplay("Off")]
        None,
        [ChoiceDisplay("Single Track")]
        Track,
        [ChoiceDisplay("Entire Queue")]
        Queue
    }
}
