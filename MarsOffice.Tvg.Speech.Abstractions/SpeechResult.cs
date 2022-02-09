using System;
using System.Collections.Generic;
using System.Text;

namespace MarsOffice.Tvg.Speech.Abstractions
{
    public class SpeechResult
    {
        public string VideoId { get; set; }
        public string JobId { get; set; }
        public string UserId { get; set; }
        public string UserEmail { get; set; }
        public string FileLink { get; set; }
        public IEnumerable<long> IndividualDurationsInMillis { get; set; }
        public long TotalDurationInMillis { get; set; }
    }
}
