using IrisBot.Database;
using IrisBot.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IrisBot.Translation
{
    public static class TranslationLoader
    {
        /// <summary>
        /// GuildId 정보를 받아와 List<GuildSettings> 에서 Language 값이 무엇인지 찾는다.
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns>서버의 언어 설정</returns>
        public static async Task<Translations> FindGuildTranslationAsync(ulong guildId)
        {
            GuildSettings? guildSettings = null;
            await Task.Run(() =>
            {
                guildSettings = GuildSettings.GetGuildsList().Find(x => x.GuildId == guildId);
            });

            if (guildSettings?.Language == null)
                return Translations.English;
            else
                return guildSettings.Language;
        }

        /// <summary>
        /// 지정된 메세지 코드의 문자열을 가져온다. 여러 언어의 메세지 관리를 용이하게 만들기 위한 함수.
        /// </summary>
        /// <param name="messageCode">json에 저장된 메세지 코드명</param>
        /// <param name="language">메세지 언어</param>
        /// <returns>메세지 코드에 맞는 문자열, 없을 경우 에러 메세지 반환</returns>
        public static async Task<string> GetTranslationAsync(string messageCode, Translations language)
        {
            string jsonPath = Program.TranslationsDirectory;
            switch (language)
            {
                case Translations.English:
                    jsonPath = Path.Combine(jsonPath, "English.json");
                    break;
                case Translations.Korean:
                    jsonPath = Path.Combine(jsonPath, "Korean.json");
                    break;
            }

            try
            {
                using (StreamReader file = File.OpenText(jsonPath))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    JObject json = (JObject)await JToken.ReadFromAsync(reader);
                    return json[messageCode]?.ToString() ?? $"Failed to load tranlsated message \"{messageCode}\"";
                }
            }
            catch (Exception ex)
            {
                await CustomLog.ExceptionHandler(ex);
                return $"Failed to load translated message \"{messageCode}\"";
            }
        }
    }
}
