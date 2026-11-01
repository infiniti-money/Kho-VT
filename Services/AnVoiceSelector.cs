using System.Speech.Synthesis;

namespace HisaWarehouse.Services;

/// <summary>
/// An reads through a female Vietnamese voice exposed to SAPI by Windows.
/// A northern/Hanoi accent cannot be guaranteed by a locale tag: it depends
/// on the voice installed by the user. Never silently read Vietnamese using
/// an English voice (which sounds like incorrect speech recognition).
/// </summary>
public static class AnVoiceSelector
{
    public static string Configure(SpeechSynthesizer speaker)
    {
        var voices = speaker.GetInstalledVoices().Where(voice => voice.Enabled).ToList();
        var selected = voices.FirstOrDefault(voice =>
                voice.VoiceInfo.Culture.Name.Equals("vi-VN", StringComparison.OrdinalIgnoreCase) &&
                voice.VoiceInfo.Gender == VoiceGender.Female)
            ?? voices.FirstOrDefault(voice =>
                voice.VoiceInfo.Culture.TwoLetterISOLanguageName == "vi" &&
                voice.VoiceInfo.Gender == VoiceGender.Female);

        if (selected is null)
            throw new InvalidOperationException(
                "Windows 11 chưa có giọng NỮ tiếng Việt SAPI. Hãy cài giọng tiếng Việt " +
                "tương thích SpeechSynthesizer; chỉ chọn giọng nữ để tránh An đọc sai ngôn ngữ.");

        speaker.SelectVoice(selected.VoiceInfo.Name);
        speaker.Rate = -1;
        speaker.Volume = 95;
        return selected.VoiceInfo.Name;
    }
}
