using System;
using System.Threading.Tasks;
using MarsOffice.Tvg.Speech.Abstractions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace MarsOffice.Tvg.Speech
{
    public class RequestSpeechConsumer
    {
        private readonly IConfiguration _config;
        private readonly SpeechConfig _speechConfig;

        public RequestSpeechConsumer(IConfiguration config)
        {
            _config = config;
        }

        [FunctionName("RequestSpeechConsumer")]
        public async Task Run(
            [QueueTrigger("request-speech", Connection = "localsaconnectionstring")]RequestSpeech request,
            [Queue("speech-result", Connection = "localsaconnectionstring")] IAsyncCollector<SpeechResult> speechResultQueue,
            ILogger log)
        {
            try
            {
                var speechConfig = SpeechConfig.FromSubscription(_config["speechkey"], _config["location"].Replace(" ", "").ToLower());
                speechConfig.SpeechSynthesisLanguage = request.SpeechLanguage;
                speechConfig.SpeechSynthesisVoiceName = request.SpeechType;
                speechConfig.SetProfanity(ProfanityOption.Masked);
                speechConfig.OutputFormat = OutputFormat.Detailed;
                speechConfig.RequestWordLevelTimestamps();
                speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3);
                

            } catch (Exception e)
            {
                log.LogError(e, "Function threw an exception");
                await speechResultQueue.AddAsync(new SpeechResult
                {
                    Error = e.Message,
                    Success = false,
                    JobId = request.JobId,
                    VideoId = request.VideoId,
                    UserEmail = request.UserEmail,
                    UserId = request.UserId
                });
                await speechResultQueue.FlushAsync();
            }
        }
    }
}
