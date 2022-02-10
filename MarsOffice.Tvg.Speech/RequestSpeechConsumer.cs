using System;
using System.Threading.Tasks;
using MarsOffice.Tvg.Speech.Abstractions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.IO;

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
                speechConfig.SpeechSynthesisLanguage = request.SpeechLanguage ?? "en-US";
                if (!string.IsNullOrEmpty(request.SpeechType))
                {
                    speechConfig.SpeechSynthesisVoiceName = request.SpeechType;
                }
                speechConfig.SetProfanity(ProfanityOption.Masked);
                speechConfig.OutputFormat = OutputFormat.Simple;
                speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio48Khz192KBitRateMonoMp3);

                using var synthesizer = new SpeechSynthesizer(speechConfig, null);
                var i = 0;

                foreach (var sentence in request.Sentences)
                {
                    var result = await synthesizer.SpeakTextAsync(sentence);

                    i++;
                }
                i.ToString();
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
