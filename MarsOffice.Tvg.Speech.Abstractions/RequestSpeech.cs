using System;
using System.Collections.Generic;
using System.Text;

namespace MarsOffice.Tvg.Speech.Abstractions
{
    public class RequestSpeech
    {
        public string VideoId { get; set; }
        public string JobId { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public IEnumerable<string> Sentences { get; set; }
        public int? SpeechPitch { get; set; }
        public int? SpeechSpeed { get; set; }
        public string SpeechLanguage { get; set; }
        public string SpeechType { get; set; }
        public long? SpeechPauseBeforeInMillis { get; set; }
        public long? SpeechPauseAfterInMillis { get; set; }
    }
}
