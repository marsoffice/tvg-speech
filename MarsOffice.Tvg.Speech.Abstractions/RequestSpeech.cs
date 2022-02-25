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
        public double? SpeechPitch { get; set; }
        public double? SpeechSpeed { get; set; }
        public string SpeechLanguage { get; set; }
        public string SpeechType { get; set; }
        public long? SpeechPauseBeforeInMillis { get; set; }
        public long? SpeechPauseAfterInMillis { get; set; }
    }
}
